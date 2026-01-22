/**
 * Nexus ERP - Product Master Logic
 * Architecture: Hybrid (MVC View + CQRS API)
 */

var productApp = (function () {
    "use strict";

    // --- Configuration ---
    const API_URL = "/api/products";
    const LOOKUP_URLS = {
        brands: "/api/brands",
        categories: "/api/categories",
        uom: "/api/unitofmeasures"
    };

    var _table;
    var _drawer;

    // --- Initialization ---
    function init() {
        _initGrid();
        _loadDropdowns(); // Pre-load master data for the form

        var drawerEl = document.getElementById('productDrawer');
        if (drawerEl) _drawer = new bootstrap.Offcanvas(drawerEl);
    }

    function _initGrid() {
        // This connects to 'GetProductsHandler' which returns specific Variant items
        _table = $('#productsGrid').DataTable({
            processing: true,
            serverSide: false, // Client-side for now based on your query
            ajax: {
                url: API_URL, // GET request
                type: "GET",
                dataSrc: "data" // Matches Result<List<ProductDto>> wrapper
            },
            columns: [
                {
                    data: 'sku',
                    render: function (data) { return `<span class="font-monospace fw-bold text-dark">${data}</span>`; }
                },
                { data: 'name' }, // This is the Variant Name (e.g. Shirt - Red - L)
                {
                    data: null,
                    render: function (data, type, row) {
                        // Extracting variant details from name string or extended DTO if available
                        // Ideally, your GetProductsHandler DTO should have Size/Color fields exposed.
                        // For now, we assume standard naming convention or simple display.
                        return `<small class="text-muted">Item Variant</small>`;
                    }
                },
                { data: 'costPrice', visible: false }, // Hidden, usually for internal use
                {
                    data: 'sellingPrice',
                    className: "text-end",
                    render: $.fn.dataTable.render.number(',', '.', 2, 'Rs. ')
                },
                {
                    data: null,
                    className: "text-end pe-4",
                    orderable: false,
                    render: function (data) {
                        return `<button class="btn btn-sm btn-light border" title="Edit"><i class="bi bi-pencil"></i></button>`;
                    }
                }
            ]
        });
    }

    // --- Master Data Loading ---
    function _loadDropdowns() {
        // Helper to load <select> options
        function loadSelect(url, elementId) {
            $.get(url, function (response) {
                var list = response.data || response; // Handle different wrapper styles
                var options = '<option value="" selected disabled>Select...</option>';
                list.forEach(item => {
                    options += `<option value="${item.id}">${item.name}</option>`;
                });
                $(`#${elementId}`).html(options);
            });
        }

        // Trigger loads (Assuming these APIs exist based on your Entities)
        loadSelect(LOOKUP_URLS.categories, 'CategoryId');
        loadSelect(LOOKUP_URLS.brands, 'BrandId');
        loadSelect(LOOKUP_URLS.uom, 'UnitOfMeasureId');
    }

    // --- UI Actions ---
    function openEditor() {
        document.getElementById('productForm').reset();
        $('#variantList').empty(); // Clear table
        addVariantRow(); // Add one empty row by default
        _drawer.show();
    }

    function addVariantRow() {
        var rowId = Date.now(); // Unique ID for row removal
        var html = `
            <tr id="row_${rowId}">
                <td><input type="text" class="form-control form-control-sm v-size" placeholder="e.g. L"></td>
                <td><input type="text" class="form-control form-control-sm v-color" placeholder="e.g. Red"></td>
                <td><input type="text" class="form-control form-control-sm v-sku" placeholder="Auto/Manual"></td>
                <td><input type="number" class="form-control form-control-sm v-cost" step="0.01"></td>
                <td><input type="number" class="form-control form-control-sm v-price" step="0.01"></td>
                <td class="text-end">
                    <button type="button" class="btn btn-link text-danger p-0" onclick="$('#row_${rowId}').remove()">
                        <i class="bi bi-x-circle-fill"></i>
                    </button>
                </td>
            </tr>
        `;
        $('#variantList').append(html);
    }

    // --- Save Logic (The Complex Part) ---
    function save() {
        var form = document.getElementById('productForm');
        if (!form.checkValidity()) {
            form.classList.add('was-validated');
            return;
        }

        // 1. Build Header Object
        var productDto = {
            Name: $('#Name').val(),
            Description: $('#Description').val(),
            CategoryId: parseInt($('#CategoryId').val()),
            BrandId: parseInt($('#BrandId').val()),
            UnitOfMeasureId: parseInt($('#UnitOfMeasureId').val()),
            Type: 1, // Default to 'Stock Item' enum
            Variants: []
        };

        // 2. Iterate Table Rows to build Variants List
        $('#variantList tr').each(function () {
            var row = $(this);
            var variant = {
                Size: row.find('.v-size').val(),
                Color: row.find('.v-color').val(),
                SKU: row.find('.v-sku').val(),
                CostPrice: parseFloat(row.find('.v-cost').val()) || 0,
                SellingPrice: parseFloat(row.find('.v-price').val()) || 0,
                ReorderLevel: 10 // Default or add input for this
            };

            // Simple validation: Skip empty rows
            if (variant.SKU && variant.Size) {
                productDto.Variants.push(variant);
            }
        });

        if (productDto.Variants.length === 0) {
            Swal.fire('Error', 'Please add at least one variant (e.g. Size/Color)', 'warning');
            return;
        }

        // 3. Send Payload (Matches CreateProductCommand structure)
        $.ajax({
            url: API_URL,
            type: "POST",
            contentType: "application/json",
            data: JSON.stringify({ Product: productDto }), // Wrapping in 'Product' property as per Command
            success: function (response) {
                _drawer.hide();
                _table.ajax.reload();
                Swal.fire({ icon: 'success', title: 'Saved!', toast: true, position: 'top-end', timer: 2000 });
            },
            error: function (xhr) {
                Swal.fire('Error', 'Failed to save product.', 'error');
            }
        });
    }

    return {
        init: init,
        openEditor: openEditor,
        addVariantRow: addVariantRow,
        save: save
    };
})();

$(document).ready(function () {
    productApp.init();
});