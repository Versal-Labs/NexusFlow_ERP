window.supStatementApp = {
    _table: null,

    init: async function () {
        // 1. Set Default Dates (Last 30 Days)
        const end = new Date();
        const start = new Date();
        start.setDate(end.getDate() - 30);

        $('#filterStartDate').val(start.toISOString().split('T')[0]);
        $('#filterEndDate').val(end.toISOString().split('T')[0]);

        // 2. Load Suppliers
        await this._loadLookups();

        // 3. Check for URL Parameter (If redirected from AP Aging)
        const urlParams = new URLSearchParams(window.location.search);
        const supplierId = urlParams.get('supplierId');
        if (supplierId) {
            $('#filterSupplier').val(supplierId).trigger('change');
        }

        this._initGrid();
    },

    _loadLookups: async function () {
        try {
            const res = await api.get('/api/supplier');
            const suppliers = res.data || res || [];
            let $sup = $('#filterSupplier').empty().append('<option value="">-- Select Supplier --</option>');
            suppliers.forEach(s => $sup.append(`<option value="${s.id}">${s.name}</option>`));
            $sup.select2();
        } catch (e) { console.error("Lookup failed"); }
    },

    _buildQueryString: function () {
        const start = $('#filterStartDate').val();
        const end = $('#filterEndDate').val();
        const sup = $('#filterSupplier').val();

        if (!sup || !start || !end) return '';
        return `?SupplierId=${sup}&StartDate=${start}&EndDate=${end}`;
    },

    _initGrid: function () {
        const self = this;

        this._table = $('#statementGrid').DataTable({
            ajax: async function (data, callback) {
                const query = self._buildQueryString();
                if (!query) {
                    callback({ data: [] });
                    return;
                }

                try {
                    const res = await api.get('/api/reporting/supplier-statement' + query);
                    const records = res.data || res || [];

                    // Enable export buttons if data exists
                    if (records.length > 0) {
                        $('#btnExcel, #btnPdf').prop('disabled', false);
                    } else {
                        $('#btnExcel, #btnPdf').prop('disabled', true);
                    }

                    callback({ data: records });
                } catch (e) {
                    callback({ data: [] });
                    toastr.error(e.responseJSON?.message || "Failed to load statement.");
                }
            },
            columns: [
                { data: 'date', className: 'ps-3 font-monospace' },
                {
                    data: 'transactionType',
                    render: function (d) {
                        if (d === 'Opening Balance') return `<span class="fw-bold text-muted">${d}</span>`;
                        if (d === 'AP Bill') return `<span class="text-danger"><i class="fa-solid fa-file-invoice-dollar fa-fw"></i> ${d}</span>`;
                        if (d === 'Payment Made') return `<span class="text-success"><i class="fa-solid fa-money-check-dollar fa-fw"></i> ${d}</span>`;
                        return `<span class="text-warning text-dark"><i class="fa-solid fa-rotate-left fa-fw"></i> ${d}</span>`;
                    }
                },
                { data: 'referenceNo', className: 'font-monospace' },
                { data: 'description', className: 'text-muted' },
                {
                    data: 'credit', // BILL (Increases our debt)
                    className: 'text-end',
                    render: d => d > 0 ? parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) : '-'
                },
                {
                    data: 'debit', // PAYMENT (Decreases our debt)
                    className: 'text-end text-success',
                    render: d => d > 0 ? parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) : '-'
                },
                {
                    data: 'balance',
                    className: 'text-end fw-bold text-dark pe-3',
                    render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 })
                }
            ],
            order: [], // Disable sorting! Enforce chronological order from the backend.
            ordering: false,
            paging: false, // Statements are usually printed as one continuous document
            info: false,
            searching: false // Disable generic search
        });
    },

    reloadGrid: async function () {
        if (!$('#filterSupplier').val()) {
            toastr.warning("Please select a Supplier first.");
            return;
        }

        try {
            $('#statementGrid_processing').show();

            const query = this._buildQueryString();
            const res = await api.get('/api/reporting/supplier-statement' + query);
            const records = res.data || res || [];

            if (records.length > 0) {
                $('#btnExcel, #btnPdf').prop('disabled', false);
            } else {
                $('#btnExcel, #btnPdf').prop('disabled', true);
            }

            this._table.clear().rows.add(records).draw();

        } catch (e) {
            toastr.error("Failed to refresh statement data.");
        } finally {
            $('#statementGrid_processing').hide();
        }
    },

    export: function (format) {
        const query = this._buildQueryString();
        if (!query) return;

        const url = format === 'excel' ? '/api/reporting/supplier-statement/export/excel' : '/api/reporting/supplier-statement/export/pdf';
        window.open(url + query, '_blank');
    }
};

$(document).ready(() => supStatementApp.init());