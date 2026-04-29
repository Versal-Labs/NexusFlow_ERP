window.apApp = {
    _table: null,

    init: function () {
        this._loadLookups();
        this._initGrid();
    },

    _loadLookups: async function () {
        try {
            const res = await api.get('/api/supplier');
            const suppliers = res.data || res || [];
            let $sup = $('#filterSupplier').empty().append('<option value="">All Suppliers</option>');
            suppliers.forEach(s => $sup.append(`<option value="${s.id}">${s.name}</option>`));
            $sup.select2();
        } catch (e) { console.error("Lookup failed"); }
    },

    _initGrid: function () {
        const self = this;

        this._table = $('#apGrid').DataTable({
            ajax: async function (data, callback) {
                try {
                    let q = $('#filterSupplier').val() ? `?SupplierId=${$('#filterSupplier').val()}` : '';
                    const res = await api.get('/api/reporting/ap-aging' + q);
                    callback({ data: res.data || res || [] });
                } catch (e) { callback({ data: [] }); }
            },
            columns: [
                {
                    data: 'supplierName', className: 'fw-bold text-dark ps-3',
                    render: function (d, type, row) {
                        return `${d}<br><small class="text-muted"><i class="fa-solid fa-phone"></i> ${row.phone || 'N/A'}</small>`;
                    }
                },
                { data: 'totalOutstanding', className: 'text-end fw-bold text-danger', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'current', className: 'text-end', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'days1To30', className: 'text-end', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'days31To60', className: 'text-end', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'days61To90', className: 'text-end', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'over90Days', className: 'text-end fw-bold text-danger', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                {
                    data: null, className: 'text-center pe-3', orderable: false,
                    render: function (data, type, row) {
                        // Pass ID to the Statement view
                        return `
                            <a href="/Reporting/SupplierStatement?supplierId=${row.supplierId}" target="_blank" class="btn btn-sm btn-outline-dark shadow-sm" title="View Ledger">
                                <i class="fa-solid fa-file-lines me-1"></i> Ledger
                            </a>`;
                    }
                }
            ],
            order: [[1, 'desc']], // Order by Total Outstanding descending
            dom: '<"d-flex justify-content-between align-items-center mb-3"l>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 20,

            footerCallback: function () {
                let api = this.api();
                let intVal = i => typeof i === 'string' ? i.replace(/[\$,]/g, '') * 1 : typeof i === 'number' ? i : 0;

                let sums = [1, 2, 3, 4, 5, 6].map(col => api.column(col, { page: 'current' }).data().reduce((a, b) => intVal(a) + intVal(b), 0));

                $('#footTotal').html(sums[0].toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $('#footCurrent').html(sums[1].toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $('#foot30').html(sums[2].toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $('#foot60').html(sums[3].toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $('#foot90').html(sums[4].toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $('#footOver90').html(sums[5].toLocaleString(undefined, { minimumFractionDigits: 2 }));
            }
        });
    },

    reloadGrid: async function () {
        try {
            $('#apGrid_processing').show();

            let q = $('#filterSupplier').val() ? `?SupplierId=${$('#filterSupplier').val()}` : '';
            const res = await api.get('/api/reporting/ap-aging' + q);

            this._table.clear().rows.add(res.data || res || []).draw();

        } catch (e) {
            toastr.error("Failed to refresh AP Aging report.");
        } finally {
            $('#apGrid_processing').hide();
        }
    },

    export: function (format) {
        let q = $('#filterSupplier').val() ? `?SupplierId=${$('#filterSupplier').val()}` : '';
        const url = format === 'excel' ? '/api/reporting/ap-aging/export/excel' : '/api/reporting/ap-aging/export/pdf';
        window.open(url + q, '_blank');
    }
};

$(document).ready(() => apApp.init());