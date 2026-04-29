window.statementApp = {
    _table: null,

    init: async function () {
        // 1. Set Default Dates (Last 30 Days)
        const end = new Date();
        const start = new Date();
        start.setDate(end.getDate() - 30);

        $('#filterStartDate').val(start.toISOString().split('T')[0]);
        $('#filterEndDate').val(end.toISOString().split('T')[0]);

        // 2. Load Customers
        await this._loadLookups();

        // 3. Check for URL Parameter (If redirected from AR Aging)
        const urlParams = new URLSearchParams(window.location.search);
        const customerId = urlParams.get('customerId');
        if (customerId) {
            $('#filterCustomer').val(customerId).trigger('change');
        }

        this._initGrid();
    },

    _loadLookups: async function () {
        try {
            const custRes = await api.get('/api/customer');
            const customers = custRes.data || custRes || [];
            let $cust = $('#filterCustomer').empty().append('<option value="">-- Select Customer --</option>');
            customers.forEach(c => $cust.append(`<option value="${c.id}">${c.name}</option>`));
            $cust.select2();
        } catch (e) { console.error("Lookup failed"); }
    },

    _buildQueryString: function () {
        const start = $('#filterStartDate').val();
        const end = $('#filterEndDate').val();
        const cust = $('#filterCustomer').val();

        if (!cust || !start || !end) return '';
        return `?CustomerId=${cust}&StartDate=${start}&EndDate=${end}`;
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
                    const res = await api.get('/api/reporting/customer-statement' + query);
                    const records = res.data || res || [];

                    // Enable export buttons if data exists
                    if (records.length > 0) {
                        $('#btnExcel, #btnPdf').prop('disabled', false);
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
                        if (d === 'Invoice') return `<span class="text-primary"><i class="fa-solid fa-file-invoice fa-fw"></i> ${d}</span>`;
                        if (d === 'Payment Receipt') return `<span class="text-success"><i class="fa-solid fa-money-bill fa-fw"></i> ${d}</span>`;
                        return `<span class="text-warning text-dark"><i class="fa-solid fa-rotate-left fa-fw"></i> ${d}</span>`;
                    }
                },
                { data: 'referenceNo', className: 'font-monospace' },
                { data: 'description', className: 'text-muted' },
                { data: 'debit', className: 'text-end', render: d => d > 0 ? parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) : '-' },
                { data: 'credit', className: 'text-end text-success', render: d => d > 0 ? parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) : '-' },
                { data: 'balance', className: 'text-end fw-bold text-dark pe-3', render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) }
            ],
            order: [], // Disable sorting so it stays in chronological order computed by the backend!
            ordering: false,
            paging: false, // Statements shouldn't be paginated usually
            info: false,
            searching: false // Disable generic search to enforce using our exact filters
        });
    },

    reloadGrid: async function () {
        if (!$('#filterCustomer').val()) {
            toastr.warning("Please select a Customer first.");
            return;
        }

        try {
            $('#statementGrid_processing').show(); // Show native loading spinner

            const query = this._buildQueryString();
            const res = await api.get('/api/reporting/customer-statement' + query);
            const records = res.data || res || [];

            // Toggle export buttons based on data
            if (records.length > 0) {
                $('#btnExcel, #btnPdf').prop('disabled', false);
            } else {
                $('#btnExcel, #btnPdf').prop('disabled', true);
            }

            // Clear and draw new data
            this._table.clear().rows.add(records).draw();

        } catch (e) {
            toastr.error("Failed to refresh statement data.");
        } finally {
            $('#statementGrid_processing').hide(); // Hide spinner
        }
    },

    export: function (format) {
        const query = this._buildQueryString();
        if (!query) return;

        const url = format === 'excel' ? '/api/reporting/customer-statement/export/excel' : '/api/reporting/customer-statement/export/pdf';
        window.open(url + query, '_blank');
    }
};

$(document).ready(() => statementApp.init());