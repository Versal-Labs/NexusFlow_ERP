window.receiptApp = {
    _table: null,
    _modal: null,

    init: function () {
        console.log("[ReceiptApp] Initialization started...");
        try {
            // 1. Safely bind the modal
            var modalEl = document.getElementById('receiptModal');
            if (modalEl) {
                this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false });
            }

            this._initGrid();
            this._loadMasterData();

            // 2. Event Listeners for the Allocation Engine
            const self = this;

            // When customer changes, load their specific unpaid invoices
            $('#CustomerId').on('change', function () {
                const customerId = $(this).val();
                if (customerId) {
                    self._loadUnpaidInvoices(customerId);
                } else {
                    $('#allocationBody').html('<tr><td colspan="6" class="text-center text-muted fst-italic py-3">Select a customer to view unpaid invoices.</td></tr>');
                    self._calculateTotal();
                }
            });

            // Auto-calculate the total received amount as the user types in the grid
            $('#allocationBody').on('input', '.alloc-input', function () {
                self._calculateTotal();
            });

        } catch (e) {
            console.error("[ReceiptApp] Init Error:", e);
        }
    },

    _initGrid: function () {
        this._table = $('#receiptsGrid').DataTable({
            ajax: {
                url: '/api/treasury/receipts',
                dataSrc: function (json) { return json.data || json || []; },
                headers: { "Authorization": "Bearer " + localStorage.getItem("jwtToken") }
            },
            columns: [
                { data: 'referenceNo', className: 'fw-bold text-success font-monospace' },
                { data: 'date', render: d => new Date(d).toLocaleDateString() },
                { data: 'customerName', className: 'fw-bold text-dark' },
                { data: 'method' },
                { data: 'relatedDocumentNo', className: 'font-monospace text-muted' },
                {
                    data: 'amount',
                    className: 'text-end fw-bold text-dark',
                    render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 })
                }
            ],
            order: [[1, 'desc'], [0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 20
        });
    },

    _loadMasterData: async function () {
        try {
            const [custRes, accRes] = await Promise.all([
                api.get('/api/customer'),
                api.get('/api/finance/accounts')
            ]);

            const customers = custRes.data || custRes;
            const accounts = accRes.data || accRes;

            // Populate Customers securely
            let custEl = $('#CustomerId');
            custEl.empty().append('<option value="">-- Select Customer --</option>');
            if (customers.length > 0) {
                customers.forEach(c => custEl.append(`<option value="${c.id}">${c.name}</option>`));
            }

            // Destroy existing select2 if present, then rebind to modal
            if (custEl.hasClass("select2-hidden-accessible")) custEl.select2('destroy');
            custEl.select2({ dropdownParent: $('#receiptModal') });

            // Populate Banks/Cash Accounts
            let accEl = $('#AccountId');
            accEl.empty().append('<option value="">-- Select Destination --</option>');
            if (accounts.length > 0) {
                accounts.filter(a => a.type === 'Asset' || a.type === '1').forEach(a => {
                    accEl.append(`<option value="${a.id}">[${a.code}] ${a.name}</option>`);
                });
            }
        } catch (e) {
            console.error("[ReceiptApp] Lookup Error:", e);
        }
    },

    _loadUnpaidInvoices: async function (customerId) {
        $('#allocationBody').html('<tr><td colspan="6" class="text-center py-3"><div class="spinner-border spinner-border-sm text-primary"></div> Loading...</td></tr>');

        try {
            const res = await api.get(`/api/sales/customers/${customerId}/unpaid-invoices`);
            const invoices = res.data || res;

            let html = '';
            if (!invoices || invoices.length === 0) {
                html = '<tr><td colspan="6" class="text-center text-success fw-bold py-4"><i class="fa-solid fa-check-circle me-1 fs-5"></i><br>No unpaid invoices for this customer.</td></tr>';
            } else {
                invoices.forEach(inv => {
                    // Check if overdue for UI highlighting
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
                                       data-balance="${inv.balance}" value="0.00" min="0" max="${inv.balance}" step="0.01">
                            </td>
                        </tr>
                    `;
                });
            }
            $('#allocationBody').html(html);
            this._calculateTotal();
        } catch (e) {
            $('#allocationBody').html('<tr><td colspan="6" class="text-center text-danger py-3">Error loading invoices.</td></tr>');
        }
    },

    _calculateTotal: function () {
        let total = 0;
        $('.alloc-input').each(function () {
            let val = parseFloat($(this).val()) || 0;
            let max = parseFloat($(this).data('balance')) || 0;

            // Prevent user from overpaying a specific invoice
            if (val > max) {
                val = max;
                $(this).val(max.toFixed(2));
            }
            total += val;
        });

        // Update hidden field for saving, and UI label for the user
        $('#TotalAmount').val(total);
        $('#lblTotalReceived').text('Rs. ' + total.toLocaleString(undefined, { minimumFractionDigits: 2 }));
    },

    openModal: function () {
        console.log("[ReceiptApp] Opening Modal...");

        // 1. Safely reset form
        var form = document.getElementById('receiptForm');
        if (form) form.reset();

        // 2. Set default date
        var dateEl = document.getElementById('Date');
        if (dateEl) dateEl.valueAsDate = new Date();

        // 3. Clear Customer (This will trigger the change event and clear the grid)
        $('#CustomerId').val('').trigger('change');

        // 4. Bulletproof Modal Show
        if (this._modal) {
            this._modal.show();
        } else {
            var modalEl = document.getElementById('receiptModal');
            if (modalEl) {
                this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false });
                this._modal.show();
            } else {
                console.error("Fatal: Modal DOM element missing.");
            }
        }
    },

    save: async function () {
        var form = document.getElementById('receiptForm');
        if (!form || !form.checkValidity()) {
            if (form) form.reportValidity();
            return;
        }

        const totalAmount = parseFloat($('#TotalAmount').val()) || 0;

        // Validation: Must allocate money
        if (totalAmount <= 0) {
            toastr.warning("You must allocate an amount greater than zero to an invoice.");
            return;
        }

        const payload = {
            Date: $('#Date').val(),
            Type: 1, // 1 = CustomerReceipt
            Method: parseInt($('#Method').val()),
            AccountId: parseInt($('#AccountId').val()), // The specific Bank/Cash Account
            Amount: totalAmount,
            CustomerId: parseInt($('#CustomerId').val()),
            Remarks: $('#Remarks').val() || "Payment Allocated",
            Allocations: []
        };

        // Harvest allocations from the grid
        $('.alloc-input').each(function () {
            let val = parseFloat($(this).val()) || 0;
            if (val > 0) {
                payload.Allocations.push({
                    InvoiceId: parseInt($(this).closest('tr').data('id')),
                    Amount: val
                });
            }
        });

        const res = await api.post('/api/treasury/payments', payload);

        if (res && res.succeeded) {
            toastr.success(res.messages ? res.messages[0] : "Payment Recorded Successfully!");
            if (this._modal) this._modal.hide();
            if (this._table) this._table.ajax.reload(null, false);
        }
    }
};

// Bind to document ready securely
$(document).ready(function () {
    window.receiptApp.init();
});