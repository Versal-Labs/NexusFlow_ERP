window.productApp = {
    _table: null,
    _modal: null,
    API_URL: "/api/Product",

    init: function () {
        var modalEl = document.getElementById('productModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });

        this._initGrid();
        this._loadMasterData();
        this._registerEvents();
        bulkApp.init(); // Initialize Bulk App
    },

    _registerEvents: function () {
        const self = this;

        // 1. TIER-1 ERP DYNAMIC UI TOGGLE
        $('#Type').on('change', function () {
            const typeVal = parseInt($(this).val());
            const isRawMaterial = (typeVal === 1);

            if (isRawMaterial) {
                // Hide Selling Price & Rename Columns to generic attributes
                $('.th-price').hide();
                $('.td-price').hide();
                $('.th-size').text('Attribute 1 (e.g. Length/Type)');
                $('.th-color').text('Attribute 2 (e.g. Material)');
                $('#rawMaterialAlert').removeClass('d-none');
            } else {
                // Show Selling Price & revert to standard names
                $('.th-price').show();
                $('.td-price').show();
                $('.th-size').text('Size');
                $('.th-color').text('Color');
                $('#rawMaterialAlert').addClass('d-none');
            }
        });

        // Accordion Logic
        $('#productsGrid tbody').on('click', 'td.dt-control', function () {
            var tr = $(this).closest('tr');
            var row = self._table.row(tr);

            if (row.child.isShown()) {
                row.child.hide();
                tr.removeClass('shown');
                $(this).html('<i class="fa-solid fa-chevron-right text-muted"></i>');
            } else {
                row.child(self._formatVariants(row.data())).show();
                tr.addClass('shown');
                $(this).html('<i class="fa-solid fa-chevron-down text-primary"></i>');
            }
        });
    },

    _initGrid: function () {
        this._table = $('#productsGrid').DataTable({
            ajax: {
                url: this.API_URL, type: "GET",
                dataSrc: function (json) { return json.data || json || []; }
            },
            columns: [
                { className: 'dt-control text-center', orderable: false, data: null, defaultContent: '<i class="fa-solid fa-chevron-right text-muted"></i>', width: "20px" },
                { data: "name", className: "fw-bold text-dark" },
                {
                    data: "type",
                    render: function (d) {
                        if (d === 'RawMaterial' || d == 1) return '<span class="badge bg-warning bg-opacity-10 text-warning border border-warning">Raw Material</span>';
                        if (d === 'Service' || d == 3) return '<span class="badge bg-info bg-opacity-10 text-info border border-info">Service</span>';
                        return '<span class="badge bg-success bg-opacity-10 text-success border border-success">Finished Good</span>';
                    }
                },
                { data: "categoryName" },
                { data: "brandName" },
                {
                    data: null, className: "text-end pe-3", orderable: false,
                    render: function (data, type, row) {
                        const safeRow = JSON.stringify(row).replace(/"/g, '&quot;');
                        return `<button type="button" class="btn btn-sm btn-light border px-2 shadow-sm" onclick="productApp.edit(${safeRow})"><i class="fa-solid fa-pen-to-square text-primary"></i> Edit</button>`;
                    }
                }
            ],
            order: [[1, 'asc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    _formatVariants: function (d) {
        if (!d.variants || d.variants.length === 0) return '<div class="p-3 text-muted fst-italic text-center">No variants defined.</div>';

        const isRawMaterial = (d.type === 'RawMaterial' || d.type == 1);
        const priceHeader = isRawMaterial ? '' : '<th class="text-end">Selling Price</th>';

        let rows = d.variants.map(v => {
            const priceCell = isRawMaterial ? '' : `<td class="text-end fw-bold text-dark">${parseFloat(v.sellingPrice || 0).toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>`;
            return `<tr>
                <td class="font-monospace text-primary fw-bold">${v.sku}</td>
                <td>${v.size === 'N/A' ? '-' : v.size}</td>
                <td>${v.color === 'N/A' ? '-' : v.color}</td>
                <td class="text-end">${parseFloat(v.costPrice || 0).toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                ${priceCell}
            </tr>`;
        }).join('');

        return `
            <div class="p-3 bg-light border-bottom border-top">
                <table class="table table-sm table-bordered bg-white mb-0 w-75 fs-13 shadow-sm">
                    <thead class="table-light"><tr><th>SKU</th><th>Attribute 1</th><th>Attribute 2</th><th class="text-end">Std. Cost</th>${priceHeader}</tr></thead>
                    <tbody>${rows}</tbody>
                </table>
            </div>`;
    },

    _loadMasterData: async function () {
        try {
            const [brandRes, catRes, unitRes] = await Promise.all([
                api.get('/api/Brand'), api.get('/api/Category'), api.get('/api/UnitOfMeasure')
            ]);
            this._populateSelect('BrandId', brandRes.data || brandRes);
            this._populateSelect('CategoryId', catRes.data || catRes);
            this._populateSelect('UnitOfMeasureId', unitRes.data || unitRes);

            // Populate Bulk Edit Category Filter
            this._populateSelect('bulkCategoryFilter', catRes.data || catRes);
            $('#bulkCategoryFilter option:first').text('-- All Categories --');
        } catch (e) { console.error(e); }
    },

    _populateSelect: function (id, data) {
        let $el = $(`#${id}`);
        $el.empty().append('<option value="">-- Select --</option>');
        if (data) data.forEach(i => $el.append($('<option></option>').val(i.id).text(i.name)));
    },

    openEditor: function () {
        $('#productForm')[0].reset();
        $('#drawerTitle').html('<i class="fa-solid fa-box text-primary me-2"></i>New Product');
        $('#hdnProductId').val(0);
        $('#variantList').empty();
        $('#Type').trigger('change'); // Trigger UI logic
        this.addVariantRow();
        if (this._modal) this._modal.show();
    },

    edit: async function (row) {
        const res = await api.get(`${this.API_URL}/${row.id}`);
        if (res && res.data) {
            const data = res.data;
            $('#drawerTitle').html('<i class="fa-solid fa-box text-primary me-2"></i>Edit Product');
            $('#hdnProductId').val(data.id);
            $('#Name').val(data.name);
            $('#Description').val(data.description);

            // Map string enums back to integer for the dropdown
            let typeInt = data.type === 'RawMaterial' ? 1 : data.type === 'FinishedGood' ? 2 : 3;
            $('#Type').val(typeInt).trigger('change');

            $('#BrandId').val(data.brandId);
            $('#CategoryId').val(data.categoryId);
            $('#UnitOfMeasureId').val(data.unitOfMeasureId);

            $('#variantList').empty();
            if (data.variants && data.variants.length > 0) {
                data.variants.forEach(v => this.addVariantRow(v));
            } else {
                this.addVariantRow();
            }
            if (this._modal) this._modal.show();
        }
    },

    addVariantRow: function (v = null) {
        const id = Date.now();
        const isRawMaterial = parseInt($('#Type').val()) === 1;
        const displayStyle = isRawMaterial ? 'display:none;' : '';

        const html = `
            <tr id="row_${id}">
                <input type="hidden" class="v-id" value="${v ? v.id : 0}">
                <td><input type="text" class="form-control form-control-sm v-size" value="${v ? (v.size === 'N/A' ? '' : v.size) : ''}"></td>
                <td><input type="text" class="form-control form-control-sm v-color" value="${v ? (v.color === 'N/A' ? '' : v.color) : ''}"></td>
                <td><input type="text" class="form-control form-control-sm v-sku font-monospace fw-bold text-primary" value="${v ? v.sku : ''}"></td>
                <td><input type="number" class="form-control form-control-sm v-cost text-end" value="${v ? v.costPrice : '0'}" step="0.01"></td>
                <td class="td-price" style="${displayStyle}"><input type="number" class="form-control form-control-sm v-price text-end bg-light" value="${v ? v.sellingPrice : '0'}" step="0.01"></td>
                <td class="text-center align-middle"><button type="button" class="btn btn-sm text-danger" onclick="$('#row_${id}').remove()"><i class="fa-solid fa-trash-can"></i></button></td>
            </tr>`;
        $('#variantList').append(html);
    },

    save: async function () {
        var form = document.getElementById('productForm');
        if (!form.checkValidity()) { form.reportValidity(); return; }

        const isRawMaterial = parseInt($('#Type').val()) === 1;
        const productDto = {
            Id: parseInt($('#hdnProductId').val()) || 0,
            Name: $('#Name').val(),
            Description: $('#Description').val(),
            Type: parseInt($('#Type').val()),
            BrandId: parseInt($('#BrandId').val()),
            CategoryId: parseInt($('#CategoryId').val()),
            UnitOfMeasureId: parseInt($('#UnitOfMeasureId').val()),
            Variants: []
        };

        $('#variantList tr').each(function () {
            const row = $(this);
            const skuVal = row.find('.v-sku').val();
            if (skuVal && skuVal.trim() !== '') {
                productDto.Variants.push({
                    Id: parseInt(row.find('.v-id').val()) || 0,
                    Size: row.find('.v-size').val() || 'N/A',
                    Color: row.find('.v-color').val() || 'N/A',
                    SKU: skuVal,
                    CostPrice: parseFloat(row.find('.v-cost').val()) || 0,
                    SellingPrice: isRawMaterial ? 0 : (parseFloat(row.find('.v-price').val()) || 0),
                    ReorderLevel: 0
                });
            }
        });

        if (productDto.Variants.length === 0) { toastr.warning("At least one Variant with a valid SKU is required."); return; }

        try {
            const res = productDto.Id === 0
                ? await api.post(this.API_URL, { Product: productDto })
                : await api.put(this.API_URL, { Product: productDto });

            if (res && res.succeeded) {
                toastr.success(res.message);
                this._modal.hide();
                this._table.ajax.reload(null, false);
            } else { toastr.error(res.messages?.[0] || "Save failed"); }
        } catch (e) { toastr.error("Network error."); }
    }
};

// ==========================================
// BULK EXCEL-STYLE EDIT ENGINE
// ==========================================
window.bulkApp = {
    _table: null,
    _modal: null,
    _modifiedVariants: {}, // Tracks dirty rows

    init: function () {
        var el = document.getElementById('bulkModal');
        if (el) {
            this._modal = new bootstrap.Modal(el, { backdrop: 'static' });

            // TIER-1 UI FIX: Force DataTables to recalculate widths AFTER modal is fully visible
            $(el).on('shown.bs.modal', () => {
                if (this._table) {
                    this._table.columns.adjust().draw();
                }
            });
        }

        $('#bulkTypeFilter, #bulkCategoryFilter').on('change', () => {
            if (this._table) this._table.ajax.reload();
        });
    },

    openModal: function () {
        this._modifiedVariants = {}; // Clear dirty state
        if (!this._table) this._initBulkGrid();
        else this._table.ajax.reload();

        this._modal.show();
    },

    _initBulkGrid: function () {
        this._table = $('#bulkGrid').DataTable({
            ajax: {
                url: "/api/Product", type: "GET",
                dataSrc: function (json) {
                    // Flatten data: we need 1 row per Variant, not per Product
                    let variants = [];
                    let data = json.data || json || [];

                    const typeFilter = $('#bulkTypeFilter').val();
                    const catFilter = $('#bulkCategoryFilter option:selected').text();

                    data.forEach(p => {
                        // Apply Header Filters manually for the flattened array
                        let typeStr = p.type === 1 || p.type === 'RawMaterial' ? 'Raw Material' : p.type === 3 || p.type === 'Service' ? 'Service' : 'Finished Good';
                        if (typeFilter && typeStr !== typeFilter) return;
                        if ($('#bulkCategoryFilter').val() && p.categoryName !== catFilter) return;

                        if (p.variants) {
                            p.variants.forEach(v => {
                                variants.push({
                                    variantId: v.id, type: typeStr, category: p.categoryName, productName: p.name,
                                    sku: v.sku, size: v.size === 'N/A' ? '' : v.size, color: v.color === 'N/A' ? '' : v.color,
                                    costPrice: v.costPrice, sellingPrice: v.sellingPrice
                                });
                            });
                        }
                    });
                    return variants;
                }
            },
            columns: [
                { data: "type", width: "10%" },
                { data: "category", width: "15%" },
                { data: "productName", className: "fw-bold", width: "15%" },
                { data: "sku", className: "font-monospace text-primary", width: "10%" },
                { data: "size", render: this._renderInput('size') },
                { data: "color", render: this._renderInput('color') },
                { data: "costPrice", render: this._renderInput('costPrice', 'number') },
                {
                    data: "sellingPrice",
                    render: function (data, type, row) {
                        if (row.type === 'Raw Material') return '<span class="text-muted fst-italic">N/A</span>';
                        return bulkApp._renderInput('sellingPrice', 'number')(data, type, row);
                    }
                }
            ],
            scrollY: "60vh", scrollCollapse: true, paging: false, // Excel style scrolling
            dom: '<"d-flex justify-content-between align-items-center mb-2"f>rt<"mt-2"i>'
        });

        // Event delegation for input changes to track dirtiness
        $('#bulkGrid tbody').on('input', '.bulk-edit-input', function () {
            $(this).closest('td').addClass('modified-cell');
            const vId = $(this).data('id');
            const field = $(this).data('field');
            const val = $(this).val();

            if (!bulkApp._modifiedVariants[vId]) bulkApp._modifiedVariants[vId] = {};
            bulkApp._modifiedVariants[vId][field] = val;
            bulkApp._modifiedVariants[vId].variantId = vId; // ensure ID is attached
        });
    },

    _renderInput: function (field, inputType = 'text') {
        return function (data, type, row) {
            let step = inputType === 'number' ? 'step="0.01"' : '';
            let align = inputType === 'number' ? 'text-end fw-bold' : '';
            return `<input type="${inputType}" class="bulk-edit-input ${align}" data-id="${row.variantId}" data-field="${field}" value="${data}" ${step}>`;
        }
    },

    saveChanges: async function () {
        const modifications = Object.values(this._modifiedVariants);

        if (modifications.length === 0) {
            toastr.info("No changes detected."); return;
        }

        // TIER-1 Confirm via SweetAlert
        Swal.fire({
            title: 'Confirm Bulk Edit',
            text: `You are about to modify ${modifications.length} variant(s). Are you sure?`,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#0d6efd',
            cancelButtonColor: '#6c757d',
            confirmButtonText: 'Yes, Save Changes!'
        }).then(async (result) => {
            if (result.isConfirmed) {
                try {
                    // Map partial modifications back to full DTO structure
                    let payload = modifications.map(m => {
                        // Grab original row data to fill in fields that weren't touched
                        let tr = $(`input[data-id="${m.variantId}"]`).closest('tr');
                        return {
                            VariantId: m.variantId,
                            Size: m.size !== undefined ? m.size : tr.find(`input[data-field="size"]`).val() || '',
                            Color: m.color !== undefined ? m.color : tr.find(`input[data-field="color"]`).val() || '',
                            CostPrice: parseFloat(m.costPrice !== undefined ? m.costPrice : tr.find(`input[data-field="costPrice"]`).val()) || 0,
                            SellingPrice: parseFloat(m.sellingPrice !== undefined ? m.sellingPrice : (tr.find(`input[data-field="sellingPrice"]`).val() || 0)) || 0
                        };
                    });

                    const res = await api.put('/api/Product/bulk-update', { Variants: payload });

                    if (res && res.succeeded) {
                        Swal.fire('Saved!', res.message, 'success');
                        this._modifiedVariants = {}; // Reset dirty state
                        this._table.ajax.reload(null, false); // Reload bulk grid
                        productApp._table.ajax.reload(null, false); // Reload underlying main grid
                    } else {
                        toastr.error(res.messages?.[0] || "Update failed.");
                    }
                } catch (e) {
                    toastr.error("Network error during bulk save.");
                }
            }
        });
    }
};

$(document).ready(() => productApp.init());