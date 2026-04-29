window.grnApp = {
    _table: null,
    _modal: null,
    _viewModal: null,

    init: async function () {
        var modalEl = document.getElementById('grnModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false });

        var viewEl = document.getElementById('grnViewModal');
        if (viewEl) this._viewModal = new bootstrap.Modal(viewEl);

        this._initFilters();
        this._initGrid();
        await this._loadMasterData();

        const self = this;
        $('#PurchaseOrderId').on('change', function () {
            const poId = $(this).val();
            if (poId) self._loadPoDetails(poId);
            else {
                $('#linesBody').html('<tr><td colspan="6" class="text-center text-muted fst-italic py-4">Select a Purchase Order to load items.</td></tr>');
                $('#poStatusBadge').hide();
                self.calculateTotals();
            }
        });

        $('#linesBody').on('input', '.receive-qty', () => self.calculateTotals());

        // Deep link handler
        const poIdFromUrl = new URLSearchParams(window.location.search).get('poId');
        if (poIdFromUrl) {
            this.openModal();
            $('#PurchaseOrderId').val(poIdFromUrl).trigger('change');
            window.history.replaceState({}, document.title, window.location.pathname);
        }
    },

    _initFilters: function () {
        api.get('/api/Supplier').then(res => {
            let suppliers = Array.isArray(res) ? res : (res?.data || []);
            let $sup = $('#filterSupplier').empty().append('<option value="">All Suppliers</option>');
            suppliers.forEach(s => $sup.append($('<option></option>').val(s.name).text(s.name)));
        }).catch(err => console.error(err));

        $('#filterSupplier, #filterWarehouse, #filterStartDate, #filterEndDate').on('change', () => this.reloadGrid());

        $.fn.dataTable.ext.search.push((settings, data, dataIndex) => {
            if (settings.nTable.id !== 'grnGrid') return true;

            const filterSup = ($('#filterSupplier').val() || '').toLowerCase();
            const filterWh = ($('#filterWarehouse').val() || '').toLowerCase();
            const filterStart = $('#filterStartDate').val();
            const filterEnd = $('#filterEndDate').val();

            const rowDateStr = data[2] || '';
            const rowSup = (data[3] || '').toLowerCase();
            const rowWh = (data[4] || '').toLowerCase();

            if (filterSup && !rowSup.includes(filterSup)) return false;
            if (filterWh && !rowWh.includes(filterWh)) return false;

            if (filterStart || filterEnd) {
                if (!rowDateStr) return false;
                const rowDate = new Date(rowDateStr);
                if (filterStart && rowDate < new Date(filterStart)) return false;
                if (filterEnd && rowDate > new Date(filterEnd + 'T23:59:59')) return false;
            }
            return true;
        });
    },

    resetFilters: function () {
        $('#filterSupplier, #filterWarehouse, #filterStartDate, #filterEndDate').val('');
        this.reloadGrid();
    },

    reloadGrid: function () { this._table.ajax.reload(); },

    _initGrid: function () {
        this._table = $('#grnGrid').DataTable({
            ajax: function (data, callback, settings) {
                (async () => {
                    try {
                        const response = await api.get('/api/purchasing/grns');
                        callback({ data: response.data || response || [] });
                    } catch (e) { callback({ data: [] }); }
                })();
                return { abort: function () { } };
            },
            columns: [
                { data: 'grnNumber', className: 'fw-bold text-success font-monospace' },
                { data: 'poNumber', className: 'font-monospace text-muted' },
                { data: 'receiptDate', render: d => d ? new Date(d).toLocaleDateString() : '-' },
                { data: 'supplierName', className: 'fw-bold text-dark' },
                { data: 'warehouseName' },
                { data: 'referenceNo', className: 'font-monospace text-muted', defaultContent: '-' },
                { data: 'totalValue', className: 'text-end fw-bold text-dark', render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                {
                    data: null, className: 'text-center',
                    render: () => '<span class="badge bg-success">Posted</span>'
                },
                {
                    data: null, className: 'text-end pe-3', orderable: false,
                    render: function (data, type, row) {
                        return `<button class="btn btn-sm btn-outline-dark shadow-sm me-1" onclick="grnApp.viewDocument(${row.id})" title="View Document"><i class="fa-solid fa-eye"></i></button>`;
                    }
                }
            ],
            order: [[0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 20
        });
    },

    _loadMasterData: async function () {
        try {
            const [whRes, poRes] = await Promise.all([
                api.get('/api/masterdata/warehouses'),
                api.get('/api/purchasing/purchase-orders')
            ]);

            const warehouses = whRes.data || whRes || [];
            const pos = poRes.data || poRes || [];

            let whEl = $('#WarehouseId');
            let fWhEl = $('#filterWarehouse').empty().append('<option value="">All Warehouses</option>');
            whEl.empty().append('<option value="">-- Select Destination --</option>');

            if (warehouses) warehouses.forEach(w => {
                whEl.append(`<option value="${w.id}">[${w.code}] ${w.name}</option>`);
                fWhEl.append(`<option value="${w.name}">${w.name}</option>`);
            });

            let poEl = $('#PurchaseOrderId');
            poEl.empty().append('<option value="">-- Select Purchase Order --</option>');
            if (pos) {
                pos.filter(p => p.status !== 'Closed' && p.status !== 'Draft' && p.status !== 'Cancelled').forEach(p => {
                    poEl.append(`<option value="${p.id}">[${p.poNumber}] ${p.supplierName} - ${new Date(p.date).toLocaleDateString()}</option>`);
                });
            }

            if (poEl.hasClass("select2-hidden-accessible")) poEl.select2('destroy');
            poEl.select2({ dropdownParent: $('#grnModal'), width: '100%' });
        } catch (e) { console.error("[GrnApp] Master Data Error:", e); }
    },

    // --- RESTORED FUNCTIONS ---
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
                                           value="${pendingQty}" min="0" max="${pendingQty * 1.05}" step="0.01" onclick="this.select()">
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
        var form = document.getElementById('grnForm');
        if (form) form.reset();

        var dateEl = document.getElementById('DateReceived');
        if (dateEl) dateEl.valueAsDate = new Date();

        $('#PurchaseOrderId').val('').trigger('change');

        if (this._modal) this._modal.show();
    },

    save: async function (e) {
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
        const btn = $(e.currentTarget);
        const originalHtml = btn.html();
        btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-1"></i> Processing...');

        try {
            const res = await api.post('/api/purchasing/grns', payload);

            if (res && res.succeeded) {
                toastr.success(res.message || "GRN Posted Successfully!");
                this._modal.hide();
                this._table.ajax.reload(null, false);
            } else {
                toastr.error(res?.messages?.[0] || "Failed to post GRN.");
            }
        } catch (err) {
            toastr.error("Network Error.");
        } finally {
            btn.prop('disabled', false).html(originalHtml);
        }
    },

    // --- VIEWER MODAL LOGIC ---
    viewDocument: async function (id) {
        try {
            const res = await api.get(`/api/purchasing/grns/${id}`);
            const doc = res.data || res;

            $('#docGrnNo').text(doc.grnNumber);
            $('#docPoNo').text(doc.poNumber);
            $('#docDate').text(new Date(doc.receiptDate).toLocaleDateString());
            $('#docSupplier').text(doc.supplierName);
            $('#docWarehouse').text(doc.warehouseName);
            $('#docRefNo').text(doc.referenceNo || 'N/A');

            const totalStr = parseFloat(doc.totalValue || 0).toLocaleString(undefined, { minimumFractionDigits: 2 });
            $('#docTotalLarge').text(`$${totalStr}`);

            let tbody = '';
            if (doc.items) {
                doc.items.forEach(i => {
                    let qty = parseFloat(i.quantityReceived || 0);
                    let cost = parseFloat(i.unitCost || 0);
                    let lineGross = qty * cost;

                    tbody += `
                        <tr>
                            <td class="fw-bold">${i.productName}</td>
                            <td class="text-center font-monospace fw-bold text-success">${qty}</td>
                            <td class="text-end">${cost.toFixed(2)}</td>
                            <td class="text-end fw-bold">${lineGross.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                        </tr>`;
                });
            }
            $('#docItemsBody').html(tbody);

            // Tier-1: Easy navigation to AP Billing
            $('#btnModalCreateBill').off('click').on('click', function () {
                window.location.href = `/Purchasing/SupplierBills?grnId=${id}`;
            });

            this._viewModal.show();
        } catch (e) {
            toastr.error("Failed to load GRN document.");
            console.error(e);
        }
    }
};

$(document).ready(function () { window.grnApp.init(); });