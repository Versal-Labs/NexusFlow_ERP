window.valApp = {
    _table: null,

    init: function () {
        this._initFilters();
        this._initGrid();

        // Trigger filter on Enter key press in search box
        $('#filterSearch').on('keypress', (e) => {
            if (e.which === 13) this.reloadGrid();
        });
    },

    _initFilters: async function () {
        try {
            const [whRes, catRes] = await Promise.all([
                api.get('/api/masterdata/warehouses'),
                api.get('/api/Category')
            ]);

            let $wh = $('#filterWarehouse');
            (whRes.data || whRes || []).forEach(w => $wh.append(`<option value="${w.id}">${w.name}</option>`));

            let $cat = $('#filterCategory');
            (catRes.data || catRes || []).forEach(c => $cat.append(`<option value="${c.id}">${c.name}</option>`));
        } catch (e) { console.error("Filter load failed", e); }
    },

    reloadGrid: function () {
        this._table.ajax.reload();
    },

    _initGrid: function () {
        this._table = $('#valuationGrid').DataTable({
            // DataTables Export Buttons Configuration
            dom: '<"d-flex justify-content-between align-items-center mb-3"<"dt-buttons-container"B>f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            buttons: [
                { extend: 'excelHtml5', text: '<i class="fa-solid fa-file-excel me-1"></i> Export Excel', className: 'btn btn-sm btn-success shadow-sm me-2' },
                { extend: 'pdfHtml5', text: '<i class="fa-solid fa-file-pdf me-1"></i> Export PDF', className: 'btn btn-sm btn-danger shadow-sm' }
            ],
            pageLength: 25,
            ajax: async function (data, callback) {
                try {
                    let wId = $('#filterWarehouse').val() || '';
                    let cId = $('#filterCategory').val() || '';
                    let search = $('#filterSearch').val() || '';
                    
                    const res = await api.get(`/api/inventory/valuation?warehouseId=${wId}&categoryId=${cId}&search=${search}`);
                    const rows = res.data || res || [];
                    
                    // Update Summary Cards Dynamically!
                    let totalVal = 0;
                    let totalUnits = 0;
                    rows.forEach(r => {
                        totalVal += r.totalFifoValue;
                        totalUnits += r.totalQuantity;
                    });
                    
                    $('#lblTotalValue').text('$' + totalVal.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }));
                    $('#lblTotalUnits').text(totalUnits.toLocaleString(undefined, { minimumFractionDigits: 2 }));

                    callback({ data: rows });
                } catch (e) { 
                    toastr.error("Failed to fetch valuation data.");
                    callback({ data: [] }); 
                }
            },
            columns: [
                { data: 'warehouseName', className: 'fw-bold text-secondary' },
                { data: 'categoryName' },
                { data: 'sku', className: 'font-monospace text-primary fw-bold' },
                { data: 'productName', className: 'text-dark fw-bold' },
                { data: 'totalQuantity', className: 'text-end fw-bold', render: d => d.toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'blendedUnitCost', className: 'text-end text-muted', render: d => d.toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'totalFifoValue', className: 'text-end fw-bold text-success pe-3', render: d => d.toLocaleString(undefined, { minimumFractionDigits: 2 }) }
            ],
            order: [[0, 'asc'], [1, 'asc'], [3, 'asc']]
        });
    }
};

$(document).ready(() => valApp.init());