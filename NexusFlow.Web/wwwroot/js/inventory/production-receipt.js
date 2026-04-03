window.receiptApp = {
    _modal: null,
    _table: null,

    init: function () {
        var modalEl = document.getElementById('receiptModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl);

        document.getElementById('receiptDate').valueAsDate = new Date();
        
        this._initGrid();
        this._loadLookups();

        $('#btnSaveReceipt').on('click', (e) => this.save(e));
    },

    _initGrid: function () {
        // Placeholder for DataTables. You will implement the Dapper Query for this later.
        this._table = $('#receiptsGrid').DataTable({
            ajax: {
                url: '/api/inventory/production-receipts', // Ensure you create a GET endpoint
                dataSrc: function (json) { return json.data || json || []; },
                error: function() { /* Handle empty state gently */ }
            },
            columns: [
                { data: 'receiptNo', className: 'fw-bold font-monospace text-primary ps-4', defaultContent: 'N/A' },
                { data: 'date', render: d => d ? new Date(d).toLocaleDateString() : '-' },
                { data: 'finishedGood', className: 'fw-bold text-dark', defaultContent: '-' },
                { data: 'quantity', className: 'text-center', defaultContent: '0' },
                { data: 'totalValue', className: 'text-end fw-bold', render: d => d ? parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) : '0.00' },
                { data: 'unitCost', className: 'text-end', render: d => d ? parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) : '0.00' },
                {
                    data: 'status',
                    className: 'text-center pe-4',
                    render: () => '<span class="badge bg-success"><i class="fa-solid fa-check-double me-1"></i>Capitalized</span>'
                }
            ],
            order: [[0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    _loadLookups: async function () {
        try {
            // Using your global 'api' wrapper
            const whRes = await api.get('/api/masterdata/warehouses');
            const warehouses = whRes.data || whRes || [];
            let $wh = $('#warehouseId').empty().append('<option value="">-- Select Destination --</option>');
            warehouses.forEach(w => $wh.append($('<option></option>').val(w.id).text(w.name)));

            // Finished Good Select2
            $('#finishedGoodId').select2({
                theme: 'bootstrap-5',
                dropdownParent: $('#receiptModal'),
                placeholder: 'Search Target Product...',
                ajax: {
                    url: '/api/masterdata/variants/search',
                    headers: { 'Authorization': `Bearer ${localStorage.getItem('nexus_token')}` },
                    dataType: 'json',
                    delay: 250,
                    data: params => ({ query: params.term, productType: 1 }), // 1 = Finished Good
                    processResults: data => ({
                        results: $.map(data, item => ({ id: item.id, text: `${item.sku} - ${item.name}` }))
                    })
                }
            });
        } catch (e) {
            console.error("Lookup Error:", e);
        }
    },

    openCreateModal: function () {
        $('#receiptForm')[0].reset();
        document.getElementById('receiptDate').valueAsDate = new Date();
        $('#finishedGoodId').val(null).trigger('change');
        this._modal.show();
    },

    save: async function (e) {
        if (!$('#receiptForm')[0].checkValidity()) {
            $('#receiptForm')[0].reportValidity();
            return;
        }

        const payload = {
            receiptDate: $('#receiptDate').val(),
            issueReferenceNo: $('#issueReferenceNo').val().trim(),
            finishedGoodVariantId: parseInt($('#finishedGoodId').val()),
            warehouseId: parseInt($('#warehouseId').val()),
            batchNo: $('#batchNo').val().trim(),
            quantityReceived: parseFloat($('#quantityReceived').val()),
            subcontractorCharge: parseFloat($('#subcontractorCharge').val())
        };

        var $btn = $(e.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Capitalizing Costs...');

        try {
            // Using your global 'api' wrapper
            const res = await api.post('/api/inventory/production-receipts', payload);
            
            if (res && res.succeeded) {
                toastr.success(res.message);
                this._modal.hide();
                this._table.ajax.reload(null, false);
            } else if (res && res.messages) {
                toastr.error(res.messages[0]);
            }
        } catch (err) {
            const errorMessage = err.responseJSON?.messages?.[0] || err.message || "Failed to execute Receipt. Verify the Issue Note exists.";
            toastr.error(errorMessage);
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    }
};

$(document).ready(() => window.receiptApp.init());