/**
 * Nexus ERP - Purchase Order Controller
 * Handles Header-Detail creation with dynamic calculations.
 */
var poApp = (function () {
    "use strict";

    var table, drawer;
    var _itemList = []; // Local state for items
    var _productsCache = []; // To store product details (Price, Name)

    var init = function () {
        _initGrid();
        _loadMasterData();
        _setupEventHandlers();

        var el = document.getElementById('poDrawer');
        if (el) drawer = new bootstrap.Offcanvas(el);
    };

    var _initGrid = function () {
        table = $('#poGrid').DataTable({
            ajax: { url: "/api/Purchasing", type: "GET", dataSrc: "data" },
            columns: [
                { data: "poNumber", className: "font-monospace fw-bold text-primary" },
                {
                    data: "date",
                    render: function (d) { return new Date(d).toLocaleDateString(); }
                },
                { data: "supplierName" },
                {
                    data: "status",
                    render: function (d) {
                        var color = d === 'Draft' ? 'secondary' : (d === 'Closed' ? 'success' : 'primary');
                        return `<span class="badge bg-${color}">${d}</span>`;
                    }
                },
                {
                    data: "totalAmount", className: "text-end fw-bold",
                    render: $.fn.dataTable.render.number(',', '.', 2)
                },
                {
                    data: null, className: "text-end",
                    render: function (data, type, row) {
                        let btns = '';

                        // Only show Receive button if not closed
                        if (row.status !== 'Closed') {
                            btns += `<button class="btn btn-sm btn-success shadow-sm me-1" onclick="grnApp.openWizard(${row.id})">
                        <i class="bi bi-box-seam me-1"></i> Receive
                     </button>`;
                        }

                        btns += `<button class="btn btn-sm btn-light border">View</button>`;
                        return btns;
                    }
                }
            ],
            order: [[0, 'desc']] // Newest first
        });
    };

    var _loadMasterData = async function () {
        // 1. Load Suppliers
        try {
            const suppliers = await api.get('/api/Supplier'); // Use SupplierController
            let opts = '<option value="">Select Supplier...</option>';
            if (suppliers.data) {
                suppliers.data.forEach(s => opts += `<option value="${s.id}">${s.name}</option>`);
            }
            $('#ddlSupplier').html(opts).select2({
                theme: 'bootstrap-5',
                dropdownParent: $('#poDrawer')
            });

            // 2. Load Products for Search (Optimized: Get variants directly)
            // In a real app, this should be an AJAX Select2 search. 
            // For now, we load all active variants.
            const products = await api.get('/api/Product'); // Ensure this returns Variant info or use a dedicated endpoint

            // Transform Product DTO to flat Variant list for dropdown
            let prodOpts = '<option value="">Search Product...</option>';
            if (products.data) {
                products.data.forEach(p => {
                    p.variants.forEach(v => {
                        // Store in cache for quick lookup of price/name
                        _productsCache.push({
                            id: v.id, // Variant ID
                            name: `${p.name} (${v.size}/${v.color})`,
                            sku: v.sku,
                            cost: v.costPrice
                        });
                        prodOpts += `<option value="${v.id}">${v.sku} - ${p.name} (${v.size}/${v.color})</option>`;
                    });
                });
            }
            $('#ddlProductSearch').html(prodOpts).select2({
                theme: 'bootstrap-5',
                dropdownParent: $('#poDrawer'),
                placeholder: "Type to search SKU or Name..."
            });

        } catch (e) {
            console.error("Master data error", e);
        }
    };

    var _setupEventHandlers = function () {
        // Add Line Button
        $('#btnAddLine').on('click', function () {
            var variantId = parseInt($('#ddlProductSearch').val());
            if (!variantId) return;

            // Check if already exists
            if (_itemList.find(x => x.productVariantId === variantId)) {
                toastr.warning("Item already added.");
                return;
            }

            var product = _productsCache.find(x => x.id === variantId);
            if (product) {
                _addItemRow(product);
                $('#ddlProductSearch').val(null).trigger('change'); // Reset dropdown
            }
        });
    };

    var _addItemRow = function (product) {
        var rowId = Date.now();

        var item = {
            rowId: rowId,
            productVariantId: product.id,
            productName: product.name,
            sku: product.sku,
            unitCost: product.cost,
            quantity: 1
        };
        _itemList.push(item);
        _renderTable();
    };

    var _renderTable = function () {
        var tbody = $('#poItemsBody');
        tbody.empty();
        var grandTotal = 0;

        _itemList.forEach((item, index) => {
            var lineTotal = item.quantity * item.unitCost;
            grandTotal += lineTotal;

            var html = `
                <tr>
                    <td>
                        <div class="fw-bold">${item.productName}</div>
                        <div class="small text-muted font-monospace">${item.sku}</div>
                    </td>
                    <td>
                        <input type="number" class="form-control form-control-sm text-end" 
                               value="${item.unitCost}" step="0.01"
                               onchange="poApp.updateItem(${index}, 'cost', this.value)">
                    </td>
                    <td>
                        <input type="number" class="form-control form-control-sm text-center" 
                               value="${item.quantity}" min="1"
                               onchange="poApp.updateItem(${index}, 'qty', this.value)">
                    </td>
                    <td class="text-end fw-bold align-middle">
                        ${lineTotal.toFixed(2)}
                    </td>
                    <td class="text-end">
                        <button class="btn btn-sm text-danger" onclick="poApp.removeItem(${index})">
                            <i class="bi bi-trash"></i>
                        </button>
                    </td>
                </tr>
            `;
            tbody.append(html);
        });

        $('#lblGrandTotal').text(grandTotal.toFixed(2));
    };

    // --- Public Actions ---

    var openCreatePanel = function () {
        $('#frmPO')[0].reset();
        $('#ddlSupplier').val(null).trigger('change');
        $('#ddlProductSearch').val(null).trigger('change');
        // Set Today's Date
        document.getElementById('txtDate').valueAsDate = new Date();

        _itemList = [];
        _renderTable();

        drawer.show();
    };

    var updateItem = function (index, field, value) {
        var val = parseFloat(value) || 0;
        if (field === 'qty') _itemList[index].quantity = val;
        if (field === 'cost') _itemList[index].unitCost = val;
        _renderTable(); // Re-calc totals
    };

    var removeItem = function (index) {
        _itemList.splice(index, 1);
        _renderTable();
    };

    var save = async function () {
        var supplierId = $('#ddlSupplier').val();
        if (!supplierId) { toastr.warning("Please select a Supplier."); return; }
        if (_itemList.length === 0) { toastr.warning("Please add at least one product."); return; }

        var payload = {
            SupplierId: parseInt(supplierId),
            Date: $('#txtDate').val(),
            ExpectedDate: $('#txtExpectedDate').val(),
            Note: $('#txtNotes').val(),
            Items: _itemList.map(i => ({
                ProductVariantId: i.productVariantId,
                QuantityOrdered: i.quantity,
                UnitCost: i.unitCost
            }))
        };

        var res = await api.post('/api/Purchasing', payload);
        if (res && res.succeeded) {
            toastr.success(res.messages[0]);
            drawer.hide();
            table.ajax.reload();
        }
    };

    return {
        init: init,
        openCreatePanel: openCreatePanel,
        updateItem: updateItem,
        removeItem: removeItem,
        save: save
    };
})();

$(document).ready(function () { poApp.init(); });