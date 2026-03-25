window.invoiceApp = {
    _table: null,
    _modal: null,
    _products: [],

    init: function () {
        try {
            var modalEl = document.getElementById('invoiceModal');
            if (modalEl) {
                this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false });
            }

            // Set default dates
            document.getElementById('Date').valueAsDate = new Date();
            var dueDate = new Date();
            dueDate.setDate(dueDate.getDate() + 30); // Default Net 30
            document.getElementById('DueDate').valueAsDate = dueDate;

            this._initGrid();
            this._loadMasterData();

            // Global event listener for calculations
            $('#linesBody').on('input', '.calc-trigger', () => this.calculateTotals());
        } catch (e) {
            console.error("[InvoiceApp] Init Error:", e);
        }
    },

    _initGrid: function () {
        this._table = $('#invoiceGrid').DataTable({
            ajax: {
                url: '/api/sales/invoices',
                dataSrc: function (json) { return json.data || json || []; },
                headers: { "Authorization": "Bearer " + localStorage.getItem("jwtToken") }
            },
            columns: [
                { data: 'invoiceNumber', className: 'fw-bold text-primary font-monospace' },
                { data: 'invoiceDate', render: d => new Date(d).toLocaleDateString() },
                { data: 'dueDate', render: d => new Date(d).toLocaleDateString() },
                { data: 'customerName', className: 'fw-bold' },
                {
                    data: 'grandTotal',
                    className: 'text-end fw-bold text-dark',
                    render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 })
                },
                {
                    data: 'isPosted',
                    className: 'text-center',
                    render: d => d ? '<span class="badge bg-success">Posted</span>' : '<span class="badge bg-secondary">Draft</span>'
                }
            ],
            order: [[0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 20
        });
    },

    _loadMasterData: async function() {
        try {
            // 1. THE DESTRUCTURING FIX: Ensure 'empRes' is declared here to match the 4 API calls
            const [custRes, whRes, prodRes, empRes] = await Promise.all([
                api.get('/api/customer'),
                api.get('/api/masterdata/warehouses'), 
                api.get('/api/product'),
                api.get('/api/employee') // <-- The 4th API Call
            ]);

            // 2. THE SAFE EXTRACTION FIX: Prevents null crashes
            const customers = Array.isArray(custRes) ? custRes : (custRes?.data || []);
            const warehouses = Array.isArray(whRes) ? whRes : (whRes?.data || []);
            this._products = Array.isArray(prodRes) ? prodRes : (prodRes?.data || []);
            const employees = Array.isArray(empRes) ? empRes : (empRes?.data || []);

            // 3. POPULATE DROPDOWNS
            this._populateSelect('CustomerId', customers, 'id', 'name');
            this._populateSelect('WarehouseId', warehouses, 'id', 'name');

            if (employees.length > 0) {
                var salesReps = employees
                    .filter(e => e.isSalesRep === true)
                    .map(e => ({
                        id: e.id,
                        displayName: `[${e.employeeCode}] ${e.firstName} ${e.lastName}`
                    }));

                this._populateSelect('SalesRepId', salesReps, 'id', 'displayName');
            }

            // 4. INITIALIZE SELECT2
            if ($('#CustomerId').hasClass('select2-hidden-accessible')) {
                $('#CustomerId').select2('destroy');
            }
            $('#CustomerId').select2({ dropdownParent: $('#invoiceModal') });

        } catch (e) {
            console.error("[InvoiceApp] Lookup Error:", e);
        }
    },

    _populateSelect: function (id, data, valProp, textProp) {
        let $el = $(`#${id}`);
        $el.empty().append('<option value="">-- Select --</option>');
        if (data) {
            data.forEach(i => $el.append($('<option></option>').val(i[valProp]).text(i[textProp])));
        }
    },

    openCreateModal: function () {
        document.getElementById('invoiceForm').reset();
        $('#linesBody').empty();
        $('#CustomerId').val('').trigger('change');
        this.addLine(); // Add first empty line
        this.calculateTotals();
        if (this._modal) this._modal.show();
    },

    addLine: function() {
        const id = Date.now();
        let productOptions = '<option value="">-- Select Item --</option>';
        this._products.forEach(p => {
            if (p.variants && p.variants.length > 0) {
                productOptions += `<optgroup label="${p.name}">`;
                p.variants.forEach(v => {
                    let desc = v.sku;
                    if (v.size || v.color) desc += ` (${v.size || ''} ${v.color || ''})`;
                    // We also embed the Product Type to bypass stock checks on Services
                    productOptions += `<option value="${v.id}" data-price="${v.sellingPrice}" data-type="${p.type}">${desc}</option>`;
                });
                productOptions += `</optgroup>`;
            }
        });

        const html = `
            <tr id="row_${id}" data-absolute-discount="0" data-stock="999999">
                <td>
                    <select class="form-select form-select-sm line-variant" onchange="invoiceApp.onVariantSelect(this)">
                        ${productOptions}
                    </select>
                </td>
                <td>
                    <input type="number" class="form-control form-control-sm text-center line-qty calc-trigger" value="1" min="1">
                    <div class="stock-label text-center text-muted mt-1 fw-bold" style="font-size: 10px;">-</div>
                </td>
                <td><input type="number" class="form-control form-control-sm text-end line-price calc-trigger" value="0.00" step="0.01"></td>
                <td>
                    <div class="input-group input-group-sm">
                        <input type="number" class="form-control text-end line-discount-val calc-trigger" value="0" min="0" step="0.01">
                        <select class="form-select text-center line-discount-type calc-trigger" style="max-width: 55px;">
                            <option value="$">$</option>
                            <option value="%">%</option>
                        </select>
                    </div>
                </td>
                <td class="text-end fw-bold align-middle line-total">0.00</td>
                <td class="text-center align-middle">
                    <button type="button" class="btn btn-sm text-danger" onclick="$('#row_${id}').remove(); window.invoiceApp.calculateTotals();">
                        <i class="fa-solid fa-trash-can"></i>
                    </button>
                </td>
            </tr>`;
        $('#linesBody').append(html);
    },

    onWarehouseChange: function() {
        var linesCount = $('#linesBody tr').length;
        if (linesCount > 0) {
            toastr.info("Warehouse changed. All line items have been reset to re-evaluate available stock.");
            $('#linesBody').empty();
            this.addLine();
            this.calculateTotals();
        }
    },

    onVariantSelect: async function(selectEl) {
        const selectedOption = $(selectEl).find('option:selected');
        const price = selectedOption.data('price') || 0;
        const pType = selectedOption.data('type'); // 1 = Standard, 2 = Service
        const varId = $(selectEl).val();
        const row = $(selectEl).closest('tr');
        const stockLabel = row.find('.stock-label');
        const qtyInput = row.find('.line-qty');

        row.find('.line-price').val(parseFloat(price).toFixed(2));

        if (varId) {
            const warehouseId = $('#WarehouseId').val();
            if (!warehouseId) {
                toastr.warning("Please select a Dispatch Warehouse first.");
                $(selectEl).val('');
                return;
            }

            // If it is a "Service" (Enum value 2), bypass stock check
            if (pType == 2 || pType === "Service") {
                row.attr('data-stock', 999999);
                stockLabel.html('<span class="text-primary">Service (No Stock)</span>');
                qtyInput.attr('max', 999999);
            } 
            else {
                // It is a physical good, fetch real-time stock
                stockLabel.html('<i class="spinner-border spinner-border-sm text-secondary" style="width: 10px; height: 10px;"></i>');
                
                try {
                    const res = await api.get(`/api/inventory/stock/available?variantId=${varId}&warehouseId=${warehouseId}`);
                    const available = res.data !== undefined ? res.data : (res || 0);

                    row.attr('data-stock', available);
                    
                    if (available <= 0) {
                        stockLabel.html('<span class="text-danger"><i class="fa-solid fa-triangle-exclamation"></i> Out of Stock</span>');
                        qtyInput.val(0);
                    } else {
                        stockLabel.html(`<span class="text-success"><i class="fa-solid fa-box"></i> Avail: ${available}</span>`);
                        qtyInput.attr('max', available);
                        if (qtyInput.val() == 0) qtyInput.val(1);
                    }
                } catch (e) {
                    console.error("Stock fetch error", e);
                    stockLabel.html('<span class="text-danger">Error</span>');
                }
            }
        } else {
            row.attr('data-stock', 999999);
            stockLabel.html('-');
        }

        this.calculateTotals();
    },

    calculateTotals: function() {
        let subTotal = 0;

        $('#linesBody tr').each(function() {
            const qtyInput = $(this).find('.line-qty');
            let qty = parseFloat(qtyInput.val()) || 0;
            const availableStock = parseFloat($(this).attr('data-stock')) || 0;

            // ENTERPRISE GUARD: Enforce maximum quantity based on physical stock
            if (qty > availableStock) {
                toastr.warning(`Quantity exceeds available physical stock (${availableStock}).`);
                qty = availableStock;
                qtyInput.val(qty); // Auto-correct their mistake
            }

            const price = parseFloat($(this).find('.line-price').val()) || 0;
            const discVal = parseFloat($(this).find('.line-discount-val').val()) || 0;
            const discType = $(this).find('.line-discount-type').val();

            let lineGross = qty * price;
            let lineDiscAmount = discType === '%' ? (lineGross * (discVal / 100)) : discVal;

            if (lineDiscAmount > lineGross) lineDiscAmount = lineGross;

            let lineNet = lineGross - lineDiscAmount;

            $(this).find('.line-total').text(lineNet.toFixed(2));
            $(this).attr('data-absolute-discount', lineDiscAmount); 

            subTotal += lineNet;
        });

        // ... (Rest of your existing calculateTotals code remains exactly the same) ...
        const globDiscVal = parseFloat($('#GlobalDiscountVal').val()) || 0;
        const globDiscType = $('#GlobalDiscountType').val();
        let globDiscAmount = globDiscType === '%' ? (subTotal * (globDiscVal / 100)) : globDiscVal;
        if (globDiscAmount > subTotal) globDiscAmount = subTotal;

        let taxableAmount = subTotal - globDiscAmount;
        let applyVat = $('#ApplyVat').is(':checked');

        let tax = applyVat ? (taxableAmount * 0.18) : 0; 
        let grandTotal = taxableAmount + tax;

        if (applyVat) {
            $('#vatRow').show();
        } else {
            $('#vatRow').hide();
        }

        $('#lblSubTotal').text(subTotal.toLocaleString(undefined, { minimumFractionDigits: 2 }));
        $('#lblTax').text('+ ' + tax.toLocaleString(undefined, { minimumFractionDigits: 2 }));
        $('#lblGrandTotal').text(grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2 }));

        $('#invoiceForm').data('calculated-global-discount', globDiscAmount);
    },

    saveInvoice: async function(isDraft) {
        var form = document.getElementById('invoiceForm');
        
        // 1. SAFE PARSING: Extract values safely from Select2
        var customerId = parseInt($('#CustomerId').val()) || 0;
        var warehouseId = parseInt($('#WarehouseId').val()) || 0;
        var salesRepId = parseInt($('#SalesRepId').val()) || null;

        // 2. EXPLICIT VALIDATION: Catch empty dropdowns before the browser does
        if (customerId === 0) {
            toastr.warning("Please select a Customer.");
            $('#CustomerId').select2('open'); // Auto-open the dropdown for them
            return;
        }
        if (warehouseId === 0) {
            toastr.warning("Please select a Dispatch Warehouse.");
            $('#WarehouseId').focus();
            return;
        }

        // 3. NATIVE VALIDATION: For textboxes and dates
        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        // 4. PAYLOAD CONSTRUCTION
        const payload = {
            Invoice: {
                Date: $('#Date').val(),
                DueDate: $('#DueDate').val(),
                CustomerId: customerId,
                WarehouseId: warehouseId,
                SalesRepId: salesRepId,
                Notes: $('#Notes').val(),
                ApplyVat: $('#ApplyVat').is(':checked'),
                IsDraft: isDraft,
                GlobalDiscountAmount: parseFloat($('#invoiceForm').data('calculated-global-discount')) || 0,
                Items: []
            }
        };

        $('#linesBody tr').each(function() {
            const varId = $(this).find('.line-variant').val();
            if (varId) {
                payload.Invoice.Items.push({
                    ProductVariantId: parseInt(varId),
                    Quantity: parseFloat($(this).find('.line-qty').val()) || 0,
                    UnitPrice: parseFloat($(this).find('.line-price').val()) || 0,
                    Discount: parseFloat($(this).attr('data-absolute-discount')) || 0
                });
            }
        });

        if (payload.Invoice.Items.length === 0) {
            toastr.warning("Please add at least one item to the invoice.");
            return;
        }

        // Disable button to prevent double-submits
        var $btn = $(event.currentTarget);
        var originalText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Processing...');

        try {
            const res = await api.post('/api/sales/invoices', payload);

            if (res && res.succeeded) {
                toastr.success(res.message || "Invoice saved successfully.");
                this._modal.hide();
                this._table.ajax.reload(null, false);
            }
        } catch (e) {
            console.error("Save Invoice Error:", e);
        } finally {
            $btn.prop('disabled', false).html(originalText);
        }
    }
};

$(document).ready(function () {
    window.invoiceApp.init();
});