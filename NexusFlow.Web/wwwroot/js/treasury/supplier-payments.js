window.paymentApp = {
    _modal: null,
    _table: null,

    init: function () {
        var modalEl = document.getElementById('paymentModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });
        document.getElementById('paymentDate').valueAsDate = new Date();
        
        this._initGrid();
        this._loadLookups();

        $('#btnSavePayment').on('click', (e) => this.save(e));

        const self = this;
        $('#supplierId').on('change', function () {
            const suppId = $(this).val();
            if (suppId) self._loadUnpaidBills(suppId);
            else {
                $('#allocationBody').html('<tr><td colspan="6" class="text-center text-muted fst-italic py-3">Select a supplier.</td></tr>');
                self._calculateTotals();
            }
        });

        $('#allocationBody').on('input', '.alloc-input', () => self._calculateTotals());
        $('#amount').on('input', () => self._calculateTotals());
        
        $('#filterPaySupplierId').on('change', () => this.reloadGrid());
    },

    _initGrid: function () {
        this._table = $('#paymentsGrid').DataTable({
            // USING YOUR GLOBAL API SETUP FOR DATATABLES
            ajax: async function (data, callback, settings) {
                try {
                    let supplierId = $('#filterPaySupplierId').val() || '';
                    let method = $('#filterMethod').val() || '';
                    let url = `/api/treasury/payments?type=2&supplierId=${supplierId}&method=${method}`;
                    
                    const res = await api.get(url);
                    callback({ data: res.data || res || [] });
                } catch (e) {
                    callback({ data: [] });
                }
            },
            columns: [
                { data: 'referenceNo', className: 'fw-bold font-monospace text-danger ps-3' },
                { data: 'date', render: d => new Date(d).toLocaleDateString() },
                { data: 'partyName', className: 'fw-bold text-dark' },
                { 
                    data: 'method', 
                    render: function(d) {
                        if(d === 1) return '<span class="badge bg-secondary">Cash</span>';
                        if(d === 2) return '<span class="badge bg-info text-dark">Transfer</span>';
                        if(d === 4) return '<span class="badge bg-dark">Own Cheque</span>';
                        if(d === 5) return '<span class="badge bg-primary">Endorsed Cheque</span>';
                        return d;
                    }
                },
                { data: 'amount', className: 'text-end fw-bold text-danger', render: d => 'LKR ' + parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                {
                    data: null, className: 'text-end pe-3', orderable: false,
                    render: function (data, type, row) {
                        let btns = `<button class="btn btn-sm btn-outline-dark shadow-sm me-1" onclick="paymentApp.viewPayment(${row.id})" title="View Details"><i class="fa-solid fa-eye"></i></button>`;
                        btns += `<button class="btn btn-sm btn-outline-secondary shadow-sm" title="Print Remittance" onclick="window.open('/api/treasury/payments/${row.id}/remittance', '_blank')"><i class="fa-solid fa-print"></i></button>`;
                        return btns;
                    }
                }
            ],
            order: [[0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    reloadGrid: function() {
        this._table.ajax.reload();
    },

    resetFilters: function() {
        $('#filterPaySupplierId').val('').trigger('change');
        $('#filterMethod').val('');
        this.reloadGrid();
    },

    _loadLookups: async function () {
        try {
            const suppRes = await api.get('/api/supplier');
            let $supp = $('#supplierId').empty().append('<option value="">-- Select Supplier --</option>');
            let $filterSupp = $('#filterPaySupplierId').empty().append('<option value="">All Suppliers</option>');
            
            (suppRes.data || suppRes || []).forEach(s => {
                $supp.append(`<option value="${s.id}">${s.name}</option>`);
                $filterSupp.append(`<option value="${s.id}">${s.name}</option>`);
            });
            $supp.select2({ dropdownParent: $('#paymentModal') });
            $filterSupp.select2();

            const accRes = await api.get('/api/finance/accounts?type=Bank');
            let $acc = $('#accountId').empty().append('<option value="">-- Select Bank/Cash --</option>');
            (accRes.data || accRes || []).forEach(a => $acc.append(`<option value="${a.id}">[${a.code}] ${a.name}</option>`));

            // Load Unendorsed Cheques sitting in the safe
            const chqRes = await api.get('/api/treasury/cheques?status=0');
            let $chq = $('#endorseChequeId').empty().append('<option value="">-- Select Cheque from Vault --</option>');
            (chqRes.data || chqRes || []).forEach(c => {
                $chq.append($('<option></option>').val(c.id).data('amount', c.amount).text(`CHQ: ${c.chequeNumber} | ${c.customerName} | $${c.amount}`));
            });
            $chq.select2({ dropdownParent: $('#paymentModal') });

        } catch (e) { console.error("Lookup Error:", e); }
    },

    onMethodChange: function() {
        let method = $('#method').val();
        
        if (method === "5") { // Endorsed Cheque
            $('#divPayFrom').addClass('d-none');
            $('#accountId').removeAttr('required');
            
            $('#divEndorseCheque').removeClass('d-none');
            $('#endorseChequeId').attr('required', 'required');
            
            $('#amount').prop('readonly', true).addClass('bg-light');
            $('#amountHelpText').text("Amount is locked to the selected Cheque's value.");
        } else {
            $('#divEndorseCheque').addClass('d-none');
            $('#endorseChequeId').removeAttr('required').val('').trigger('change');
            
            $('#divPayFrom').removeClass('d-none');
            $('#accountId').attr('required', 'required');
            
            $('#amount').prop('readonly', false).removeClass('bg-light');
            $('#amountHelpText').text("Enter the amount you are paying the supplier.");
        }
    },

    onEndorsedChequeSelect: function() {
        const selectedOption = $('#endorseChequeId').find(':selected');
        const amount = selectedOption.data('amount') || 0;
        $('#amount').val(parseFloat(amount).toFixed(2));
        this._calculateTotals();
    },

    _loadUnpaidBills: async function(supplierId) {
        $('#allocationBody').html('<tr><td colspan="6" class="text-center py-4"><i class="spinner-border spinner-border-sm text-danger me-2"></i> Loading bills...</td></tr>');
        try {
            const res = await api.get(`/api/purchasing/suppliers/${supplierId}/unpaid-bills`);
            const bills = res.data || res || [];
            let html = '';
            if (bills.length === 0) {
                html = '<tr><td colspan="6" class="text-center text-success fw-bold py-4">No unpaid bills for this supplier.</td></tr>';
            } else {
                bills.forEach(b => {
                    html += `
                        <tr data-id="${b.id}">
                            <td class="font-monospace fw-bold text-danger">${b.billNumber}</td>
                            <td>${new Date(b.date).toLocaleDateString()}</td>
                            <td>${new Date(b.date).toLocaleDateString()}</td>
                            <td class="text-end">${b.total.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                            <td class="text-end fw-bold">${b.balance.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                            <td>
                                <input type="number" class="form-control form-control-sm text-end fw-bold text-danger alloc-input" 
                                       data-balance="${b.balance}" value="0.00" min="0" max="${b.balance}" step="0.01" onclick="this.select()">
                            </td>
                        </tr>`;
                });
            }
            $('#allocationBody').html(html);
            this._calculateTotals();
        } catch (e) { $('#allocationBody').html('<tr><td colspan="6" class="text-center text-danger py-3">Error loading bills.</td></tr>'); }
    },

    autoAllocate: function () {
        let paymentAmount = parseFloat($('#amount').val()) || 0;
        if (paymentAmount <= 0) {
            toastr.warning("Amount must be greater than zero."); return;
        }

        let remainingMoney = paymentAmount;
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
        toastr.success("Amount applied to oldest bills.");
    },

    _calculateTotals: function () {
        let paymentAmount = parseFloat($('#amount').val()) || 0;
        let totalAllocated = 0;

        $('.alloc-input').each(function () {
            let val = parseFloat($(this).val()) || 0;
            let max = parseFloat($(this).data('balance')) || 0;
            if (val > max) { val = max; $(this).val(max.toFixed(2)); }
            totalAllocated += val;
        });

        let unallocated = paymentAmount - totalAllocated;
        $('#lblAllocated').text(totalAllocated.toLocaleString(undefined, { minimumFractionDigits: 2 }));
        
        let $unallocLbl = $('#lblUnallocated');
        if (unallocated < 0) {
            $unallocLbl.text("OVER-ALLOCATED!").removeClass('text-success').addClass('text-danger');
        } else {
            $unallocLbl.text(unallocated.toLocaleString(undefined, { minimumFractionDigits: 2 })).removeClass('text-danger').addClass('text-success');
        }
    },

    viewPayment: async function(id) {
        try {
            // Fetch the specific payment details from the backend
            const res = await api.get('/api/treasury/payments/' + id);
            const data = res.data || res;

            if (!data) {
                toastr.error("Payment not found.");
                return;
            }

            // Populate Master Data
            $('#viewRefNo').text(data.referenceNo);
            $('#viewAmount').text('$' + data.amount.toLocaleString(undefined, { minimumFractionDigits: 2 }));
            $('#viewDate').text(new Date(data.date).toLocaleDateString());
            $('#viewSupplier').text(data.supplierName);
            $('#viewRemarks').text(data.remarks || 'N/A');

            // Format Method
            let methodStr = "Unknown";
            if (data.method === 1) methodStr = "Cash";
            if (data.method === 2) methodStr = "Bank Transfer";
            if (data.method === 4) methodStr = "Own Cheque";
            if (data.method === 5) methodStr = "Endorsed Customer Cheque";
            $('#viewMethod').text(methodStr);

            // Format Source
            $('#viewSource').text(data.sourceName || 'Vault (Swapped)');

            // Populate Allocations Grid
            let html = '';
            if (!data.allocations || data.allocations.length === 0) {
                html = '<tr><td colspan="3" class="text-center text-muted fst-italic py-3">This payment was stored entirely as an Advance (Unallocated).</td></tr>';
            } else {
                data.allocations.forEach(a => {
                    html += `
                    <tr>
                        <td class="font-monospace fw-bold">${a.billNumber}</td>
                        <td>${new Date(a.billDate).toLocaleDateString()}</td>
                        <td class="text-end fw-bold text-success">${a.amountApplied.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                    </tr>`;
                });
            }
            $('#viewAllocationsBody').html(html);

            // Show the Modal
            new bootstrap.Modal(document.getElementById('viewPaymentModal')).show();

        } catch (e) {
            toastr.error("Failed to load payment details.");
        }
    },

    openCreateModal: function () {
        $('#paymentForm')[0].reset();
        document.getElementById('paymentDate').valueAsDate = new Date();
        $('#supplierId').val('').trigger('change');
        this.onMethodChange();
        this._modal.show();
    },

    save: async function (e) {
        var form = $('#paymentForm')[0];
        if (!form.checkValidity()) { form.reportValidity(); return; }

        const paymentAmount = parseFloat($('#amount').val()) || 0;
        let totalAllocated = 0;
        let allocations = [];

        $('.alloc-input').each(function () {
            let val = parseFloat($(this).val()) || 0;
            if (val > 0) {
                totalAllocated += val;
                allocations.push({ InvoiceId: parseInt($(this).closest('tr').data('id')), Amount: val });
            }
        });

        if (totalAllocated > paymentAmount) {
            toastr.error("You cannot allocate more money than the payment total!"); return;
        }

        const method = parseInt($('#method').val());

        const payload = {
            Date: $('#paymentDate').val(),
            Method: method,
            PaymentAmount: paymentAmount, 
            SupplierId: parseInt($('#supplierId').val()),
            Remarks: $('#referenceNo').val(),
            Allocations: allocations
        };

        if (method === 5) {
            payload.EndorsedChequeId = parseInt($('#endorseChequeId').val());
        } else {
            payload.AccountId = parseInt($('#accountId').val());
        }

        var $btn = $(e.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Posting...');

        try {
            const res = await api.post('/api/treasury/payments', payload); 
            if (res && res.succeeded) {
                toastr.success(res.message || "Payment posted successfully");
                this._modal.hide();
                this._table.ajax.reload(null, false);
            } else if (res && res.messages) { toastr.error(res.messages[0]); }
        } catch (err) { toastr.error("Failed to execute payment."); } 
        finally { $btn.prop('disabled', false).html(ogText); }
    }
};

$(document).ready(() => window.paymentApp.init());