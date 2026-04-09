window.transferApp = {
    _table: null,
    _modal: null,
    _warehouses: [],
    _products: [],

    init: function() {
        var modalEl = document.getElementById('transferModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });

        var viewEl = document.getElementById('viewTransferModal');
        if (viewEl) this._viewModal = new bootstrap.Modal(viewEl);

        this._initGrid();
        this._loadLookups();

        $('#btnModalReverse').click(() => this.reverseTransfer(this._currentRefNo));
        $('#btnModalPrint').click(() => window.open(`/api/inventory/transfers/${this._currentRefNo}/pdf`, '_blank'));

        // TIER-1 ERP FEATURE: If they change the warehouses mid-entry, clear the lines!
        $('#SourceWarehouseId, #TargetWarehouseId').on('change', () => {
            $('#linesBody').empty();
            this.addLine();
        });

        // TIER-1 ERP FEATURE: Real-time UI validation to block over-transferring
        $(document).on('input', '.qty-input', function() {
            let maxQty = parseFloat($(this).attr('max'));
            let currentVal = parseFloat($(this).val()) || 0;
            
            if (maxQty !== undefined && currentVal > maxQty) {
                $(this).val(maxQty); // Snap it back to max
                toastr.warning(`Cannot transfer more than the available source stock (${maxQty}).`);
            }
        });
    },

    _initGrid: function() {
        this._table = $('#transfersGrid').DataTable({
            ajax: async function(data, callback) {
                try {
                    const res = await api.get('/api/inventory/transfers');
                    callback({ data: res.data || res || [] });
                } catch (e) { callback({ data: [] }); }
            },
            columns: [
                { data: 'referenceNo', className: 'fw-bold text-primary font-monospace ps-3' },
                { data: 'date', render: d => new Date(d).toLocaleDateString() },
                { data: 'sourceWarehouse', className: 'fw-bold text-danger' },
                { data: 'targetWarehouse', className: 'fw-bold text-success' },
                { data: 'totalItems', className: 'text-center' },
                { data: 'totalValue', className: 'text-end fw-bold pe-3', render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                {
                    // TIER-1 ERP FEATURE: THE ACTIONS COLUMN
                    data: null, className: 'text-end pe-3', orderable: false,
                    render: function(data, type, row) {
                        return `<button class="btn btn-sm btn-outline-dark shadow-sm me-1" onclick="transferApp.viewTransfer('${row.referenceNo}')" title="View Transfer"><i class="fa-solid fa-eye"></i></button>
                                <button class="btn btn-sm btn-outline-danger shadow-sm me-1" onclick="transferApp.reverseTransfer('${row.referenceNo}')" title="Reverse Movement"><i class="fa-solid fa-rotate-left"></i></button>
                                <button class="btn btn-sm btn-outline-secondary shadow-sm" onclick="window.open('/api/inventory/transfers/${row.referenceNo}/pdf', '_blank')" title="Print Delivery Note"><i class="fa-solid fa-print"></i></button>`;
                    }
                }
            ],
            order: [[1, 'desc'], [0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    // ==========================================
    // VIEW & REVERSE ACTIONS
    // ==========================================
    viewTransfer: async function(refNo) {
        try {
            this._currentRefNo = refNo;
            const doc = await api.get(`/api/inventory/transfers/${refNo}`);
            
            $('#viewRefNo').text(doc.referenceNo);
            $('#viewDate').text(new Date(doc.date).toLocaleDateString());
            $('#viewSource').text(doc.sourceWarehouse);
            $('#viewTarget').text(doc.targetWarehouse);
            $('#viewNotes').text(doc.notes || '');
            $('#viewTotalValue').text('$' + doc.totalValue.toLocaleString(undefined, { minimumFractionDigits: 2 }));

            let tbody = '';
            doc.items.forEach(item => {
                tbody += `
                    <tr>
                        <td class="fw-bold">${item.description}</td>
                        <td class="font-monospace text-muted fs-12">${item.sku}</td>
                        <td class="text-center fw-bold">${item.qty}</td>
                        <td class="text-end fw-bold text-success">${item.totalValue.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                    </tr>`;
            });
            $('#viewItemsBody').html(tbody);

            // Hide Reverse button if this is already a reversal
            if (doc.notes && doc.notes.startsWith("Reversal of")) {
                $('#btnModalReverse').hide();
                $('#viewRefNo').append(' <span class="badge bg-warning text-dark ms-2">REVERSAL</span>');
            } else {
                $('#btnModalReverse').show();
            }

            this._viewModal.show();
        } catch (e) { toastr.error("Failed to load transfer details."); }
    },

    reverseTransfer: async function(refNo) {
        const result = await Swal.fire({
            title: 'Reverse this Transfer?',
            text: "This will automatically create a new transfer moving the exact quantities back from the Target warehouse to the Source warehouse. Proceed?",
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#dc3545',
            cancelButtonColor: '#6c757d',
            confirmButtonText: 'Yes, Execute Reversal',
            reverseButtons: true
        });

        if (!result.isConfirmed) return;

        try {
            const res = await api.post(`/api/inventory/transfers/${refNo}/reverse`);
            if (res && res.succeeded) {
                toastr.success(res.message);
                if (this._viewModal) this._viewModal.hide();
                this._table.ajax.reload(null, false);
            } else if (res && res.messages) { toastr.error(res.messages[0]); }
        } catch (e) {
            toastr.error(e.responseJSON?.messages?.[0] || "Failed to reverse. The target warehouse may no longer have enough stock.");
        }
    },

    _loadLookups: async function () {
        try {
            const [whRes, prodRes] = await Promise.all([
                api.get('/api/masterdata/warehouses'),
                api.get('/api/product') // Your product lookup endpoint
            ]);

            this._warehouses = whRes.data || whRes || [];
            this._products = prodRes.data || prodRes || [];

            let $src = $('#SourceWarehouseId').empty().append('<option value="">-- Select Source --</option>');
            let $tgt = $('#TargetWarehouseId').empty().append('<option value="">-- Select Target --</option>');
            
            this._warehouses.forEach(w => {
                $src.append(`<option value="${w.id}">${w.name}</option>`);
                $tgt.append(`<option value="${w.id}">${w.name}</option>`);
            });

        } catch (e) { console.error("Lookup Error:", e); }
    },

    openCreateModal: function () {
        $('#transferForm')[0].reset();
        $('#transferForm').removeClass('was-validated');
        $('#TransferDate').val(new Date().toISOString().split('T')[0]);
        $('#linesBody').empty();
        this.addLine(); // Add one default line
        this._modal.show();
    },

    addLine: function() {
        const id = Date.now();
        let prodOpts = '<option value="">-- Select Item --</option>';
        
        this._products.forEach(p => {
            if (p.variants) {
                p.variants.forEach(v => {
                    prodOpts += `<option value="${v.id}">${p.name} - ${v.sku}</option>`;
                });
            }
        });

        // Updated HTML to include the Source/Target quantity labels
        const html = `
            <tr id="row_${id}">
                <td class="ps-3">
                    <select class="form-select form-select-sm variant-select select2" required style="width:100%">${prodOpts}</select>
                </td>
                <td class="text-end align-middle">
                    <span class="badge bg-danger bg-opacity-10 text-danger border border-danger w-100 lbl-source-qty">0.00</span>
                </td>
                <td class="text-end align-middle">
                    <span class="badge bg-success bg-opacity-10 text-success border border-success w-100 lbl-target-qty">0.00</span>
                </td>
                <td>
                    <input type="number" class="form-control form-control-sm qty-input text-center fw-bold" value="1" min="0.01" step="0.01" required disabled>
                </td>
                <td class="text-center pe-2 align-middle">
                    <button type="button" class="btn btn-sm text-danger" onclick="$('#row_${id}').remove();"><i class="fa-solid fa-trash-can"></i></button>
                </td>
            </tr>`;
            
        $('#linesBody').append(html);
        const $select = $(`#row_${id} .select2`).select2({ dropdownParent: $('#transferModal') });

        // ATTACH REAL-TIME FETCH EVENT
        $select.on('change', async function() {
            const variantId = $(this).val();
            const sourceId = $('#SourceWarehouseId').val();
            const targetId = $('#TargetWarehouseId').val();
            
            const $row = $(this).closest('tr');
            const $srcLbl = $row.find('.lbl-source-qty');
            const $tgtLbl = $row.find('.lbl-target-qty');
            const $qtyInput = $row.find('.qty-input');

            if (!variantId) {
                $srcLbl.text('0.00'); $tgtLbl.text('0.00'); 
                $qtyInput.val(1).prop('disabled', true).removeAttr('max');
                return;
            }

            if (!sourceId || !targetId) {
                toastr.error("Please select both Source and Target warehouses first.");
                $(this).val('').trigger('change.select2');
                return;
            }

            if (sourceId === targetId) {
                toastr.error("Source and Target warehouse cannot be the same!");
                $(this).val('').trigger('change.select2');
                return;
            }

            // Show loading spinners in the badges
            $srcLbl.html('<i class="fa-solid fa-spinner fa-spin"></i>');
            $tgtLbl.html('<i class="fa-solid fa-spinner fa-spin"></i>');

            try {
                // Fetch both stock levels perfectly in parallel
                const [srcRes, tgtRes] = await Promise.all([
                    api.get(`/api/inventory/stock-level?variantId=${variantId}&warehouseId=${sourceId}`),
                    api.get(`/api/inventory/stock-level?variantId=${variantId}&warehouseId=${targetId}`)
                ]);

                const sourceQty = srcRes.data || 0;
                const targetQty = tgtRes.data || 0;

                // Update UI visually
                $srcLbl.text(sourceQty.toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $tgtLbl.text(targetQty.toLocaleString(undefined, { minimumFractionDigits: 2 }));
                
                // Enforce Logic
                if (sourceQty <= 0) {
                    $qtyInput.val(0).prop('disabled', true).removeAttr('max');
                    toastr.error("Item is out of stock in the Source warehouse.");
                } else {
                    $qtyInput.prop('disabled', false).attr('max', sourceQty);
                    if (parseFloat($qtyInput.val()) > sourceQty) {
                        $qtyInput.val(sourceQty); // Snap to max if default '1' is greater than available
                    }
                }
            } catch (e) {
                $srcLbl.text('Error'); $tgtLbl.text('Error');
            }
        });
    },

    saveTransfer: async function(e) {
        var form = $('#transferForm')[0];
        if (!form.checkValidity()) { $(form).addClass('was-validated'); return; }

        const sourceId = $('#SourceWarehouseId').val();
        const targetId = $('#TargetWarehouseId').val();

        if (sourceId === targetId) {
            toastr.error("Source and Target warehouse cannot be the same!");
            return;
        }

        let items = [];
        let hasError = false;

        $('#linesBody tr').each(function() {
            let variantId = parseInt($(this).find('.variant-select').val());
            let qty = parseFloat($(this).find('.qty-input').val()) || 0;
            
            if (!variantId || qty <= 0) hasError = true;
            else items.push({ ProductVariantId: variantId, Quantity: qty });
        });

        if (hasError || items.length === 0) {
            toastr.error("Please ensure all rows have a selected product and a valid quantity greater than 0.");
            return;
        }

        const payload = {
            SourceWarehouseId: parseInt(sourceId),
            TargetWarehouseId: parseInt(targetId),
            TransferDate: $('#TransferDate').val(),
            Notes: $('#Notes').val(),
            Items: items
        };

        var $btn = $(e.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Processing FIFO...');

        try {
            const res = await api.post('/api/inventory/transfers', payload);
            if (res && res.succeeded) {
                toastr.success(res.message || "Transfer Executed.");
                this._modal.hide();
                this._table.ajax.reload(null, false);
            } else if (res && res.messages) { toastr.error(res.messages[0]); }
        } catch (err) { 
            toastr.error(err.responseJSON?.messages?.[0] || "Insufficient stock or transfer failed."); 
        } finally { 
            $btn.prop('disabled', false).html(ogText); 
        }
    }
};

$(document).ready(() => transferApp.init());