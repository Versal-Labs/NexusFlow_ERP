window.salesReportApp = {
    _table: null,

    init: function () {
        this._loadLookups();
        this._initGrid();

        // Default to current month
        const date = new Date();
        const firstDay = new Date(date.getFullYear(), date.getMonth(), 1).toISOString().split('T')[0];
        const lastDay = new Date(date.getFullYear(), date.getMonth() + 1, 0).toISOString().split('T')[0];

        $('#filterStartDate').val(firstDay);
        $('#filterEndDate').val(lastDay);
    },

    _loadLookups: async function () {
        try {
            const [custRes, empRes] = await Promise.all([
                api.get('/api/customer'),
                api.get('/api/employee') // Assuming this endpoint exists based on your setup
            ]);

            const customers = custRes.data || custRes || [];
            const employees = empRes.data || empRes || [];

            let $cust = $('#filterCustomer').empty().append('<option value="">All Customers</option>');
            customers.forEach(c => $cust.append(`<option value="${c.id}">${c.name}</option>`));
            $cust.select2();

            let $rep = $('#filterSalesRep').empty().append('<option value="">All Representatives</option>');
            // Filter only employees who are marked as Sales Reps
            employees.filter(e => e.isSalesRep).forEach(e => {
                $rep.append(`<option value="${e.id}">${e.firstName} ${e.lastName}</option>`);
            });
            $rep.select2();

        } catch (e) {
            console.error("Failed to load lookups", e);
        }
    },

    _buildQueryString: function () {
        let q = [];
        const start = $('#filterStartDate').val();
        const end = $('#filterEndDate').val();
        const cust = $('#filterCustomer').val();
        const rep = $('#filterSalesRep').val();

        if (start) q.push(`StartDate=${start}`);
        if (end) q.push(`EndDate=${end}`);
        if (cust) q.push(`CustomerId=${cust}`);
        if (rep) q.push(`SalesRepId=${rep}`);

        return q.length > 0 ? '?' + q.join('&') : '';
    },

    _initGrid: function () {
        const self = this;

        this._table = $('#salesGrid').DataTable({
            ajax: async function (data, callback, settings) {
                try {
                    const url = '/api/reporting/sales-register' + self._buildQueryString();
                    const res = await api.get(url);
                    callback({ data: res.data || res || [] });
                } catch (e) {
                    callback({ data: [] });
                    toastr.error("Failed to load report data.");
                }
            },
            columns: [
                { data: 'invoiceNo', className: 'fw-bold text-primary font-monospace ps-3' },
                { data: 'date' },
                { data: 'customer', className: 'fw-bold text-dark' },
                { data: 'salesRep' },
                { data: 'subTotal', className: 'text-end', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'vat', className: 'text-end text-danger', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'grandTotal', className: 'text-end fw-bold text-dark', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                {
                    data: 'status', className: 'text-center pe-3',
                    render: function (d) {
                        if (d === 'Paid') return '<span class="badge bg-success">Paid</span>';
                        if (d === 'Partial') return '<span class="badge bg-info text-dark">Partial</span>';
                        return '<span class="badge bg-warning text-dark">Unpaid</span>';
                    }
                }
            ],
            order: [[1, 'desc'], [0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"l>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 50,

            // TIER-1 FEATURE: Dynamic Footers to calculate totals of filtered data
            footerCallback: function (row, data, start, end, display) {
                let api = this.api();

                // Helper to sum a column
                let intVal = function (i) {
                    return typeof i === 'string' ? i.replace(/[\$,]/g, '') * 1 : typeof i === 'number' ? i : 0;
                };

                let totalSub = api.column(4, { page: 'current' }).data().reduce((a, b) => intVal(a) + intVal(b), 0);
                let totalVat = api.column(5, { page: 'current' }).data().reduce((a, b) => intVal(a) + intVal(b), 0);
                let totalGrand = api.column(6, { page: 'current' }).data().reduce((a, b) => intVal(a) + intVal(b), 0);

                $('#footSubTotal').html(totalSub.toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $('#footVat').html(totalVat.toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $('#footGrandTotal').html(totalGrand.toLocaleString(undefined, { minimumFractionDigits: 2 }));
            }
        });
    },

    reloadGrid: async function () {
        try {
            $('#salesGrid_processing').show();

            const res = await api.get('/api/reporting/sales-register' + this._buildQueryString());

            this._table.clear().rows.add(res.data || res || []).draw();

        } catch (e) {
            toastr.error("Failed to refresh report.");
        } finally {
            $('#salesGrid_processing').hide();
        }
    },

    resetFilters: function () {
        $('#filterStartDate, #filterEndDate').val('');
        $('#filterCustomer, #filterSalesRep').val('').trigger('change');
        this.reloadGrid();
    },

    // TIER-1 FEATURE: Syncfusion Document Exports!
    export: function (format) {
        // Construct the URL with current filters
        const baseUrl = format === 'excel' ? '/api/reporting/sales-register/export/excel' : '/api/reporting/sales-register/export/pdf';
        const fullUrl = baseUrl + this._buildQueryString();

        // Standard browser download trigger. 
        // This is safe because the endpoints are secured by cookies if you are logged in.
        window.open(fullUrl, '_blank');
    }
};

$(document).ready(() => salesReportApp.init());