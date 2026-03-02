var categoryApp = (function () {
    var table;
    var $modalEl = document.getElementById('offcanvasCategory');
    var modalInstance = null;

    var init = function () {
        if ($modalEl) modalInstance = new bootstrap.Offcanvas($modalEl);
        loadTable();
        $('#frmCategory').on('submit', function (e) { e.preventDefault(); save(); });
    };

    var loadTable = function () {
        if ($.fn.DataTable.isDataTable('#tblCategories')) return;
        table = $('#tblCategories').DataTable({
            "ajax": { "url": "/api/Category", "type": "GET", "dataSrc": "data" },
            "columns": [
                { "data": "id", "width": "10%" },
                { "data": "name", "width": "40%", "className": "fw-semibold" },
                { "data": "code", "width": "30%", "className": "font-monospace" },
                {
                    "data": "id", "width": "20%", "className": "text-end",
                    "render": function (data, type, row) {
                        var safeRow = JSON.stringify(row).replace(/"/g, '&quot;');
                        return `<button class="btn btn-sm btn-outline-secondary" onclick="categoryApp.edit(${safeRow})"><i class="bi bi-pencil"></i> Edit</button>`;
                    }
                }
            ],
            "responsive": true
        });
    };

    var openCreatePanel = function () {
        $('#offcanvasCategoryLabel').text("New Category");
        $('#frmCategory')[0].reset();
        $('#hdnCategoryId').val(0);
        modalInstance.show();
    };

    var edit = function (row) {
        $('#offcanvasCategoryLabel').text("Edit Category");
        $('#hdnCategoryId').val(row.id);
        $('#txtCatName').val(row.name);
        $('#txtCatCode').val(row.code);
        modalInstance.show();
    };

    var save = async function () {
        var id = parseInt($('#hdnCategoryId').val());
        var payload = { id: id, name: $('#txtCatName').val(), code: $('#txtCatCode').val() };

        var result;
        if (id === 0) result = await api.post("/api/Category", payload);
        else result = await api.put(`/api/Category/${id}`, payload);

        if (result && result.succeeded) {
            modalInstance.hide();
            table.ajax.reload();
        }
    };

    return { init: init, openCreatePanel: openCreatePanel, edit: edit };
})();