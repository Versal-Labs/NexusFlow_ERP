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

    _loadMasterData: async function () {
        try {
            const [custRes, whRes, prodRes] = await Promise.all([
                api.get('/api/customer'),
                api.get('/api/masterdata/warehouses'), // Assuming you have this
                api.get('/api/product')
            ]);

            const customers = custRes.data || custRes;
            const warehouses = whRes.data || whRes;
            this._products = prodRes.data || prodRes; // Cache products for dropdowns

            this._populateSelect('CustomerId', customers, 'id', 'name');
            this._populateSelect('WarehouseId', warehouses, 'id', 'name');

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

    addLine: function () {
        const id = Date.now();
        let productOptions = '<option value="">-- Select Item --</option>';
        this._products.forEach(p => {
            if (p.variants && p.variants.length > 0) {
                productOptions += `<optgroup label="${p.name}">`;
                p.variants.forEach(v => {
                    let desc = v.sku;
                    if (v.size || v.color) desc += ` (${v.size || ''} ${v.color || ''})`;
                    productOptions += `<option value="${v.id}" data-price="${v.sellingPrice}">${desc}</option>`;
                });
                productOptions += `</optgroup>`;
            }
        });

        // Notice the input-group for Line Discount ($ vs %)
        const html = `
            <tr id="row_${id}" data-absolute-discount="0">
                <td>
                    <select class="form-select form-select-sm line-variant" onchange="invoiceApp.onVariantSelect(this)">
                        ${productOptions}
                    </select>
                </td>
                <td><input type="number" class="form-control form-control-sm text-center line-qty calc-trigger" value="1" min="1"></td>
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

    onVariantSelect: function (selectEl) {
        const selectedOption = $(selectEl).find('option:selected');
        const price = selectedOption.data('price') || 0;
        const row = $(selectEl).closest('tr');
        row.find('.line-price').val(parseFloat(price).toFixed(2));
        this.calculateTotals();
    },

    calculateTotals: function () {
        let subTotal = 0;

        // 1. Calculate Lines
        $('#linesBody tr').each(function () {
            const qty = parseFloat($(this).find('.line-qty').val()) || 0;
            const price = parseFloat($(this).find('.line-price').val()) || 0;
            const discVal = parseFloat($(this).find('.line-discount-val').val()) || 0;
            const discType = $(this).find('.line-discount-type').val();

            let lineGross = qty * price;
            // Convert % to absolute Amount
            let lineDiscAmount = discType === '%' ? (lineGross * (discVal / 100)) : discVal;

            // Prevent discount exceeding price
            if (lineDiscAmount > lineGross) lineDiscAmount = lineGross;

            let lineNet = lineGross - lineDiscAmount;

            $(this).find('.line-total').text(lineNet.toFixed(2));
            $(this).attr('data-absolute-discount', lineDiscAmount); // Store absolute value for the backend

            subTotal += lineNet;
        });

        // 2. Calculate Global Discount
        const globDiscVal = parseFloat($('#GlobalDiscountVal').val()) || 0;
        const globDiscType = $('#GlobalDiscountType').val();
        let globDiscAmount = globDiscType === '%' ? (subTotal * (globDiscVal / 100)) : globDiscVal;
        if (globDiscAmount > subTotal) globDiscAmount = subTotal;

        // 3. Tax & Grand Total
        let taxableAmount = subTotal - globDiscAmount;
        let applyVat = $('#ApplyVat').is(':checked');

        let tax = applyVat ? (taxableAmount * 0.18) : 0; // 18% Estimate
        let grandTotal = taxableAmount + tax;

        // Update UI
        if (applyVat) {
            $('#vatRow').show();
        } else {
            $('#vatRow').hide();
        }

        $('#lblSubTotal').text(subTotal.toLocaleString(undefined, { minimumFractionDigits: 2 }));
        $('#lblTax').text('+ ' + tax.toLocaleString(undefined, { minimumFractionDigits: 2 }));
        $('#lblGrandTotal').text(grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2 }));

        // Store calculated global discount for backend
        $('#invoiceForm').data('calculated-global-discount', globDiscAmount);
    },

    saveInvoice: async function (isDraft) {
        var form = document.getElementById('invoiceForm');
        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        const payload = {
            Invoice: {
                Date: $('#Date').val(),
                DueDate: $('#DueDate').val(),
                CustomerId: parseInt($('#CustomerId').val()),
                WarehouseId: parseInt($('#WarehouseId').val()),
                SalesRepId: parseInt($('#SalesRepId').val()) || null,
                Notes: $('#Notes').val(),
                ApplyVat: $('#ApplyVat').is(':checked'),
                IsDraft: isDraft,
                GlobalDiscountAmount: parseFloat($('#invoiceForm').data('calculated-global-discount')) || 0,
                Items: []
            }
        };

        $('#linesBody tr').each(function () {
            const varId = $(this).find('.line-variant').val();
            if (varId) {
                payload.Invoice.Items.push({
                    ProductVariantId: parseInt(varId),
                    Quantity: parseFloat($(this).find('.line-qty').val()) || 0,
                    UnitPrice: parseFloat($(this).find('.line-price').val()) || 0,
                    Discount: parseFloat($(this).attr('data-absolute-discount')) || 0 // Fetch absolute computed discount
                });
            }
        });

        if (payload.Invoice.Items.length === 0) {
            toastr.warning("Please add at least one item to the invoice.");
            return;
        }

        const res = await api.post('/api/sales/invoices', payload);

        if (res && res.succeeded) {
            toastr.success(res.messages ? res.messages[0] : "Saved successfully.");
            this._modal.hide();
            this._table.ajax.reload(null, false);
        }
    }
};

$(document).ready(function () {
    window.invoiceApp.init();
});