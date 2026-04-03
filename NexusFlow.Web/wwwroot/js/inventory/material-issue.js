window.issueApp = {
    _modal: null,
    _table: null,

    init: function () {
        var modalEl = document.getElementById('issueModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl);

        document.getElementById('issueDate').valueAsDate = new Date();
        
        this._initGrid();
        this._loadLookups();

        $('#btnSaveIssue').on('click', (e) => this.save(e));
    },

    _initGrid: function () {
        this._table = $('#issuesGrid').DataTable({
            ajax: {
                url: '/api/inventory/issues',
                dataSrc: function (json) { return json.data || json || []; }
            },
            columns: [
                { data: 'issueNo', className: 'fw-bold font-monospace text-primary ps-4' },
                { data: 'issueDate', render: d => d ? new Date(d).toLocaleDateString() : '-' },
                { data: 'warehouseName', className: 'fw-bold text-dark' },
                { 
                    data: 'totalCost', 
                    className: 'text-end fw-bold',
                    render: d => d ? parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) : '0.00'
                },
                {
                    data: null,
                    className: 'text-center pe-4',
                    render: () => '<span class="badge bg-primary"><i class="fa-solid fa-truck me-1"></i>Dispatched</span>'
                }
            ],
            order: [[0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    _loadLookups: async function () {
        try {
            // Load Warehouses via custom api wrapper
            const whRes = await api.get('/api/masterdata/warehouses');
            const warehouses = whRes.data || whRes || [];
            let $wh = $('#warehouseId').empty().append('<option value="">-- Select Source Warehouse --</option>');
            warehouses.forEach(w => $wh.append($('<option></option>').val(w.id).text(w.name)));

            // Initialize Subcontractor Select2 -> Hits the new search endpoint
            $('#subcontractorId').select2({
                theme: 'bootstrap-5',
                dropdownParent: $('#issueModal'),
                placeholder: 'Search Garment Factory...',
                ajax: {
                    url: '/api/purchasing/suppliers/search', 
                    headers: { 'Authorization': `Bearer ${localStorage.getItem('nexus_token')}` },
                    dataType: 'json',
                    delay: 250,
                    data: params => ({ query: params.term }),
                    processResults: data => ({
                        results: $.map(data, item => ({ id: item.id, text: item.name }))
                    })
                }
            });

            // Initialize Finished Good Select2
            $('#finishedGoodId').select2({
                theme: 'bootstrap-5',
                dropdownParent: $('#issueModal'),
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
        $('#issueForm')[0].reset();
        document.getElementById('issueDate').valueAsDate = new Date();
        $('#subcontractorId').val(null).trigger('change');
        $('#finishedGoodId').val(null).trigger('change');
        this._modal.show();
    },

    save: async function (e) {
        if (!$('#issueForm')[0].checkValidity()) {
            $('#issueForm')[0].reportValidity();
            return;
        }

        const payload = {
            issueDate: $('#issueDate').val(),
            referenceNumber: $('#referenceNumber').val().trim(),
            subcontractorId: parseInt($('#subcontractorId').val()),
            warehouseId: parseInt($('#warehouseId').val()),
            finishedGoodVariantId: parseInt($('#finishedGoodId').val()),
            targetQuantity: parseFloat($('#targetQuantity').val())
        };

        var $btn = $(e.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Processing FIFO Engine...');

        try {
            const res = await api.post('/api/inventory/issue-materials', payload);
            
            if (res && res.succeeded) {
                toastr.success(res.message);
                this._modal.hide();
                this._table.ajax.reload(null, false); // Reload the new grid
            } else if (res && res.messages) {
                toastr.error(res.messages[0]);
            }
        } catch (err) {
            const errorMessage = err.responseJSON?.messages?.[0] || err.message || "Failed to execute Material Issue. Check stock levels.";
            toastr.error(errorMessage);
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    }
};

$(document).ready(() => window.issueApp.init());