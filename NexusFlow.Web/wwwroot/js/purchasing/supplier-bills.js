window.billApp = {
    _table: null,
    _modal: null,
    _viewModal: null,
    _products: [],
    _accounts: [],
    _unbilledGrns: [],
    _currentDocId: null,

    init: function () {
        try {
            var modalEl = document.getElementById('billModal');
            if (modalEl) this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false });

            var viewEl = document.getElementById('billViewModal');
            if (viewEl) this._viewModal = new bootstrap.Modal(viewEl);

            this._initFilters();
            this._initGrid();
            this._loadMasterData();

            $('#linesBody').on('input', '.calc-trigger', () => this.calculateTotals());
            $('#ApplyVat').on('change', () => this.calculateTotals());

            const self = this;

            $('#SupplierId').on('change', function () {
                const suppId = $(this).val();
                if (suppId) self._loadUnbilledGrns(suppId);
                else $('#LinkedGrnIds').empty().trigger('change');
            });

            $('#LinkedGrnIds').on('change', function () {
                self._populateLinesFromGrns();
            });

            // Action Buttons
            $('#btnModalPay').click(() => {
                // Route to the Treasury Payment screen, auto-filling the Bill ID
                window.location.href = `/Finance/Treasury/Payments?billId=${this._currentDocId}`;
            });

            // Deep link handler (If redirected from GRN screen)
            const grnIdFromUrl = new URLSearchParams(window.location.search).get('grnId');
            if (grnIdFromUrl) {
                this.openModal();
                // Note: To make this fully seamless, you'd fetch the GRN's Supplier, set it, then set LinkedGrnIds.
                window.history.replaceState({}, document.title, window.location.pathname);
            }

        } catch (e) {
            console.error("Init Error:", e);
        }
    },

    _initFilters: function () {
        api.get('/api/Supplier').then(res => {
            let suppliers = Array.isArray(res) ? res : (res?.data || []);
            let $sup = $('#filterSupplier').empty().append('<option value="">All Suppliers</option>');
            suppliers.forEach(s => $sup.append($('<option></option>').val(s.name).text(s.name)));
        }).catch(err => console.error(err));

        $('#filterSupplier, #filterStatus, #filterStartDate, #filterEndDate').on('change', () => this.reloadGrid());

        $.fn.dataTable.ext.search.push((settings, data, dataIndex) => {
            if (settings.nTable.id !== 'billGrid') return true;

            const filterSup = ($('#filterSupplier').val() || '').toLowerCase();
            const filterStatus = ($('#filterStatus').val() || '').toLowerCase();
            const filterStart = $('#filterStartDate').val();
            const filterEnd = $('#filterEndDate').val();

            const rowDateStr = data[1] || '';
            const rowSup = (data[3] || '').toLowerCase();
            const rowStatusHTML = (data[8] || '').toLowerCase(); // Document/Payment Status column

            if (filterSup && !rowSup.includes(filterSup)) return false;

            if (filterStatus) {
                if (filterStatus === 'draft' && !rowStatusHTML.includes('draft')) return false;
                if (filterStatus === 'unpaid' && !rowStatusHTML.includes('unpaid')) return false;
                if (filterStatus === 'partial' && !rowStatusHTML.includes('partial')) return false;
                if (filterStatus === 'paid' && !rowStatusHTML.includes('paid')) return false;
            }

            if (filterStart || filterEnd) {
                if (!rowDateStr) return false;
                const rowDate = new Date(rowDateStr);
                if (filterStart && rowDate < new Date(filterStart)) return false;
                if (filterEnd && rowDate > new Date(filterEnd + 'T23:59:59')) return false;
            }
            return true;
        });
    },

    resetFilters: function () {
        $('#filterSupplier, #filterStatus, #filterStartDate, #filterEndDate').val('');
        this.reloadGrid();
    },

    reloadGrid: function () { this._table.ajax.reload(); },

    _initGrid: function () {
        this._table = $('#billGrid').DataTable({
            ajax: function (data, callback, settings) {
                (async () => {
                    try {
                        const response = await api.get('/api/purchasing/supplier-bills');
                        callback({ data: response.data || response || [] });
                    } catch (e) { callback({ data: [] }); }
                })();
                return { abort: function () { } };
            },
            columns: [
                { data: 'billNumber', className: 'fw-bold text-primary font-monospace' },
                { data: 'billDate', render: d => new Date(d).toLocaleDateString() },
                { data: 'dueDate', render: d => new Date(d).toLocaleDateString() },
                { data: 'supplierName', className: 'fw-bold' },
                { data: 'supplierInvoiceNo', className: 'font-monospace text-muted' },
                { data: 'grandTotal', className: 'text-end fw-bold text-dark', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'amountPaid', className: 'text-end fw-bold text-success', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                {
                    data: null, className: 'text-end fw-bold text-danger',
                    render: function (data, type, row) {
                        let bal = (parseFloat(row.grandTotal) || 0) - (parseFloat(row.amountPaid) || 0);
                        return bal.toLocaleString(undefined, { minimumFractionDigits: 2 });
                    }
                },
                {
                    data: null, className: 'text-center',
                    render: function (data, type, row) {
                        if (!row.isPosted) return '<span class="badge bg-secondary">Draft</span>';
                        if (row.paymentStatus === 'Paid') return '<span class="badge bg-success">Paid</span>';
                        if (row.paymentStatus === 'Partial') return '<span class="badge bg-info text-dark">Partial</span>';
                        return '<span class="badge bg-warning text-dark">Unpaid</span>';
                    }
                },
                {
                    data: null, className: 'text-end pe-3', orderable: false,
                    render: function (data, type, row) {
                        let btns = `<button class="btn btn-sm btn-outline-dark shadow-sm me-1" onclick="billApp.viewDocument(${row.id})" title="View Document"><i class="fa-solid fa-eye"></i></button>`;

                        // TIER-1: Edit Drafts
                        if (!row.isPosted) {
                            btns += `<button class="btn btn-sm btn-outline-primary shadow-sm me-1" onclick="billApp.edit(${row.id})" title="Edit Draft"><i class="fa-solid fa-pen-to-square"></i></button>`;
                        }

                        // TIER-1: Pay Bill Routing
                        if (row.isPosted && row.paymentStatus !== 'Paid') {
                            btns += `<a href="/Finance/Treasury/Payments?billId=${row.id}" class="btn btn-sm btn-success shadow-sm" title="Record Payment"><i class="fa-solid fa-money-bill-transfer"></i></a>`;
                        }
                        return btns;
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

            const suppliers = suppRes.data || suppRes || [];
            this._products = prodRes.data || prodRes || [];

            // Cache Expense Accounts only
            const allAccounts = accRes.data || accRes || [];
            this._accounts = allAccounts.filter(a => a.type === 'Expense' || a.type === '5');

            let suppEl = $('#SupplierId');
            suppEl.empty().append('<option value="">-- Select Supplier --</option>');
            suppliers.forEach(s => suppEl.append(`<option value="${s.id}">${s.name}</option>`));
            suppEl.select2({ dropdownParent: $('#billModal') });

        } catch (e) { console.error("Lookup Error:", e); }
    },

    _loadUnbilledGrns: async function (supplierId) {
        try {
            const res = await api.get(`/api/purchasing/suppliers/${supplierId}/unbilled-grns`);
            this._unbilledGrns = res.data || res || [];

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

        } catch (e) { console.error("Error loading GRNs", e); }
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

        const html = `
            <tr id="row_${id}" class="bg-light">
                <td>
                    <select class="form-select form-select-sm line-type bg-light text-muted" disabled>
                        <option value="Product">Product (GRN)</option>
                    </select>
                </td>
                <td>
                    <div class="fw-bold">${name}</div>
                    <div class="small font-monospace text-muted">${sku}</div>
                    <input type="hidden" class="product-select" value="${variantId}">
                </td>
                <td><input type="text" class="form-control form-control-sm line-desc bg-light" value="Auto-pulled from GRN" readonly></td>
                <td><input type="number" class="form-control form-control-sm text-center line-qty calc-trigger bg-light" value="${qty}" readonly></td>
                <td><input type="number" class="form-control form-control-sm text-end line-price calc-trigger bg-light" value="${cost}" readonly></td>
                <td class="text-end fw-bold align-middle line-total text-primary">${(qty * cost).toFixed(2)}</td>
                <td class="text-center align-middle">
                    <i class="fa-solid fa-lock text-muted" title="Locked to GRN"></i>
                </td>
            </tr>`;
        $('#linesBody').prepend(html);
    },

    openModal: function () {
        document.getElementById('billForm').reset();
        $('#BillId').val(0);
        $('#linesBody').empty();

        document.getElementById('BillDate').valueAsDate = new Date();
        var dueDate = new Date(); dueDate.setDate(dueDate.getDate() + 30);
        document.getElementById('DueDate').valueAsDate = dueDate;

        $('#SupplierId').val('').trigger('change');
        this.calculateTotals();
        if (this._modal) this._modal.show();
    },

    // TIER-1 FEATURE: Edit Drafts
    edit: async function (id) {
        try {
            const res = await api.get(`/api/purchasing/supplier-bills/${id}`);
            const bill = res.data || res;

            $('#billForm')[0].reset();
            $('#linesBody').empty();

            $('#BillId').val(bill.id);

            // Set Date safely
            if (bill.billDate) $('#BillDate').val(bill.billDate.split('T')[0]);
            if (bill.dueDate) $('#DueDate').val(bill.dueDate.split('T')[0]);
            $('#SupplierInvoiceNo').val(bill.supplierInvoiceNo);
            $('#Remarks').val(bill.remarks);
            $('#ApplyVat').prop('checked', bill.applyVat);

            // Temporarily unbind Supplier Change so it doesn't wipe GRNs
            $('#SupplierId').off('change');
            $('#SupplierId').val(bill.supplierId).trigger('change');

            // Re-bind it after setting
            const self = this;
            setTimeout(() => {
                $('#SupplierId').on('change', function () {
                    const suppId = $(this).val();
                    if (suppId) self._loadUnbilledGrns(suppId);
                    else $('#LinkedGrnIds').empty().trigger('change');
                });
            }, 500);

            // Rebuild lines (Needs backend to return the exact lines)
            if (bill.items && bill.items.length > 0) {
                bill.items.forEach(item => {
                    this.addLine(item); // Uses modified addLine logic
                });
            }

            this.calculateTotals();
            this._modal.show();
        } catch (e) {
            toastr.error("Could not load Bill for editing.");
        }
    },

    addLine: function (existingItem = null) {
        const id = Date.now() + Math.floor(Math.random() * 1000);

        let accountOpts = '<option value="">-- Select Direct Expense Account --</option>';
        this._accounts.forEach(a => {
            let sel = (existingItem && existingItem.expenseAccountId === a.id) ? 'selected' : '';
            accountOpts += `<option value="${a.id}" ${sel}>[${a.code}] ${a.name}</option>`;
        });

        // For Products not locked to GRN (rare in strict 3-way match, but possible)
        let productOpts = '<option value="">-- Select Product --</option>';
        this._products.forEach(p => {
            if (p.variants) {
                p.variants.forEach(v => {
                    let sel = (existingItem && existingItem.productVariantId === v.id) ? 'selected' : '';
                    productOpts += `<option value="${v.id}" ${sel}>${p.name} - ${v.sku}</option>`;
                });
            }
        });

        let isExp = existingItem ? (existingItem.expenseAccountId != null) : true;
        let qty = existingItem ? existingItem.quantity : 1;
        let price = existingItem ? existingItem.unitPrice : 0;
        let desc = existingItem ? existingItem.description : '';

        const html = `
            <tr id="row_${id}">
                <td>
                    <select class="form-select form-select-sm line-type" onchange="billApp.onTypeChange(this, ${id})">
                        <option value="Expense" ${isExp ? 'selected' : ''}>Direct Expense</option>
                        <option value="Product" ${!isExp ? 'selected' : ''}>Product Item</option>
                    </select>
                </td>
                <td id="target_${id}">
                    <select class="form-select form-select-sm line-target account-select select2-init" style="${isExp ? '' : 'display:none;'}">${accountOpts}</select>
                    <select class="form-select form-select-sm line-target product-select select2-init" style="${!isExp ? '' : 'display:none;'}">${productOpts}</select>
                </td>
                <td><input type="text" class="form-control form-control-sm line-desc" value="${desc}"></td>
                <td><input type="number" class="form-control form-control-sm text-center line-qty calc-trigger" value="${qty}" min="0.01" step="0.01" onclick="this.select()"></td>
                <td><input type="number" class="form-control form-control-sm text-end line-price calc-trigger" value="${price}" step="0.01" onclick="this.select()"></td>
                <td class="text-end fw-bold align-middle line-total">0.00</td>
                <td class="text-center align-middle">
                    <button type="button" class="btn btn-sm text-danger" onclick="$('#row_${id}').remove(); window.billApp.calculateTotals();"><i class="fa-solid fa-trash-can"></i></button>
                </td>
            </tr>`;

        const newRow = $(html);
        $('#linesBody').append(newRow);

        newRow.find('.select2-init').select2({ dropdownParent: $('#billModal'), width: '100%' });
    },

    onTypeChange: function (selectEl, id) {
        const type = $(selectEl).val();
        const targetTd = $(`#target_${id}`);

        if (type === 'Product') {
            targetTd.find('.product-select').next('.select2-container').show();
            targetTd.find('.account-select').next('.select2-container').hide();
        } else {
            targetTd.find('.product-select').next('.select2-container').hide();
            targetTd.find('.account-select').next('.select2-container').show();
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
        if (!form.checkValidity()) { form.reportValidity(); return; }

        var billId = parseInt($('#BillId').val()) || 0; // Grab ID

        const payload = {
            Bill: {
                Id: billId,
                BillDate: $('#BillDate').val(),
                DueDate: $('#DueDate').val(),
                SupplierId: parseInt($('#SupplierId').val()),
                SupplierInvoiceNo: $('#SupplierInvoiceNo').val(),
                Remarks: $('#Remarks').val(),
                ApplyVat: $('#ApplyVat').is(':checked'),
                IsDraft: isDraft,
                LinkedGrnIds: $('#LinkedGrnIds').val() ? $('#LinkedGrnIds').val().map(id => parseInt(id)) : [],
                Items: []
            }
        };

        $('#linesBody tr').each(function () {
            const type = $(this).find('.line-type').val();
            let prodId = null, accId = null;

            if (type === 'Product' || $(this).find('.line-type').length === 0) {
                // Check if it's a locked auto-line (no select box, just hidden input)
                const hiddenProd = $(this).find('.product-select');
                if (hiddenProd.is('input')) {
                    prodId = parseInt(hiddenProd.val());
                } else {
                    prodId = parseInt($(this).find('.product-select').val());
                }
            } else {
                accId = parseInt($(this).find('.account-select').val());
            }

            if (prodId || accId) {
                payload.Bill.Items.push({
                    ProductVariantId: prodId || null,
                    ExpenseAccountId: accId || null,
                    Description: $(this).find('.line-desc').val() || 'Auto-pulled',
                    Quantity: parseFloat($(this).find('.line-qty').val()) || 0,
                    UnitPrice: parseFloat($(this).find('.line-price').val()) || 0
                });
            }
        });

        if (payload.Bill.Items.length === 0) {
            toastr.warning("Add at least one valid item or expense.");
            return;
        }

        var $btn = $(event.currentTarget);
        var originalText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Processing...');

        try {
            const res = billId === 0
                ? await api.post('/api/purchasing/supplier-bills', payload)
                : await api.put(`/api/purchasing/supplier-bills/${billId}`, payload); // Support Edit!

            if (res && res.succeeded) {
                toastr.success(res.messages ? res.messages[0] : "Supplier Bill Saved!");
                this._modal.hide();
                this._table.ajax.reload(null, false);
            } else {
                toastr.error(res?.messages?.[0] || "Failed to save Bill.");
            }
        } catch (e) { console.error(e); }
        finally { $btn.prop('disabled', false).html(originalText); }
    },

    viewDocument: async function (id) {
        try {
            this._currentDocId = id;
            const res = await api.get(`/api/purchasing/supplier-bills/${id}`);
            const doc = res.data || res;

            $('#docBillNo').text(doc.billNumber);
            $('#docRefNo').text(doc.supplierInvoiceNo);
            $('#docDate').text(new Date(doc.billDate).toLocaleDateString());
            $('#docDueDate').text(new Date(doc.dueDate).toLocaleDateString());
            $('#docSupplier').text(doc.supplierName);
            $('#docNotes').text(doc.remarks || '-');

            let total = parseFloat(doc.grandTotal || 0);
            let paid = parseFloat(doc.amountPaid || 0);
            let balance = total - paid;

            $('#docSubtotal').text(parseFloat(doc.subTotal || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }));
            $('#docTax').text(parseFloat(doc.taxAmount || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }));
            $('#docTotal').text(total.toLocaleString(undefined, { minimumFractionDigits: 2 }));
            $('#docPaid').text(`-${paid.toLocaleString(undefined, { minimumFractionDigits: 2 })}`);

            const balString = balance.toLocaleString(undefined, { minimumFractionDigits: 2 });
            $('#docBalanceDue').text(balString);
            $('#docBalanceDueLarge').text(`$${balString}`);

            // Payment Logic
            if (doc.isPosted && doc.paymentStatus !== 'Paid') {
                $('#btnModalPay').show();
            } else {
                $('#btnModalPay').hide();
            }

            let tbody = '';
            if (doc.items) {
                doc.items.forEach(i => {
                    let qty = parseFloat(i.quantity || 0);
                    let price = parseFloat(i.unitPrice || 0);

                    tbody += `
                        <tr>
                            <td class="fw-bold">${i.description}</td>
                            <td class="text-center">${qty}</td>
                            <td class="text-end">${price.toFixed(2)}</td>
                            <td class="text-end fw-bold">${(i.lineTotal || 0).toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                        </tr>`;
                });
            }
            $('#docItemsBody').html(tbody);
            this._viewModal.show();

        } catch (e) {
            toastr.error("Failed to load document.");
            console.error(e);
        }
    }
};

$(document).ready(function () { window.billApp.init(); });