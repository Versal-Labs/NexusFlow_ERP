var categoryApp = (function () {
    var table;
    var $modalEl = document.getElementById('offcanvasCategory');
    var modalInstance = null;
    var API_URL = "/api/Category";

    var init = function() {
        if ($modalEl) modalInstance = new bootstrap.Offcanvas($modalEl);
        
        loadTable();
        loadLookups();

        // ARCHITECTURAL FIX 1: Use .off('submit') to prevent event stacking
        $('#frmCategory').off('submit').on('submit', function(e) { 
            e.preventDefault(); 
            e.stopPropagation();
            
            var form = $(this)[0];
            if (!form.checkValidity()) {
                $(this).addClass('was-validated');
                return;
            }
            
            save(); 
        });

        $('button[data-bs-toggle="tab"]').on('shown.bs.tab', function(e) {
            if (e.target.id === 'categories-tab' && table) {
                table.columns.adjust().responsive.recalc();
            }
        });
    };

    var loadTable = function () {
        if ($.fn.DataTable.isDataTable('#tblCategories')) return;
        table = $('#tblCategories').DataTable({
            "ajax": { 
                "url": API_URL, 
                "type": "GET", 
                "dataSrc": function(json) { return json.data || json || []; },
                "headers": { "Authorization": "Bearer " + localStorage.getItem("jwtToken") }
            },
            "columns": [
                { "data": "code", "className": "font-monospace fw-bold text-primary ps-4" },
                { "data": "name", "className": "fw-bold text-dark" },
                { "data": "parentCategoryName", "defaultContent": '<span class="text-muted fst-italic">Root</span>' },
                { "data": "salesAccountName", "defaultContent": '<span class="badge bg-danger bg-opacity-10 text-danger">Missing</span>' },
                { "data": "inventoryAccountName", "defaultContent": "-" },
                { "data": "cogsAccountName", "defaultContent": "-" },
                {
                    "data": null, 
                    "className": "text-end pe-4",
                    "orderable": false,
                    "render": function (data, type, row) {
                        var safeRow = JSON.stringify(row).replace(/"/g, '&quot;');
                        return `<button class="btn btn-sm btn-outline-secondary" onclick="categoryApp.edit(${safeRow})"><i class="bi bi-pencil"></i> Edit</button>`;
                    }
                }
            ],
            "responsive": true,
            "dom": '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
        });
    };

    var loadLookups = async function () {
        try {
            const [catRes, accRes] = await Promise.all([
                api.get(API_URL),
                api.get('/api/Finance/accounts') // Endpoint fetching Chart of Accounts
            ]);

            // Populate Parent Categories
            const categories = catRes.data || catRes || [];
            let $parent = $('#ddlParentCategory').empty().append('<option value="">-- No Parent (Root) --</option>');
            categories.forEach(c => $parent.append($('<option></option>').val(c.id).text(`[${c.code}] ${c.name}`)));

            // Populate GL Accounts
            const accounts = accRes.data || accRes || [];
            if (accounts.length > 0) {
                const $sales = $('#ddlSalesAccount').empty().append('<option value="">-- Select Revenue A/C --</option>');
                const $inv = $('#ddlInventoryAccount').empty().append('<option value="">-- Select Inventory A/C --</option>');
                const $cogs = $('#ddlCogsAccount').empty().append('<option value="">-- Select COGS A/C --</option>');

                accounts.forEach(a => {
                    let text = `[${a.code}] ${a.name}`;
                    // Filter based on AccountType Enums (Revenue=4, Asset=1, Expense=5)
                    if (a.type === 'Revenue' || a.type === '4' || a.type === 'Income') $sales.append($('<option></option>').val(a.id).text(text));
                    if (a.type === 'Asset' || a.type === '1' || a.type === 'Inventory') $inv.append($('<option></option>').val(a.id).text(text));
                    if (a.type === 'Expense' || a.type === '5' || a.type === 'CostOfGoodsSold') $cogs.append($('<option></option>').val(a.id).text(text));
                });
            }
        } catch (e) {
            console.error("Failed to load Category lookups", e);
        }
    };

    var openCreatePanel = function () {
        var form = $('#frmCategory');
        form[0].reset();
        form.removeClass('was-validated'); // Clear UI validation states
        
        $('#offcanvasCategoryLabel').html('<i class="bi bi-diagram-3 text-primary me-2"></i>New Category');
        $('#hdnCategoryId').val(0);
        modalInstance.show();
    };

    var edit = function (row) {
        var form = $('#frmCategory');
        form.removeClass('was-validated'); // Clear UI validation states
        
        $('#offcanvasCategoryLabel').html('<i class="bi bi-pen text-primary me-2"></i>Edit Category');
        $('#hdnCategoryId').val(row.id);
        
        $('#txtCatCode').val(row.code);
        $('#txtCatName').val(row.name);
        $('#ddlParentCategory').val(row.parentCategoryId || '');
        
        $('#ddlSalesAccount').val(row.salesAccountId || '');
        $('#ddlInventoryAccount').val(row.inventoryAccountId || '');
        $('#ddlCogsAccount').val(row.cogsAccountId || '');
        
        modalInstance.show();
    };

    var save = async function() {
        var id = parseInt($('#hdnCategoryId').val()) || 0;
        
        var payload = { 
            Category: {
                Id: id, 
                Code: $('#txtCatCode').val().trim().toUpperCase(),
                Name: $('#txtCatName').val().trim(),
                ParentCategoryId: parseInt($('#ddlParentCategory').val()) || null,
                SalesAccountId: parseInt($('#ddlSalesAccount').val()) || null,
                InventoryAccountId: parseInt($('#ddlInventoryAccount').val()) || null,
                CogsAccountId: parseInt($('#ddlCogsAccount').val()) || null
            }
        };

        // ARCHITECTURAL FIX 2: Disable the submit button to prevent double-clicks
        var $btn = $('#frmCategory button[type="submit"]');
        var originalBtnText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Saving...');

        try {
            var result;
            if (id === 0) result = await api.post(API_URL, payload);
            else result = await api.put(`${API_URL}/${id}`, payload);

            if (result && (result.succeeded || result.id)) {
                
                // ARCHITECTURAL FIX 3: Check your api.js! 
                // If api.js automatically fires toastr.success globally, DELETE this next line to avoid duplicates.
                toastr.success(result.message || "Category saved successfully."); 
                
                modalInstance.hide();
                table.ajax.reload(null, false);
                loadLookups(); 
            }
        } catch (e) {
            console.error("Save Error", e);
        } finally {
            // Restore button state regardless of success or failure
            $btn.prop('disabled', false).html(originalBtnText);
        }
    };

    return { init: init, openCreatePanel: openCreatePanel, edit: edit };
})();

// Initialize when document is ready
$(document).ready(function () {
    categoryApp.init();
});