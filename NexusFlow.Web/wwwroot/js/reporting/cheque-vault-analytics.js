window.vaultApp = {
    _table: null,

    init: async function () {
        // Default to looking ahead 30 days for PDCs
        const start = new Date();
        const end = new Date();
        end.setDate(start.getDate() + 30);

        $('#filterStartDate').val(start.toISOString().split('T')[0]);
        $('#filterEndDate').val(end.toISOString().split('T')[0]);

        await this._loadLookups();
        this._initGrid();
    },

    _loadLookups: async function () {
        try {
            const [custRes, bankRes] = await Promise.all([
                api.get('/api/customer'),
                api.get('/api/banks')
            ]);

            const customers = custRes.data || custRes || [];
            const banks = bankRes.data || bankRes || [];

            let $cust = $('#filterCustomer').empty().append('<option value="">All Customers</option>');
            customers.forEach(c => $cust.append(`<option value="${c.id}">${c.name}</option>`));
            $cust.select2();

            let $bank = $('#filterBank').empty().append('<option value="">All Banks</option>');
            banks.forEach(b => $bank.append(`<option value="${b.id}">[${b.bankCode}] ${b.name}</option>`));
            $bank.select2();

        } catch (e) { console.error("Lookup failed"); }
    },

    _buildQueryString: function () {
        let q = [];
        const start = $('#filterStartDate').val();
        const end = $('#filterEndDate').val();
        const status = $('#filterStatus').val();
        const bank = $('#filterBank').val();
        const cust = $('#filterCustomer').val();

        if (start) q.push(`StartDate=${start}`);
        if (end) q.push(`EndDate=${end}`);
        if (status) q.push(`Status=${status}`);
        if (bank) q.push(`BankId=${bank}`);
        if (cust) q.push(`CustomerId=${cust}`);

        return q.length > 0 ? '?' + q.join('&') : '';
    },

    _initGrid: function () {
        const self = this;

        this._table = $('#vaultGrid').DataTable({
            ajax: async function (data, callback) {
                try {
                    const res = await api.get('/api/reporting/cheque-vault' + self._buildQueryString());
                    callback({ data: res.data || res || [] });
                } catch (e) { callback({ data: [] }); }
            },
            columns: [
                { data: 'chequeNumber', className: 'ps-3 font-monospace fw-bold text-dark' },
                { data: 'pdcDate', className: 'font-monospace' },
                { data: 'customer', className: 'fw-bold text-primary' },
                { data: 'bank', render: (d, t, r) => `${d}<br><small class="text-muted">${r.branch}</small>` },
                { data: 'amount', className: 'text-end fw-bold text-dark', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                {
                    data: 'status', className: 'text-center',
                    render: function (d) {
                        if (d === 'InSafe') return `<span class="badge bg-secondary">In Safe</span>`;
                        if (d === 'Deposited') return `<span class="badge bg-info text-dark">Deposited</span>`;
                        if (d === 'Cleared') return `<span class="badge bg-success">Cleared</span>`;
                        if (d === 'Bounced') return `<span class="badge bg-danger">Bounced</span>`;
                        return `<span class="badge bg-dark">${d}</span>`;
                    }
                },
                {
                    data: 'daysToMaturity', className: 'text-center pe-3',
                    render: function (d, type, row) {
                        if (row.status === 'Cleared' || row.status === 'Returned') return '-';
                        if (row.status === 'Bounced') return `<span class="text-danger fw-bold"><i class="fa-solid fa-triangle-exclamation"></i> ACTION REQ</span>`;

                        if (d > 0) return `<span class="text-muted">In ${d} days</span>`;
                        if (d === 0) return `<span class="text-success fw-bold"><i class="fa-solid fa-check"></i> Realizable Today</span>`;
                        return `<span class="text-warning fw-bold text-dark"><i class="fa-solid fa-clock"></i> Overdue ${Math.abs(d)}d</span>`;
                    }
                }
            ],
            order: [[1, 'asc']], // Order chronologically by PDC date
            dom: '<"d-flex justify-content-between align-items-center mb-3"l>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 50,

            footerCallback: function () {
                let api = this.api();
                let intVal = i => typeof i === 'string' ? i.replace(/[\$,]/g, '') * 1 : typeof i === 'number' ? i : 0;
                let totalAmt = api.column(4, { page: 'current' }).data().reduce((a, b) => intVal(a) + intVal(b), 0);
                $('#footAmount').html(totalAmt.toLocaleString(undefined, { minimumFractionDigits: 2 }));
            }
        });
    },

    reloadGrid: async function () {
        try {
            // 1. Show the native DataTables loading spinner
            $('#vaultGrid_processing').show();

            // 2. Fetch the new data with the updated filters
            const res = await api.get('/api/reporting/cheque-vault' + this._buildQueryString());
            const records = res.data || res || [];

            // 3. Clear old data and draw new data
            this._table.clear();
            this._table.rows.add(records);
            this._table.draw();

        } catch (e) {
            toastr.error("Failed to refresh report data.");
            console.error(e);
        } finally {
            // 4. Hide the spinner
            $('#vaultGrid_processing').hide();
        }
    },

    export: function (format) {
        const url = format === 'excel' ? '/api/reporting/cheque-vault/export/excel' : '/api/reporting/cheque-vault/export/pdf';
        window.open(url + this._buildQueryString(), '_blank');
    }
};

$(document).ready(() => vaultApp.init());