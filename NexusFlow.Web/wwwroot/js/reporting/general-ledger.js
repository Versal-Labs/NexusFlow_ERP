window.ledgerApp = {
    _table: null,

    init: async function () {
        // Set Default Dates (Current Month)
        const date = new Date();
        const firstDay = new Date(date.getFullYear(), date.getMonth(), 1).toISOString().split('T')[0];
        const lastDay = new Date(date.getFullYear(), date.getMonth() + 1, 0).toISOString().split('T')[0];

        $('#filterStartDate').val(firstDay);
        $('#filterEndDate').val(lastDay);

        await this._loadLookups();
        this._initGrid();
    },

    _loadLookups: async function () {
        try {
            const res = await api.get('/api/finance/accounts'); // Ensure you have this endpoint
            const accounts = res.data || res || [];
            let $acc = $('#filterAccount').empty().append('<option value="">-- Select GL Account --</option>');

            // Grouping by Type makes it easy for the accountant to find expenses vs banks
            let grouped = accounts.reduce((r, a) => {
                r[a.type] = [...r[a.type] || [], a];
                return r;
            }, {});

            for (const [type, accs] of Object.entries(grouped)) {
                let optgroup = $(`<optgroup label="${type}"></optgroup>`);
                accs.forEach(a => optgroup.append(`<option value="${a.id}">[${a.code}] ${a.name}</option>`));
                $acc.append(optgroup);
            }
            $acc.select2();
        } catch (e) { console.error("Lookup failed"); }
    },

    _buildQueryString: function () {
        let q = [];
        const start = $('#filterStartDate').val();
        const end = $('#filterEndDate').val();
        const acc = $('#filterAccount').val();
        const mod = $('#filterModule').val();

        if (acc) q.push(`AccountId=${acc}`);
        if (start) q.push(`StartDate=${start}`);
        if (end) q.push(`EndDate=${end}`);
        if (mod) q.push(`Module=${mod}`);

        return q.length > 0 ? '?' + q.join('&') : '';
    },

    _initGrid: function () {
        const self = this;

        this._table = $('#ledgerGrid').DataTable({
            data: [], // Starts empty until an account is selected
            columns: [
                { data: 'date', className: 'ps-3 font-monospace' },
                { data: 'journalNo', className: 'font-monospace text-primary fw-bold' },
                {
                    data: 'module',
                    render: function (d) {
                        if (d === 'System') return `<span class="badge bg-dark bg-opacity-10 text-dark border border-dark">Opening Balance</span>`;
                        return `<span class="badge bg-secondary">${d}</span>`;
                    }
                },
                { data: 'description', className: 'text-muted' },
                { data: 'referenceNo', className: 'font-monospace' },
                { data: 'debit', className: 'text-end', render: d => d > 0 ? parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) : '-' },
                { data: 'credit', className: 'text-end text-danger', render: d => d > 0 ? parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) : '-' },
                { data: 'balance', className: 'text-end fw-bold text-dark pe-3', render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) }
            ],
            order: [], // Disable internal sorting to preserve chronological running balance
            ordering: false,
            paging: false,
            info: false,
            searching: false
        });
    },

    reloadGrid: async function () {
        if (!$('#filterAccount').val()) {
            toastr.warning("Please select a Chart of Account first.");
            return;
        }

        try {
            $('#ledgerGrid_processing').show();

            const query = this._buildQueryString();
            const res = await api.get('/api/reporting/general-ledger' + query);
            const records = res.data || res || [];

            if (records.length > 0) {
                $('#btnExcel, #btnPdf').prop('disabled', false);
            } else {
                $('#btnExcel, #btnPdf').prop('disabled', true);
            }

            this._table.clear().rows.add(records).draw();

        } catch (e) {
            toastr.error(e.responseJSON?.message || "Failed to load ledger data.");
        } finally {
            $('#ledgerGrid_processing').hide();
        }
    },

    export: function (format) {
        const query = this._buildQueryString();
        if (!query) return;

        const url = format === 'excel' ? '/api/reporting/general-ledger/export/excel' : '/api/reporting/general-ledger/export/pdf';
        window.open(url + query, '_blank');
    }
};

$(document).ready(() => ledgerApp.init());