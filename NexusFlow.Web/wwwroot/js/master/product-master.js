/**
 * Nexus ERP - Product Master Logic
 * Features: Accordion Grid, Financial Validation, Select2 Integration
 */
var productApp = (function () {
    "use strict";

    var table, drawer;
    const API_URL = "/api/Product";

    var init = function () {
        _initGrid();
        _loadMasterData();
        _registerEvents();

        var drawerEl = document.getElementById('productDrawer');
        if (drawerEl) drawer = new bootstrap.Offcanvas(drawerEl);
    };

    var _registerEvents = function () {
        // Toggle Inventory Account visibility
        $('#Type').on('change', function () {
            var typeVal = $(this).val();
            if (typeVal == "2") { // Service
                $('#divInventoryAccount').slideUp();
                $('#InventoryAccountId').val(null).trigger('change');
            } else {
                $('#divInventoryAccount').slideDown();
            }
        });

        // Add event listener for opening and closing details
        $('#productsGrid tbody').on('click', 'td.dt-control', function () {
            var tr = $(this).closest('tr');
            var row = table.row(tr);

            if (row.child.isShown()) {
                // This row is already open - close it
                row.child.hide();
                tr.removeClass('shown');
                $(this).html('<i class="bi bi-chevron-right text-muted"></i>');
            } else {
                // Open this row
                row.child(_formatVariants(row.data())).show();
                tr.addClass('shown');
                $(this).html('<i class="bi bi-chevron-down text-primary"></i>');
            }
        });
    };

    var _initGrid = function () {
        table = $('#productsGrid').DataTable({
            ajax: { url: API_URL, type: "GET", dataSrc: "data" },
            columns: [
                {
                    // Accordion Control Column
                    className: 'dt-control text-center cursor-pointer',
                    orderable: false,
                    data: null,
                    defaultContent: '<i class="bi bi-chevron-right text-muted"></i>',
                    width: "20px"
                },
                { data: "name", className: "fw-bold", width: "25%" },
                {
                    data: "type", width: "10%",
                    render: function (d) {
                        let badge = d === 'Service' || d == 2
                            ? '<span class="badge bg-info-subtle text-info border border-info-subtle">Service</span>'
                            : '<span class="badge bg-success-subtle text-success border border-success-subtle">Stock Item</span>';
                        return badge;
                    }
                },
                { data: "categoryName", width: "15%" },
                { data: "brandName", width: "15%" },
                {
                    data: null, className: "text-end pe-4", width: "15%",
                    render: function (data, type, row) {
                        const safeRow = JSON.stringify(row).replace(/"/g, '&quot;');
                        return `<button class="btn btn-sm btn-outline-secondary shadow-sm" onclick="productApp.edit(${safeRow})"><i class="bi bi-pencil me-1"></i> Edit</button>`;
                    }
                }
            ],
            order: [[1, 'asc']] // Order by Name
        });
    };

    // --- Template for Child Row (Variants Table) ---
    function _formatVariants(d) {
        if (!d.variants || d.variants.length === 0) {
            return '<div class="p-3 text-muted fst-italic text-center">No variants defined for this product.</div>';
        }

        let rows = d.variants.map(v => `
            <tr>
                <td class="font-monospace text-primary fw-bold">${v.sku}</td>
                <td>${v.size || '-'}</td>
                <td>${v.color || '-'}</td>
                <td class="text-end">${parseFloat(v.costPrice).toFixed(2)}</td>
                <td class="text-end fw-bold text-dark">${parseFloat(v.sellingPrice).toFixed(2)}</td>
                <td class="text-end"><span class="badge bg-light text-dark border">Reorder: ${v.reorderLevel}</span></td>
            </tr>
        `).join('');

        return `
            <div class="p-3 bg-light border-bottom shadow-inner">
                <h6 class="small fw-bold text-uppercase text-muted mb-2">Variant Configuration</h6>
                <table class="table table-sm table-bordered bg-white mb-0 w-75">
                    <thead class="table-light">
                        <tr>
                            <th>SKU</th>
                            <th>Size</th>
                            <th>Color</th>
                            <th class="text-end">Std. Cost</th>
                            <th class="text-end">Selling Price</th>
                            <th class="text-end">Stock Settings</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${rows}
                    </tbody>
                </table>
            </div>
        `;
    }

    // --- Master Data & Actions ---
    var _loadMasterData = async function () {
        try {
            const [brands, cats, units, accounts] = await Promise.all([
                api.get('/api/Brand'),
                api.get('/api/Category'),
                api.get('/api/UnitOfMeasure'),
                api.get('/api/Finance/accounts')
            ]);

            _populateSelect('BrandId', brands.data);
            _populateSelect('CategoryId', cats.data);
            _populateSelect('UnitOfMeasureId', units.data);

            if (accounts && accounts.data) {
                const accs = accounts.data;
                _populateSelect('SalesAccountId', accs.filter(a => a.type === 'Revenue' || a.type === 'Income'));
                _populateSelect('CogsAccountId', accs.filter(a => a.type === 'Expense' || a.type === 'CostOfGoodsSold'));
                _populateSelect('InventoryAccountId', accs.filter(a => a.type === 'Asset' || a.type === 'Inventory'));
            }
        } catch (e) {
            console.error("Master data load failed", e);
        }
    };

    var _populateSelect = function (id, data) {
        let opts = '<option value="">Select...</option>';
        if (data) data.forEach(i => opts += `<option value="${i.id}">${i.name}</option>`);
        $(`#${id}`).html(opts);
    };

    var openEditor = function () {
        $('#drawerTitle').text("New Product");
        $('#hdnProductId').val(0);
        $('#productForm')[0].reset();
        $('#variantList').empty();
        addVariantRow();
        $('.select2').val('').trigger('change');
        $('#divInventoryAccount').show();
        drawer.show();
    };

    var edit = function (row) {
        _fetchAndPopulate(row.id);
    };

    var _fetchAndPopulate = async function (id) {
        const res = await api.get(`/api/Product/${id}`);
        if (res && res.succeeded) {
            const data = res.data;
            $('#drawerTitle').text("Edit Product");
            $('#hdnProductId').val(data.id);
            $('#Name').val(data.name);
            $('#Description').val(data.description);
            $('#Type').val(data.type).trigger('change');

            $('#BrandId').val(data.brandId).trigger('change');
            $('#CategoryId').val(data.categoryId).trigger('change');
            $('#UnitOfMeasureId').val(data.unitOfMeasureId).trigger('change');

            $('#SalesAccountId').val(data.salesAccountId).trigger('change');
            $('#CogsAccountId').val(data.cogsAccountId).trigger('change');
            if (data.inventoryAccountId) $('#InventoryAccountId').val(data.inventoryAccountId).trigger('change');

            $('#variantList').empty();
            if (data.variants) data.variants.forEach(v => addVariantRow(v));
            drawer.show();
        }
    };

    var addVariantRow = function (v = null) {
        const id = Date.now() + Math.random().toString(16).slice(2);
        const html = `
            <tr id="row_${id}">
                <td><input type="text" class="form-control form-control-sm v-size" value="${v ? v.size : ''}" placeholder="Size"></td>
                <td><input type="text" class="form-control form-control-sm v-color" value="${v ? v.color : ''}" placeholder="Color"></td>
                <td><input type="text" class="form-control form-control-sm v-sku" value="${v ? v.sku : ''}" placeholder="SKU"></td>
                <td><input type="number" class="form-control form-control-sm v-cost" value="${v ? v.costPrice : ''}"></td>
                <td><input type="number" class="form-control form-control-sm v-price" value="${v ? v.sellingPrice : ''}"></td>
                <td><button type="button" class="btn btn-sm text-danger" onclick="$('#row_${id}').remove()"><i class="bi bi-trash"></i></button></td>
            </tr>`;
        $('#variantList').append(html);
    };

    var save = async function () {
        const id = parseInt($('#hdnProductId').val());
        const typeVal = $('#Type').val();

        const productDto = {
            Id: id,
            Name: $('#Name').val(),
            Description: $('#Description').val(),
            Type: parseInt(typeVal),
            BrandId: parseInt($('#BrandId').val()),
            CategoryId: parseInt($('#CategoryId').val()),
            UnitOfMeasureId: parseInt($('#UnitOfMeasureId').val()),
            SalesAccountId: parseInt($('#SalesAccountId').val()),
            CogsAccountId: parseInt($('#CogsAccountId').val()),
            InventoryAccountId: (typeVal != "2") ? parseInt($('#InventoryAccountId').val()) : null,
            Variants: []
        };

        $('#variantList tr').each(function () {
            const row = $(this);
            const variant = {
                Size: row.find('.v-size').val(),
                Color: row.find('.v-color').val(),
                SKU: row.find('.v-sku').val(),
                CostPrice: parseFloat(row.find('.v-cost').val()) || 0,
                SellingPrice: parseFloat(row.find('.v-price').val()) || 0,
                ReorderLevel: 0
            };
            if (variant.SKU) productDto.Variants.push(variant);
        });

        if (!productDto.BrandId || !productDto.CategoryId || !productDto.UnitOfMeasureId) {
            toastr.warning("Brand, Category, and Unit are required.");
            return;
        }
        if (!productDto.SalesAccountId || !productDto.CogsAccountId) {
            toastr.warning("Financial accounts are required.");
            return;
        }

        let res;
        const commandPayload = { Product: productDto };

        if (id === 0) res = await api.post(API_URL, commandPayload);
        else res = await api.put(API_URL, commandPayload);

        if (res && res.succeeded) {
            drawer.hide();
            table.ajax.reload();
        }
    };

    return { init, openEditor, edit, addVariantRow, save };
})();

$(document).ready(function () { productApp.init(); });