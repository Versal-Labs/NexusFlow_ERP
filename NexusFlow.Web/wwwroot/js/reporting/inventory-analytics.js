window.invApp = {
    _table: null,

    init: async function () {
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
            const [whRes, prodRes] = await Promise.all([
                api.get('/api/masterdata/warehouses'),
                api.get('/api/masterdata/variants/search?query=') // Assuming you have a search endpoint for variants
            ]);

            const warehouses = Array.isArray(whRes) ? whRes : (whRes.data || []);
            const products = Array.isArray(prodRes) ? prodRes : (prodRes.data || []);

            let $wh = $('#filterWarehouse').empty().append('<option value="">All Warehouses</option>');
            warehouses.forEach(w => $wh.append(`<option value="${w.id}">${w.name}</option>`));
            $wh.select2();

            let $prod = $('#filterProduct').empty().append('<option value="">All Products</option>');
            products.forEach(p => $prod.append(`<option value="${p.id}">[${p.sku}] ${p.name}</option>`));
            $prod.select2();

        } catch (e) { console.error("Lookup failed"); }
    },

    _buildQueryString: function () {
        let q = [];
        const start = $('#filterStartDate').val();
        const end = $('#filterEndDate').val();
        const type = $('#filterType').val();
        const wh = $('#filterWarehouse').val();
        const prod = $('#filterProduct').val();

        if (start) q.push(`StartDate=${start}`);
        if (end) q.push(`EndDate=${end}`);
        if (type) q.push(`TransactionType=${type}`);
        if (wh) q.push(`WarehouseId=${wh}`);
        if (prod) q.push(`ProductVariantId=${prod}`);

        return q.length > 0 ? '?' + q.join('&') : '';
    },

    _initGrid: function () {
        const self = this;

        this._table = $('#invGrid').DataTable({
            ajax: async function (data, callback) {
                try {
                    const res = await api.get('/api/reporting/inventory-analytics' + self._buildQueryString());
                    callback({ data: res.data || res || [] });
                } catch (e) { callback({ data: [] }); }
            },
            columns: [
                { data: 'date', className: 'ps-3 font-monospace text-muted' },
                {
                    data: 'transactionType',
                    render: function (d) {
                        if (d === 'Receipt') return `<span class="badge bg-success bg-opacity-10 text-success border border-success">GRN</span>`;
                        if (d === 'Dispatch') return `<span class="badge bg-primary bg-opacity-10 text-primary border border-primary">Sale</span>`;
                        if (d === 'Adjustment') return `<span class="badge bg-warning bg-opacity-10 text-warning border border-warning">Adj</span>`;
                        if (d === 'StockTake') return `<span class="badge bg-dark bg-opacity-10 text-dark border border-dark">Audit</span>`;
                        return `<span class="badge bg-secondary bg-opacity-10 text-secondary border border-secondary">${d}</span>`;
                    }
                },
                { data: 'referenceNo', className: 'font-monospace fw-bold' },
                { data: 'warehouse' },
                { data: 'product', render: (d, t, r) => `${d} <br><small class="text-muted font-monospace">${r.sku}</small>` },
                {
                    data: 'quantity',
                    className: 'text-end fw-bold',
                    render: function (d) {
                        let val = parseFloat(d);
                        return val > 0 ? `<span class="text-success">+${val.toFixed(2)}</span>` : `<span class="text-danger">${val.toFixed(2)}</span>`;
                    }
                },
                { data: 'unitCost', className: 'text-end', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'totalValue', className: 'text-end pe-3 font-monospace', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) }
            ],
            order: [[0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"l>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 50,

            footerCallback: function () {
                let api = this.api();
                let intVal = i => typeof i === 'string' ? i.replace(/[\$,]/g, '') * 1 : typeof i === 'number' ? i : 0;

                let netQty = api.column(5, { page: 'current' }).data().reduce((a, b) => intVal(a) + intVal(b), 0);
                let netValue = api.column(7, { page: 'current' }).data().reduce((a, b) => intVal(a) + intVal(b), 0);

                $('#footQty').html(netQty > 0 ? `+${netQty.toFixed(2)}` : netQty.toFixed(2));
                $('#footQty').removeClass('text-success text-danger').addClass(netQty > 0 ? 'text-success' : 'text-danger');

                $('#footValue').html(netValue.toLocaleString(undefined, { minimumFractionDigits: 2 }));
            }
        });
    },

    reloadGrid: async function () {
        try {
            $('#invGrid_processing').show();
            const res = await api.get('/api/reporting/inventory-analytics' + this._buildQueryString());
            this._table.clear().rows.add(res.data || res || []).draw();
        } catch (e) { toastr.error("Failed to refresh report."); }
        finally { $('#invGrid_processing').hide(); }
    },

    export: function (format) {
        const url = format === 'excel' ? '/api/reporting/inventory-analytics/export/excel' : '/api/reporting/inventory-analytics/export/pdf';
        window.open(url + this._buildQueryString(), '_blank');
    }
};

$(document).ready(() => invApp.init());