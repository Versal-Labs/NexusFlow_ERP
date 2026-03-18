window.grnApp = {
    _table: null,
    _modal: null,

    init: async function () {
        try {
            var modalEl = document.getElementById('grnModal');
            if (modalEl) this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false });

            this._initGrid();

            // Await master data so dropdowns exist before we process Deep Links
            await this._loadMasterData();

            // Event Listeners
            const self = this;

            $('#PurchaseOrderId').on('change', function () {
                const poId = $(this).val();
                if (poId) {
                    self._loadPoDetails(poId);
                } else {
                    $('#linesBody').html('<tr><td colspan="6" class="text-center text-muted fst-italic py-4">Select a Purchase Order to load items.</td></tr>');
                    $('#poStatusBadge').hide();
                    self.calculateTotals();
                }
            });

            // Recalculate totals dynamically as user types received quantities
            $('#linesBody').on('input', '.receive-qty', function () {
                self.calculateTotals();
            });

            // --- THE DEEP-LINKING ENGINE ---
            // If navigated from the PO screen via "?poId=123"
            const urlParams = new URLSearchParams(window.location.search);
            const poIdFromUrl = urlParams.get('poId');

            if (poIdFromUrl) {
                this.openModal();
                // Select the PO, which triggers the 'change' event and loads the lines automatically
                $('#PurchaseOrderId').val(poIdFromUrl).trigger('change');

                // Clean the URL so refreshing doesn't keep opening the modal
                window.history.replaceState({}, document.title, window.location.pathname);
            }

        } catch (e) {
            console.error("[GrnApp] Init Error:", e);
        }
    },

    _initGrid: function () {
        this._table = $('#grnGrid').DataTable({
            ajax: {
                url: '/api/purchasing/grns',
                dataSrc: function (json) { return json.data || json || []; },
                headers: { "Authorization": "Bearer " + localStorage.getItem("jwtToken") }
            },
            columns: [
                { data: 'grnNumber', className: 'fw-bold text-primary font-monospace' },
                { data: 'receiptDate', render: d => new Date(d).toLocaleDateString() },
                { data: 'supplierName', className: 'fw-bold text-dark' },
                { data: 'warehouseName' },
                { data: 'referenceNo', className: 'font-monospace text-muted', defaultContent: '-' },
                {
                    data: 'totalValue',
                    className: 'text-end fw-bold text-dark',
                    render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 })
                }
            ],
            order: [[0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 20
        });
    },

    _loadMasterData: async function () {
        try {
            // Fetch Warehouses & OPEN Purchase Orders
            const [whRes, poRes] = await Promise.all([
                api.get('/api/masterdata/warehouses'),
                api.get('/api/purchasing/purchase-orders')
            ]);

            const warehouses = whRes.data || whRes;
            const pos = poRes.data || poRes;

            let whEl = $('#WarehouseId');
            whEl.empty().append('<option value="">-- Select Destination --</option>');
            if (warehouses) warehouses.forEach(w => whEl.append(`<option value="${w.id}">[${w.code}] ${w.name}</option>`));

            let poEl = $('#PurchaseOrderId');
            poEl.empty().append('<option value="">-- Select Purchase Order --</option>');

            // Only show POs that aren't fully Closed
            if (pos) {
                pos.filter(p => p.status !== 'Closed').forEach(p => {
                    poEl.append(`<option value="${p.id}">[${p.poNumber}] ${p.supplierName} - ${new Date(p.date).toLocaleDateString()}</option>`);
                });
            }

            // Unbind and rebind Select2 for safety inside modals
            if (poEl.hasClass("select2-hidden-accessible")) poEl.select2('destroy');
            poEl.select2({ dropdownParent: $('#grnModal') });

        } catch (e) {
            console.error("[GrnApp] Master Data Error:", e);
        }
    },

    _loadPoDetails: async function (poId) {
        $('#linesBody').html('<tr><td colspan="6" class="text-center py-4"><div class="spinner-border spinner-border-sm text-primary"></div> Loading PO lines...</td></tr>');

        try {
            const res = await api.get(`/api/purchasing/purchase-orders/${poId}`);
            const po = res.data || res;

            $('#poStatusBadge').text(`PO Status: ${po.status}`).show();

            let html = '';
            let hasPendingItems = false;

            if (po.items && po.items.length > 0) {
                po.items.forEach(item => {
                    // Calculate what hasn't been received yet
                    const pendingQty = item.quantityOrdered - item.quantityReceived;

                    if (pendingQty > 0) {
                        hasPendingItems = true;
                        html += `
                            <tr data-variant-id="${item.productVariantId}">
                                <td class="fw-bold text-dark text-start">
                                    ${item.productName || 'Product Variant ' + item.productVariantId}
                                    ${item.sku ? `<div class="small text-muted font-monospace">${item.sku}</div>` : ''}
                                </td>
                                <td class="text-center align-middle">${item.quantityOrdered}</td>
                                <td class="text-center align-middle text-primary fw-bold">${item.quantityReceived}</td>
                                <td class="bg-primary bg-opacity-10 border-primary">
                                    <input type="number" class="form-control form-control-sm text-center fw-bold text-primary receive-qty" 
                                           value="${pendingQty}" min="0" max="${pendingQty * 1.05}" step="0.01">
                                </td>
                                <td class="align-middle">
                                    <input type="number" class="form-control form-control-sm text-end unit-cost" value="${item.unitCost}" readonly>
                                </td>
                                <td class="text-end align-middle fw-bold line-total">0.00</td>
                            </tr>
                        `;
                    }
                });
            }

            if (!hasPendingItems) {
                html = '<tr><td colspan="6" class="text-center text-success fw-bold py-4"><i class="fa-solid fa-check-circle me-1 fs-5"></i><br>All items on this PO have been fully received.</td></tr>';
            }

            $('#linesBody').html(html);
            this.calculateTotals();

        } catch (e) {
            $('#linesBody').html('<tr><td colspan="6" class="text-center text-danger py-4">Error loading PO items.</td></tr>');
            console.error(e);
        }
    },

    calculateTotals: function () {
        let grandTotal = 0;
        $('#linesBody tr').each(function () {
            const qtyInput = $(this).find('.receive-qty');
            if (qtyInput.length > 0) {
                const qty = parseFloat(qtyInput.val()) || 0;
                const cost = parseFloat($(this).find('.unit-cost').val()) || 0;

                const lineTotal = qty * cost;
                $(this).find('.line-total').text(lineTotal.toLocaleString(undefined, { minimumFractionDigits: 2 }));
                grandTotal += lineTotal;
            }
        });

        $('#TotalAmount').val(grandTotal);
        $('#lblGrandTotal').text('Rs. ' + grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2 }));
    },

    openModal: function () {
        // Reset safely
        var form = document.getElementById('grnForm');
        if (form) form.reset();

        var dateEl = document.getElementById('DateReceived');
        if (dateEl) dateEl.valueAsDate = new Date();

        $('#PurchaseOrderId').val('').trigger('change');

        if (this._modal) this._modal.show();
    },

    save: async function () {
        var form = document.getElementById('grnForm');
        if (!form || !form.checkValidity()) {
            if (form) form.reportValidity();
            return;
        }

        const totalAmount = parseFloat($('#TotalAmount').val()) || 0;
        if (totalAmount <= 0) {
            toastr.warning("You must receive at least one item with a value greater than zero.");
            return;
        }

        const payload = {
            PurchaseOrderId: parseInt($('#PurchaseOrderId').val()),
            DateReceived: $('#DateReceived').val(),
            WarehouseId: parseInt($('#WarehouseId').val()),
            SupplierInvoiceNo: $('#SupplierInvoiceNo').val() || "N/A",
            Items: []
        };

        // Harvest lines from the table
        $('#linesBody tr').each(function () {
            const variantId = $(this).data('variant-id');
            const receiveQty = parseFloat($(this).find('.receive-qty').val()) || 0;
            const unitCost = parseFloat($(this).find('.unit-cost').val()) || 0;

            if (variantId && receiveQty > 0) {
                payload.Items.push({
                    ProductVariantId: parseInt(variantId),
                    QuantityReceived: receiveQty,
                    UnitCost: unitCost
                });
            }
        });

        if (payload.Items.length === 0) {
            toastr.warning("No items selected to receive.");
            return;
        }

        // Disable button to prevent double-clicks
        const btn = $(event.currentTarget);
        const originalHtml = btn.html();
        btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-1"></i> Processing...');

        try {
            const res = await api.post('/api/purchasing/grns', payload);

            if (res && res.succeeded) {
                toastr.success(res.messages ? res.messages[0] : "GRN Posted Successfully!");
                this._modal.hide();
                this._table.ajax.reload(null, false);
            } else {
                toastr.error(res.messages ? res.messages[0] : "Failed to post GRN.");
            }
        } finally {
            // Restore button
            btn.prop('disabled', false).html(originalHtml);
        }
    }
};

$(document).ready(function () { window.grnApp.init(); });