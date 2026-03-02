window.productApp = {
    _table: null,
    _modal: null,
    API_URL: "/api/Product",

    init: function () {
        try {
            var modalEl = document.getElementById('productModal');
            if (modalEl) {
                this._modal = new bootstrap.Modal(modalEl, {
                    backdrop: 'static',
                    keyboard: false
                });
            }

            this._initGrid();
            this._loadMasterData();
            this._registerEvents();
        } catch (e) {
            console.error("[ProductApp] Initialization Error:", e);
        }
    },

    _registerEvents: function () {
        // Toggle Inventory Account based on Stock vs Service
        $('#Type').on('change', function () {
            var typeVal = $(this).val();
            if (typeVal == "2") { // Service
                $('#divInventoryAccount').slideUp();
                $('#InventoryAccountId').val(null).trigger('change');
            } else {
                $('#divInventoryAccount').slideDown();
            }
        });

        // Accordion (Child Row) Logic for Variants
        const self = this;
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
                url: this.API_URL,
                type: "GET",
                dataSrc: function (json) {
                    return json.data || json || [];
                },
                headers: { "Authorization": "Bearer " + localStorage.getItem("jwtToken") }
            },
            columns: [
                {
                    className: 'dt-control text-center',
                    orderable: false,
                    data: null,
                    defaultContent: '<i class="fa-solid fa-chevron-right text-muted"></i>',
                    width: "20px"
                },
                { data: "name", className: "fw-bold text-dark" },
                {
                    data: "type",
                    render: function (d) {
                        return (d === 'Service' || d == 2)
                            ? '<span class="badge bg-info bg-opacity-10 text-info border border-info">Service</span>'
                            : '<span class="badge bg-success bg-opacity-10 text-success border border-success">Stock Item</span>';
                    }
                },
                { data: "categoryName" },
                { data: "brandName" },
                {
                    data: null,
                    className: "text-end pe-3",
                    orderable: false,
                    render: function (data, type, row) {
                        const safeRow = JSON.stringify(row).replace(/"/g, '&quot;');
                        return `
                            <div class="btn-group shadow-sm">
                                <button type="button" class="btn btn-sm btn-light border px-2" onclick="productApp.edit(${safeRow})">
                                    <i class="fa-solid fa-pen-to-square text-secondary"></i>
                                </button>
                            </div>`;
                    }
                }
            ],
            order: [[1, 'asc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 20
        });
    },

    _formatVariants: function (d) {
        if (!d.variants || d.variants.length === 0) {
            return '<div class="p-3 text-muted fst-italic text-center">No variants defined for this product.</div>';
        }

        let rows = d.variants.map(v => `
            <tr>
                <td class="font-monospace text-primary fw-bold">${v.sku}</td>
                <td>${v.size || '-'}</td>
                <td>${v.color || '-'}</td>
                <td class="text-end">${parseFloat(v.costPrice || 0).toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                <td class="text-end fw-bold text-dark">${parseFloat(v.sellingPrice || 0).toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                <td class="text-center"><span class="badge bg-light text-dark border">Reorder: ${v.reorderLevel || 0}</span></td>
            </tr>
        `).join('');

        return `
            <div class="p-3 bg-light border-bottom border-top">
                <h6 class="small fw-bold text-uppercase text-muted mb-2"><i class="fa-solid fa-layer-group me-1"></i> Variant Configuration</h6>
                <table class="table table-sm table-bordered bg-white mb-0 w-75 fs-13">
                    <thead class="table-light">
                        <tr>
                            <th>SKU</th>
                            <th>Size</th>
                            <th>Color</th>
                            <th class="text-end">Std. Cost</th>
                            <th class="text-end">Selling Price</th>
                            <th class="text-center">Settings</th>
                        </tr>
                    </thead>
                    <tbody>${rows}</tbody>
                </table>
            </div>
        `;
    },

    _loadMasterData: async function () {
        try {
            const [brandRes, catRes, unitRes, accRes] = await Promise.all([
                api.get('/api/Brand'),
                api.get('/api/Category'),
                api.get('/api/UnitOfMeasure'),
                api.get('/api/Finance/accounts')
            ]);

            this._populateSelect('BrandId', brandRes.data || brandRes);
            this._populateSelect('CategoryId', catRes.data || catRes);
            this._populateSelect('UnitOfMeasureId', unitRes.data || unitRes);

            const accounts = accRes.data || accRes;
            if (accounts && accounts.length > 0) {
                this._populateSelect('SalesAccountId', accounts.filter(a => a.type === 'Revenue' || a.type === '4' || a.type === 'Income'));
                this._populateSelect('CogsAccountId', accounts.filter(a => a.type === 'Expense' || a.type === '5' || a.type === 'CostOfGoodsSold'));
                this._populateSelect('InventoryAccountId', accounts.filter(a => a.type === 'Asset' || a.type === '1' || a.type === 'Inventory'));
            }
        } catch (e) {
            console.error("[ProductApp] Master data load failed", e);
        }
    },

    _populateSelect: function (id, data) {
        let $el = $(`#${id}`);
        $el.empty().append('<option value="">-- Select --</option>');
        if (data) {
            data.forEach(i => {
                let name = i.name || i.value;
                let text = i.code ? `[${i.code}] ${name}` : name;
                $el.append($('<option></option>').val(i.id).text(text));
            });
        }
    },

    openEditor: function () {
        var form = document.getElementById('productForm');
        if (form) form.reset();

        $('#drawerTitle').html('<i class="fa-solid fa-box text-primary me-2"></i>New Product');
        $('#hdnProductId').val(0);
        $('#variantList').empty();

        this.addVariantRow(); // Add one blank variant row by default
        $('#divInventoryAccount').show();

        if (this._modal) this._modal.show();
    },

    edit: async function (row) {
        const res = await api.get(`${this.API_URL}/${row.id}`);
        if (res && (res.succeeded || res.id)) {
            const data = res.data || res;

            $('#drawerTitle').html('<i class="fa-solid fa-box text-primary me-2"></i>Edit Product');
            $('#hdnProductId').val(data.id);
            $('#Name').val(data.name);
            $('#Description').val(data.description);
            $('#Type').val(data.type).trigger('change');

            $('#BrandId').val(data.brandId);
            $('#CategoryId').val(data.categoryId);
            $('#UnitOfMeasureId').val(data.unitOfMeasureId);

            $('#SalesAccountId').val(data.salesAccountId);
            $('#CogsAccountId').val(data.cogsAccountId);

            if (data.inventoryAccountId) {
                $('#InventoryAccountId').val(data.inventoryAccountId);
            }

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
        const id = Date.now() + Math.floor(Math.random() * 1000);
        const html = `
            <tr id="row_${id}">
                <td><input type="text" class="form-control form-control-sm v-size" value="${v ? (v.size || '') : ''}" placeholder="Size"></td>
                <td><input type="text" class="form-control form-control-sm v-color" value="${v ? (v.color || '') : ''}" placeholder="Color"></td>
                <td><input type="text" class="form-control form-control-sm v-sku font-monospace" value="${v ? (v.sku || '') : ''}" placeholder="SKU"></td>
                <td><input type="number" class="form-control form-control-sm v-cost text-end" value="${v ? v.costPrice : '0'}" step="0.01"></td>
                <td><input type="number" class="form-control form-control-sm v-price text-end" value="${v ? v.sellingPrice : '0'}" step="0.01"></td>
                <td class="text-center">
                    <button type="button" class="btn btn-sm btn-light border text-danger" onclick="$('#row_${id}').remove()">
                        <i class="fa-solid fa-trash-can"></i>
                    </button>
                </td>
            </tr>`;
        $('#variantList').append(html);
    },

    save: async function () {
        var form = document.getElementById('productForm');
        if (!form || !form.checkValidity()) {
            if (form) form.reportValidity();
            return;
        }

        const id = parseInt($('#hdnProductId').val()) || 0;
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
            const skuVal = row.find('.v-sku').val();
            if (skuVal && skuVal.trim() !== '') {
                productDto.Variants.push({
                    Size: row.find('.v-size').val(),
                    Color: row.find('.v-color').val(),
                    SKU: skuVal,
                    CostPrice: parseFloat(row.find('.v-cost').val()) || 0,
                    SellingPrice: parseFloat(row.find('.v-price').val()) || 0,
                    ReorderLevel: 0
                });
            }
        });

        if (productDto.Variants.length === 0) {
            toastr.warning("At least one Variant with a valid SKU is required.");
            return;
        }

        const commandPayload = { Product: productDto };
        let res;

        if (id === 0) {
            res = await api.post(this.API_URL, commandPayload);
        } else {
            res = await api.put(`${this.API_URL}/${id}`, commandPayload);
        }

        if (res && (res.succeeded || res.id)) {
            if (this._modal) this._modal.hide();
            if (this._table) this._table.ajax.reload(null, false);
        }
    }
};

$(document).ready(function () {
    window.productApp.init();
});