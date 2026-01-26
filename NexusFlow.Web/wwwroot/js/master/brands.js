var brandApp = (function () {
    var table;
    var $modalEl = document.getElementById('offcanvasBrand');
    var modalInstance = null;

    var init = function () {
        if ($modalEl) modalInstance = new bootstrap.Offcanvas($modalEl);
        loadTable();
        $('#frmBrand').on('submit', function (e) {
            e.preventDefault();
            saveBrand();
        });
    };

    var loadTable = function () {
        if ($.fn.DataTable.isDataTable('#tblBrands')) return;
        table = $('#tblBrands').DataTable({
            "ajax": { "url": "/api/Brand", "type": "GET", "dataSrc": "data" },
            "columns": [
                { "data": "id", "width": "10%" },
                { "data": "name", "width": "30%", "className": "fw-bold" },
                { "data": "description", "width": "40%" },
                {
                    "data": "id", "width": "20%", "className": "text-end",
                    "render": function (data, type, row) {
                        var safeRow = JSON.stringify(row).replace(/"/g, '&quot;');
                        return `<button class="btn btn-sm btn-outline-secondary" onclick="brandApp.edit(${safeRow})"><i class="bi bi-pencil"></i> Edit</button>`;
                    }
                }
            ],
            "responsive": true
        });
    };

    var openCreatePanel = function () {
        $('#offcanvasBrandLabel').text("New Brand");
        $('#frmBrand')[0].reset();
        $('#hdnBrandId').val(0);
        modalInstance.show();
    };

    var edit = function (row) {
        $('#offcanvasBrandLabel').text("Edit Brand");
        $('#hdnBrandId').val(row.id);
        $('#txtName').val(row.name);
        $('#txtDescription').val(row.description);
        modalInstance.show();
    };

    var saveBrand = async function () {
        var id = parseInt($('#hdnBrandId').val());
        var payload = {
            id: id,
            name: $('#txtName').val(),
            description: $('#txtDescription').val()
        };

        var result;
        if (id === 0) {
            result = await api.post("/api/Brand", payload);
        } else {
            result = await api.put(`/api/Brand/${id}`, payload);
        }

        if (result && result.succeeded) {
            modalInstance.hide();
            table.ajax.reload();
            // Toast is handled automatically by api.js
        }
    };

    return { init: init, openCreatePanel: openCreatePanel, edit: edit };
})();