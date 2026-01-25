var brandApp = (function () {
    var table;

    var init = function () {
        loadTable();
        registerEvents();
    };

    var loadTable = function () {
        if ($.fn.DataTable.isDataTable('#tblBrands')) {
            $('#tblBrands').DataTable().destroy();
        }

        table = $('#tblBrands').DataTable({
            "ajax": {
                "url": "/api/Brand",
                "type": "GET",
                "dataSrc": "data"
            },
            "columns": [
                { "data": "id", "width": "10%" },
                { "data": "name", "width": "30%", "className": "fw-bold" },
                { "data": "description", "width": "40%" },
                {
                    "data": "id",
                    "width": "20%",
                    "className": "text-end",
                    "render": function (data, type, row) {
                        return `
                            <button class="btn btn-sm btn-outline-secondary me-1" onclick="brandApp.edit(${data}, '${row.name}', '${row.description || ''}')">
                                Edit
                            </button>
                        `;
                    }
                }
            ],
            "language": {
                "emptyTable": "No brands defined."
            }
        });
    };

    var registerEvents = function () {
        $('#frmBrand').on('submit', function (e) {
            e.preventDefault();
            saveBrand();
        });
    };

    var openCreatePanel = function () {
        $('#offcanvasBrandLabel').text("New Brand");
        $('#hdnBrandId').val(0);
        $('#txtName').val('');
        $('#txtDescription').val('');

        var offcanvas = new bootstrap.Offcanvas(document.getElementById('offcanvasBrand'));
        offcanvas.show();
    };

    var edit = function (id, name, description) {
        $('#offcanvasBrandLabel').text("Edit Brand");
        $('#hdnBrandId').val(id);
        $('#txtName').val(name);
        $('#txtDescription').val(description);

        var offcanvas = new bootstrap.Offcanvas(document.getElementById('offcanvasBrand'));
        offcanvas.show();
    };

    var saveBrand = function () {
        var id = parseInt($('#hdnBrandId').val());
        var isNew = id === 0;

        var payload = {
            id: id,
            name: $('#txtName').val(),
            description: $('#txtDescription').val()
        };

        var url = isNew ? "/api/Brand" : `/api/Brand/${id}`;
        var method = isNew ? "POST" : "PUT";

        $.ajax({
            url: url,
            type: method,
            contentType: "application/json",
            data: JSON.stringify(payload),
            success: function (response) {
                if (response.succeeded) {
                    // Close Offcanvas
                    var offcanvasEl = document.getElementById('offcanvasBrand');
                    var offcanvas = bootstrap.Offcanvas.getInstance(offcanvasEl);
                    offcanvas.hide();

                    // Refresh Table
                    table.ajax.reload();

                    // Show success (Toast ideally, alert for now)
                    // alert("Brand saved successfully."); 
                } else {
                    alert("Error: " + response.messages[0]);
                }
            },
            error: function (err) {
                console.error(err);
                alert("An unexpected error occurred.");
            }
        });
    };

    return {
        init: init,
        openCreatePanel: openCreatePanel,
        edit: edit
    };
})();

$(document).ready(function () {
    brandApp.init();
});