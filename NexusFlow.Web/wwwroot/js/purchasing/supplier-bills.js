window.billApp = {
    _table: null,
    _modal: null,
    _products: [],
    _accounts: [],
    _unbilledGrns: [],

    init: function () {
        try {
            var modalEl = document.getElementById('billModal');
            if (modalEl) this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });

            this._initGrid();
            this._loadMasterData();

            $('#linesBody').on('input', '.calc-trigger', () => this.calculateTotals());
            $('#ApplyVat').on('change', () => this.calculateTotals());

            const self = this;

            // 1. Fetch GRNs when Supplier changes
            $('#SupplierId').on('change', function () {
                const suppId = $(this).val();
                if (suppId) self._loadUnbilledGrns(suppId);
                else $('#LinkedGrnIds').empty().trigger('change');
            });

            // 2. Auto-Populate Lines when GRNs are selected
            $('#LinkedGrnIds').on('change', function () {
                self._populateLinesFromGrns();
            });

        } catch (e) {
            console.error("Init Error:", e);
        }
    },

    _initGrid: function () {
        this._table = $('#billGrid').DataTable({
            ajax: {
                url: '/api/purchasing/supplier-bills',
                dataSrc: function (json) { return json.data || json || []; },
                headers: { "Authorization": "Bearer " + localStorage.getItem("jwtToken") }
            },
            columns: [
                { data: 'billNumber', className: 'fw-bold text-primary font-monospace' },
                { data: 'billDate', render: d => new Date(d).toLocaleDateString() },
                { data: 'dueDate', render: d => new Date(d).toLocaleDateString() },
                { data: 'supplierName', className: 'fw-bold' },
                { data: 'supplierInvoiceNo', className: 'font-monospace text-muted' },
                { data: 'grandTotal', className: 'text-end fw-bold text-dark', render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                {
                    data: null,
                    className: 'text-center',
                    render: function (d) {
                        if (!d.isPosted) return '<span class="badge bg-secondary">Draft</span>';
                        if (d.paymentStatus === 'Paid') return '<span class="badge bg-success">Paid</span>';
                        if (d.paymentStatus === 'Partial') return '<span class="badge bg-warning text-dark">Partial</span>';
                        return '<span class="badge bg-danger">Unpaid</span>';
                    }
                }
            ],
            order: [[1, 'desc'], [0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 20
        });
    },

    _loadMasterData: async function () {
        try {
            const [suppRes, prodRes, accRes] = await Promise.all([
                api.get('/api/supplier'),
                api.get('/api/product'),
                api.get('/api/finance/accounts')
            ]);

            const suppliers = suppRes.data || suppRes;
            this._products = prodRes.data || prodRes;
            // Cache Expense Accounts only
            this._accounts = (accRes.data || accRes).filter(a => a.type === 'Expense' || a.type === '5');

            let suppEl = $('#SupplierId');
            suppEl.empty().append('<option value="">-- Select Supplier --</option>');
            suppliers.forEach(s => suppEl.append(`<option value="${s.id}">${s.name}</option>`));
            suppEl.select2({ dropdownParent: $('#billModal') });

        } catch (e) {
            console.error("Lookup Error:", e);
        }
    },

    _loadUnbilledGrns: async function (supplierId) {
        try {
            const res = await api.get(`/api/purchasing/suppliers/${supplierId}/unbilled-grns`);
            this._unbilledGrns = res.data || res;

            let grnEl = $('#LinkedGrnIds');
            grnEl.empty();

            if (this._unbilledGrns.length === 0) {
                toastr.info("No unbilled GRNs found for this supplier.");
            } else {
                this._unbilledGrns.forEach(g => {
                    grnEl.append(`<option value="${g.id}">[${g.grnNumber}] - ${new Date(g.receivedDate).toLocaleDateString()} - Rs. ${g.totalAmount}</option>`);
                });
            }
            grnEl.select2({ dropdownParent: $('#billModal'), placeholder: "Select GRNs..." });

        } catch (e) {
            console.error("Error loading GRNs", e);
        }
    },

    _populateLinesFromGrns: function () {
        // Clear ONLY Product lines (keep manual expense lines intact)
        $('#linesBody tr').each(function () {
            if ($(this).find('.line-type').val() === 'Product') {
                $(this).remove();
            }
        });

        const selectedGrnIds = $('#LinkedGrnIds').val() || [];

        selectedGrnIds.forEach(id => {
            const grn = this._unbilledGrns.find(g => g.id == id);
            if (grn && grn.lines) {
                grn.lines.forEach(line => {
                    this._addAutoLine(line.productVariantId, line.productName, line.sku, line.quantityReceived, line.unitCost);
                });
            }
        });

        this.calculateTotals();
    },

    _addAutoLine: function (variantId, name, sku, qty, cost) {
        const id = Date.now() + Math.floor(Math.random() * 1000);

        // This line is locked (readonly) because it came from the warehouse physical count
        const html = `
            <tr id="row_${id}" class="bg-light">
                <td>
                    <input type="text" class="form-control form-control-sm line-type bg-light text-muted" value="Product" readonly disabled>
                </td>
                <td>
                    <div class="fw-bold">${name}</div>
                    <div class="small font-monospace text-muted">${sku}</div>
                    <input type="hidden" class="product-select" value="${variantId}">
                </td>
                <td><input type="text" class="form-control form-control-sm line-desc bg-light" value="Auto-pulled from GRN" readonly></td>
                <td><input type="number" class="form-control form-control-sm text-center line-qty calc-trigger bg-light" value="${qty}" readonly></td>
                <td><input type="number" class="form-control form-control-sm text-end line-price calc-trigger bg-light" value="${cost}" readonly></td>
                <td class="text-end fw-bold align-middle line-total text-primary">LKR ${(qty * cost).toFixed(2)}</td>
                <td class="text-center align-middle">
                    <i class="fa-solid fa-lock text-muted" title="Locked to GRN"></i>
                </td>
            </tr>`;
        $('#linesBody').prepend(html);
    },

    openModal: function () {
        document.getElementById('billForm').reset();
        $('#linesBody').empty();

        document.getElementById('BillDate').valueAsDate = new Date();
        var dueDate = new Date(); dueDate.setDate(dueDate.getDate() + 30);
        document.getElementById('DueDate').valueAsDate = dueDate;

        $('#SupplierId').val('').trigger('change');
        this.addLine();
        this.calculateTotals();
        if (this._modal) this._modal.show();
    },

    addLine: function () {
        const id = Date.now();

        // Build Option strings
        let productOpts = '<option value="">-- Select Product (Clears GRN) --</option>';
        this._products.forEach(p => {
            if (p.variants) {
                p.variants.forEach(v => {
                    productOpts += `<option value="${v.id}">${p.name} - ${v.sku}</option>`;
                });
            }
        });

        let accountOpts = '<option value="">-- Select Direct Expense Account --</option>';
        this._accounts.forEach(a => {
            accountOpts += `<option value="${a.id}">[${a.code}] ${a.name}</option>`;
        });

        const html = `
            <tr id="row_${id}">
                <td>
                    <select class="form-select form-select-sm line-type" onchange="billApp.onTypeChange(this, ${id})">
                        <option value="Product">Product (from GRN)</option>
                        <option value="Expense">Direct Expense</option>
                    </select>
                </td>
                <td id="target_${id}">
                    <select class="form-select form-select-sm line-target product-select">${productOpts}</select>
                    <select class="form-select form-select-sm line-target account-select" style="display:none;" disabled>${accountOpts}</select>
                </td>
                <td><input type="text" class="form-control form-control-sm line-desc"></td>
                <td><input type="number" class="form-control form-control-sm text-center line-qty calc-trigger" value="1" min="0.01" step="0.01"></td>
                <td><input type="number" class="form-control form-control-sm text-end line-price calc-trigger" value="0.00" step="0.01"></td>
                <td class="text-end fw-bold align-middle line-total">0.00</td>
                <td class="text-center align-middle">
                    <button type="button" class="btn btn-sm text-danger" onclick="$('#row_${id}').remove(); window.billApp.calculateTotals();"><i class="fa-solid fa-trash-can"></i></button>
                </td>
            </tr>`;
        $('#linesBody').append(html);
    },

    onTypeChange: function (selectEl, id) {
        const type = $(selectEl).val();
        const targetTd = $(`#target_${id}`);

        if (type === 'Product') {
            targetTd.find('.product-select').show().prop('disabled', false);
            targetTd.find('.account-select').hide().prop('disabled', true).val('');
        } else {
            targetTd.find('.product-select').hide().prop('disabled', true).val('');
            targetTd.find('.account-select').show().prop('disabled', false);
        }
    },

    calculateTotals: function () {
        let subTotal = 0;
        $('#linesBody tr').each(function () {
            const qty = parseFloat($(this).find('.line-qty').val()) || 0;
            const price = parseFloat($(this).find('.line-price').val()) || 0;
            const lineTotal = qty * price;
            $(this).find('.line-total').text(lineTotal.toFixed(2));
            subTotal += lineTotal;
        });

        const applyVat = $('#ApplyVat').is(':checked');
        const tax = applyVat ? (subTotal * 0.18) : 0;
        const grandTotal = subTotal + tax;

        applyVat ? $('#vatRow').show() : $('#vatRow').hide();
        $('#lblSubTotal').text(subTotal.toLocaleString(undefined, { minimumFractionDigits: 2 }));
        $('#lblTax').text('+ ' + tax.toLocaleString(undefined, { minimumFractionDigits: 2 }));
        $('#lblGrandTotal').text(grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2 }));
    },

    save: async function (isDraft) {
        var form = document.getElementById('billForm');
        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        const payload = {
            Bill: {
                BillDate: $('#BillDate').val(),
                DueDate: $('#DueDate').val(),
                SupplierId: parseInt($('#SupplierId').val()),
                SupplierInvoiceNo: $('#SupplierInvoiceNo').val(),
                Remarks: $('#Remarks').val(),
                ApplyVat: $('#ApplyVat').is(':checked'),
                IsDraft: isDraft,
                LinkedGrnIds: $('#LinkedGrnIds').val().map(id => parseInt(id)), // Attach the GRN IDs!
                Items: []
            }
        };

        $('#linesBody tr').each(function () {
            const type = $(this).find('.line-type').val();
            let prodId = null, accId = null;

            if (type === 'Product') {
                prodId = parseInt($(this).find('.product-select').val());
            } else {
                accId = parseInt($(this).find('.account-select').val());
            }

            if (prodId || accId) {
                payload.Bill.Items.push({
                    ProductVariantId: prodId || null,
                    ExpenseAccountId: accId || null,
                    Description: $(this).find('.line-desc').val(),
                    Quantity: parseFloat($(this).find('.line-qty').val()) || 0,
                    UnitPrice: parseFloat($(this).find('.line-price').val()) || 0
                });
            }
        });

        if (payload.Bill.Items.length === 0) {
            toastr.warning("Add at least one valid item or expense.");
            return;
        }

        const res = await api.post('/api/purchasing/supplier-bills', payload);

        if (res && res.succeeded) {
            toastr.success(res.messages ? res.messages[0] : "Supplier Bill Saved!");
            this._modal.hide();
            this._table.ajax.reload(null, false);
        }
    }
};

$(document).ready(function () { window.billApp.init(); });