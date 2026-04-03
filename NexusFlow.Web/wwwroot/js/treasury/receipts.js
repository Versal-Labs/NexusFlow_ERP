window.receiptApp = {
    _table: null,
    _modal: null,
    _viewModal: null,
    _currentDocId: null,

    init: function () {
        var modalEl = document.getElementById('receiptModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false });

        var viewEl = document.getElementById('viewReceiptModal');
        if (viewEl) this._viewModal = new bootstrap.Modal(viewEl);

        this._initFilters();
        this._initGrid();
        this._loadMasterData();

        const self = this;
        $('#CustomerId').on('change', function () {
            const customerId = $(this).val();
            if (customerId) self._loadUnpaidInvoices(customerId);
            else {
                $('#allocationBody').html('<tr><td colspan="6" class="text-center text-muted fst-italic py-3">Select a customer.</td></tr>');
                self._calculateTotals();
            }
        });

        $('#allocationBody').on('input', '.alloc-input', () => self._calculateTotals());
        $('#ReceiptAmount').on('input', () => self._calculateTotals());

        // Print & Void Bindings
        $('#btnModalVoid').click(() => self.voidReceipt(self._currentDocId));
        $('#btnModalPrint').click(() => window.open(`/api/treasury/receipts/${self._currentDocId}/pdf`, '_blank'));
    },

    // ==========================================
    // ENTERPRISE FILTERS
    // ==========================================
    _initFilters: function () {
        api.get('/api/customer').then(res => {
            const customers = res.data || res || [];
            let $cust = $('#filterCustomer').empty().append('<option value="">All Customers</option>');
            customers.forEach(c => $cust.append($('<option></option>').val(c.id).text(c.name)));
        });

        $('#filterCustomer, #filterMethod, #filterStartDate, #filterEndDate').on('change', () => this.reloadGrid());
    },

    resetFilters: function() {
        $('#filterCustomer').val(''); $('#filterMethod').val('');
        $('#filterStartDate').val(''); $('#filterEndDate').val('');
        this.reloadGrid();
    },

    reloadGrid: function() {
        this._table.ajax.reload();
    },

    // ==========================================
    // GRID INITIALIZATION
    // ==========================================
    _initGrid: function () {
        this._table = $('#receiptsGrid').DataTable({
            ajax: { 
                url: '/api/treasury/receipts', 
                data: function (d) {
                    d.customerId = $('#filterCustomer').val();
                    d.method = $('#filterMethod').val();
                    d.startDate = $('#filterStartDate').val();
                    d.endDate = $('#filterEndDate').val();
                },
                dataSrc: function (json) { return json.data || json || []; } 
            },
            columns: [
                { data: 'referenceNo', className: 'fw-bold text-success font-monospace ps-3' },
                { data: 'date', render: d => new Date(d).toLocaleDateString() },
                { data: 'customerName', className: 'fw-bold text-dark' },
                { 
                    data: 'method', 
                    render: function(d) {
                        if(d === 1) return '<span class="badge bg-secondary"><i class="fa-solid fa-money-bill"></i> Cash</span>';
                        if(d === 2) return '<span class="badge bg-info text-dark"><i class="fa-solid fa-money-bill-transfer"></i> Transfer</span>';
                        if(d === 3) return '<span class="badge bg-warning text-dark"><i class="fa-solid fa-money-check"></i> Cheque</span>';
                        return d;
                    }
                },
                { data: 'amount', className: 'text-end fw-bold', render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                {
                    data: null,
                    className: 'text-end pe-3',
                    orderable: false,
                    render: function (data, type, row) {
                        // Assuming IsVoided might be a field. If voided, we wouldn't show a void button inside, or we disable it.
                        let btns = `<button class="btn btn-sm btn-outline-dark shadow-sm me-1" onclick="receiptApp.viewReceipt(${row.id})" title="View Details"><i class="fa-solid fa-eye"></i></button>`;
                        btns += `<a href="/api/treasury/receipts/${row.id}/pdf" target="_blank" class="btn btn-sm btn-outline-danger shadow-sm" title="Print PDF"><i class="fa-solid fa-file-pdf"></i></a>`;
                        return btns;
                    }
                }
            ],
            order: [[0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    // ==========================================
    // VIEW / AUDIT RECEIPT
    // ==========================================
    viewReceipt: async function(id) {
        try {
            this._currentDocId = id;
            // NOTE: You must build this API endpoint in TreasuryController!
            const doc = await api.get(`/api/treasury/receipts/${id}`); 
            
            $('#viewRefNo').text(doc.referenceNo);
            $('#viewDate').text(new Date(doc.date).toLocaleDateString());
            $('#viewCustomer').text(doc.customerName);
            $('#viewRemarks').text(doc.remarks || '-');
            $('#viewAmount').text(parseFloat(doc.amount).toLocaleString(undefined, { minimumFractionDigits: 2 }));

            if (doc.method === 1) $('#viewMethodBadge').html('<span class="badge bg-secondary fs-6"><i class="fa-solid fa-money-bill"></i> Cash Payment</span>');
            else if (doc.method === 2) $('#viewMethodBadge').html('<span class="badge bg-info text-dark fs-6"><i class="fa-solid fa-money-bill-transfer"></i> Bank Transfer</span>');
            else if (doc.method === 3) $('#viewMethodBadge').html('<span class="badge bg-warning text-dark fs-6"><i class="fa-solid fa-money-check"></i> Cheque</span>');

            // Handle Cheque Box
            if (doc.method === 3 && doc.chequeDetails) {
                $('#viewChequeBox').show();
                $('#viewBank').text(doc.chequeDetails.bankName);
                $('#viewChequeNo').text(doc.chequeDetails.chequeNumber);
                $('#viewChequeDate').text(new Date(doc.chequeDetails.chequeDate).toLocaleDateString());
            } else {
                $('#viewChequeBox').hide();
            }

            let tbody = '';
            if (doc.allocations && doc.allocations.length > 0) {
                doc.allocations.forEach(a => {
                    tbody += `
                        <tr>
                            <td class="fw-bold font-monospace text-primary">${a.invoiceNumber}</td>
                            <td>${new Date(a.invoiceDate).toLocaleDateString()}</td>
                            <td class="text-end">${parseFloat(a.invoiceTotal).toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                            <td class="text-end fw-bold text-success">${parseFloat(a.amountAllocated).toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                        </tr>`;
                });
            } else {
                tbody = '<tr><td colspan="4" class="text-center text-muted fst-italic py-2">This payment was received as an unallocated advance/credit.</td></tr>';
            }
            $('#viewAllocationsBody').html(tbody);

            // Enterprise feature: Hide void if already voided
            if (doc.isVoided) {
                $('#btnModalVoid').hide();
                $('#viewRefNo').append(' <span class="badge bg-danger ms-2">VOIDED</span>');
            } else {
                $('#btnModalVoid').show();
            }

            this._viewModal.show();
        } catch (e) {
            toastr.error("Failed to load receipt details.");
            console.error(e);
        }
    },

    voidReceipt: async function(id) {
        const result = await Swal.fire({
            title: 'Void this Receipt?',
            text: "This will reverse the GL, reinstate the customer's AR balance, and revert the applied invoices back to Unpaid. This action cannot be undone.",
            icon: 'error',
            showCancelButton: true,
            confirmButtonColor: '#dc3545',
            cancelButtonColor: '#6c757d',
            confirmButtonText: 'Yes, Void Receipt',
            reverseButtons: true
        });

        if (!result.isConfirmed) return;

        try {
            // NOTE: You must build this API endpoint and Command!
            const res = await api.post(`/api/treasury/receipts/${id}/void`);
            if (res && res.succeeded) {
                toastr.success("Receipt voided successfully.");
                this._viewModal.hide();
                this.reloadGrid();
            } else if (res && res.messages) {
                toastr.error(res.messages[0]);
            }
        } catch (e) {
            toastr.error(e.responseJSON?.messages?.[0] || "Failed to void receipt.");
        }
    },

    // ==========================================
    // CREATE RECEIPT LOGIC
    // ==========================================
    _loadMasterData: async function() {
        try {
            const [custRes, accRes, bankRes] = await Promise.all([
                api.get('/api/customer'),
                api.get('/api/finance/accounts'),
                api.get('/api/finance/banks') // <--- FIXED ENDPOINT
            ]);

            let custEl = $('#CustomerId').empty().append('<option value="">-- Select Customer --</option>');
            (custRes.data || custRes || []).forEach(c => custEl.append(`<option value="${c.id}">${c.name}</option>`));
            custEl.select2({ dropdownParent: $('#receiptModal') });

            let accEl = $('#AccountId').empty().append('<option value="">-- Select Destination --</option>');
            (accRes.data || accRes || []).filter(a => a.type === 'Asset' || a.type === '1').forEach(a => {
                accEl.append(`<option value="${a.id}">[${a.code}] ${a.name}</option>`);
            });

            // Load Banks
            let bnkEl = $('#BankId').empty().append('<option value="">-- Select Bank --</option>');
            (bankRes.data || bankRes || []).forEach(b => bnkEl.append(`<option value="${b.id}">[${b.bankCode}] ${b.name}</option>`));
            bnkEl.select2({ dropdownParent: $('#receiptModal') });

            // Initialize Branch as empty
            $('#BankBranchId').empty().append('<option value="">-- Select Branch --</option>').select2({ dropdownParent: $('#receiptModal') });

        } catch (e) { console.error("[ReceiptApp] Lookup Error:", e); }
    },

    onBankChange: async function() {
        const bankId = $('#BankId').val();
        let $branch = $('#BankBranchId');
        
        $branch.empty().append('<option value="">-- Select Branch --</option>');
        
        if (!bankId) {
            $branch.prop('disabled', true);
            return;
        }

        try {
            $branch.prop('disabled', true).append('<option>Loading...</option>');
            const res = await api.get(`/api/finance/banks/${bankId}/branches`);
            const branches = res.data || res || [];
            
            $branch.empty().append('<option value="">-- Select Branch --</option>');
            branches.forEach(b => $branch.append(`<option value="${b.id}">[${b.branchCode}] ${b.branchName}</option>`));
            $branch.prop('disabled', false);
        } catch (e) {
            toastr.error("Failed to load branches.");
        }
    },

    onMethodChange: function() {
        let method = $('#Method').val();
        if (method === "3") { // Cheque
            $('#chequeSection').removeClass('d-none');
            $('#divDepositTo').addClass('d-none'); 
            $('#AccountId').removeAttr('required');
            $('#BankId, #BankBranchId, #ChequeNumber, #ChequeDate').attr('required', 'required');
        } else {
            $('#chequeSection').addClass('d-none');
            $('#divDepositTo').removeClass('d-none');
            $('#AccountId').attr('required', 'required');
            $('#BankId, #BankBranchId, #ChequeNumber, #ChequeDate').removeAttr('required').val('');
            $('#BankId').trigger('change');
            $('#BankBranchId').trigger('change');
        }
    },

    _loadUnpaidInvoices: async function (customerId) {
        $('#allocationBody').html('<tr><td colspan="6" class="text-center py-4"><i class="spinner-border spinner-border-sm text-primary me-2"></i> Loading invoices...</td></tr>');
        try {
            const res = await api.get(`/api/sales/customers/${customerId}/unpaid-invoices`);
            const invoices = res.data || res;

            let html = '';
            if (!invoices || invoices.length === 0) {
                html = '<tr><td colspan="6" class="text-center text-success fw-bold py-4"><i class="fa-solid fa-check-circle me-1 fs-5"></i><br>No unpaid invoices for this customer.</td></tr>';
            } else {
                invoices.forEach(inv => {
                    const isOverdue = new Date(inv.dueDate) < new Date() ? 'text-danger fw-bold' : '';
                    html += `
                        <tr data-id="${inv.id}">
                            <td class="font-monospace fw-bold text-primary">${inv.invoiceNumber}</td>
                            <td>${new Date(inv.invoiceDate).toLocaleDateString()}</td>
                            <td class="${isOverdue}">${new Date(inv.dueDate).toLocaleDateString()}</td>
                            <td class="text-end">${inv.grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                            <td class="text-end fw-bold">${inv.balance.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                            <td>
                                <input type="number" class="form-control form-control-sm text-end fw-bold text-success alloc-input" 
                                       data-balance="${inv.balance}" value="0.00" min="0" max="${inv.balance}" step="0.01" onclick="this.select()">
                            </td>
                        </tr>`;
                });
            }
            $('#allocationBody').html(html);
            this._calculateTotals();
        } catch (e) {
            $('#allocationBody').html('<tr><td colspan="6" class="text-center text-danger py-3">Error loading invoices.</td></tr>');
        }
    },

    autoAllocate: function () {
        let receiptAmount = parseFloat($('#ReceiptAmount').val()) || 0;
        if (receiptAmount <= 0) {
            toastr.warning("Please enter the Actual Amount Received first.");
            $('#ReceiptAmount').focus(); return;
        }

        let remainingMoney = receiptAmount;
        $('.alloc-input').each(function () {
            let maxBalance = parseFloat($(this).data('balance')) || 0;
            if (remainingMoney >= maxBalance) {
                $(this).val(maxBalance.toFixed(2));
                remainingMoney -= maxBalance;
            } else if (remainingMoney > 0) {
                $(this).val(remainingMoney.toFixed(2));
                remainingMoney = 0;
            } else {
                $(this).val('0.00');
            }
        });

        this._calculateTotals();
        toastr.success("Amount automatically distributed to oldest invoices.");
    },

    _calculateTotals: function () {
        let receiptAmount = parseFloat($('#ReceiptAmount').val()) || 0;
        let totalAllocated = 0;

        $('.alloc-input').each(function () {
            let val = parseFloat($(this).val()) || 0;
            let max = parseFloat($(this).data('balance')) || 0;
            if (val > max) { val = max; $(this).val(max.toFixed(2)); }
            totalAllocated += val;
        });

        let unallocated = receiptAmount - totalAllocated;
        $('#lblAllocated').text(totalAllocated.toLocaleString(undefined, { minimumFractionDigits: 2 }));
        
        let $unallocLbl = $('#lblUnallocated');
        if (unallocated < 0) {
            $unallocLbl.text("OVER-ALLOCATED!").removeClass('text-success').addClass('text-danger');
        } else {
            $unallocLbl.text(unallocated.toLocaleString(undefined, { minimumFractionDigits: 2 })).removeClass('text-danger').addClass('text-success');
        }
    },

    openModal: function () {
        $('#receiptForm')[0].reset();
        $('#receiptForm').removeClass('was-validated');
        $('#Date').val(new Date().toISOString().split('T')[0]);
        $('#CustomerId').val('').trigger('change');
        this.onMethodChange(); 
        if (this._modal) this._modal.show();
    },

    save: async function() {
        var form = $('#receiptForm')[0];
        if (!form.checkValidity()) { $(form).addClass('was-validated'); return; }

        const receiptAmount = parseFloat($('#ReceiptAmount').val()) || 0;
        let totalAllocated = 0;
        let allocations = [];

        $('.alloc-input').each(function() {
            let val = parseFloat($(this).val()) || 0;
            if (val > 0) {
                totalAllocated += val;
                allocations.push({ InvoiceId: parseInt($(this).closest('tr').data('id')), Amount: val });
            }
        });

        if (totalAllocated > receiptAmount) {
            toastr.error("You cannot allocate more money than you actually received!");
            return;
        }

        const method = parseInt($('#Method').val());

        const payload = {
            Date: $('#Date').val(),
            Type: 1, // CustomerReceipt
            Method: method,
            ReceiptAmount: receiptAmount,
            AccountId: method === 3 ? 0 : parseInt($('#AccountId').val()), 
            CustomerId: parseInt($('#CustomerId').val()),
            Remarks: $('#Remarks').val() || "Customer Payment",
            
            // EXACT ID PASSED TO BACKEND
            BankBranchId: method === 3 ? parseInt($('#BankBranchId').val()) : null,
            ChequeNumber: method === 3 ? $('#ChequeNumber').val() : null,
            ChequeDate: method === 3 ? $('#ChequeDate').val() : null,
            
            Allocations: allocations
        };

        var $btn = $(event.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm"></i> Saving...');

        try {
            const res = await api.post('/api/treasury/receipts', payload);
            if (res && res.succeeded) {
                toastr.success(res.message || "Payment Recorded Successfully!");
                this._modal.hide();
                this._table.ajax.reload(null, false);
            } else if (res && res.messages) { toastr.error(res.messages[0]); }
        } catch (e) {
            toastr.error(e.responseJSON?.messages?.[0] || "Failed to process receipt.");
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    }
};

$(document).ready(() => receiptApp.init());