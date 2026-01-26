var unitApp = (function () {
    var table;
    var $modalEl = document.getElementById('offcanvasUnit');
    var modalInstance = null;

    var init = function () {
        if ($modalEl) modalInstance = new bootstrap.Offcanvas($modalEl);
        loadTable();
        $('#frmUnit').on('submit', function (e) { e.preventDefault(); save(); });
    };

    var loadTable = function () {
        if ($.fn.DataTable.isDataTable('#tblUnits')) return;
        table = $('#tblUnits').DataTable({
            "ajax": { "url": "/api/UnitOfMeasure", "type": "GET", "dataSrc": "data" },
            "columns": [
                { "data": "id", "width": "10%" },
                { "data": "name", "width": "40%", "className": "fw-bold" },
                { "data": "symbol", "width": "30%" },
                {
                    "data": "id", "width": "20%", "className": "text-end",
                    "render": function (data, type, row) {
                        var safeRow = JSON.stringify(row).replace(/"/g, '&quot;');
                        return `<button class="btn btn-sm btn-outline-secondary" onclick="unitApp.edit(${safeRow})"><i class="bi bi-pencil"></i> Edit</button>`;
                    }
                }
            ],
            "responsive": true
        });
    };

    var openCreatePanel = function () {
        $('#offcanvasUnitLabel').text("New Unit");
        $('#frmUnit')[0].reset();
        $('#hdnUnitId').val(0);
        modalInstance.show();
    };

    var edit = function (row) {
        $('#offcanvasUnitLabel').text("Edit Unit");
        $('#hdnUnitId').val(row.id);
        $('#txtUnitName').val(row.name);
        $('#txtUnitSymbol').val(row.symbol);
        modalInstance.show();
    };

    var save = async function () {
        var id = parseInt($('#hdnUnitId').val());
        var payload = { id: id, name: $('#txtUnitName').val(), symbol: $('#txtUnitSymbol').val() };

        var result;
        if (id === 0) result = await api.post("/api/UnitOfMeasure", payload);
        else result = await api.put(`/api/UnitOfMeasure/${id}`, payload);

        if (result && result.succeeded) {
            modalInstance.hide();
            table.ajax.reload();
        }
    };

    return { init: init, openCreatePanel: openCreatePanel, edit: edit };
})();