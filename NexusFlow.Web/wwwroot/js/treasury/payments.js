window.paymentApp = {
    _modal: null,
    _table: null,

    init: function () {
        var modalEl = document.getElementById('paymentModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl);
        document.getElementById('paymentDate').valueAsDate = new Date();
        
        this._initGrid();
        this._loadLookups();
        $('#btnSavePayment').on('click', (e) => this.save(e));
    },

    _initGrid: function () {
        this._table = $('#paymentsGrid').DataTable({
            ajax: {
                // Ensure your Api/TreasuryController has this GET endpoint returning Payment transactions
                url: '/api/treasury/payments', 
                dataSrc: function (json) { return json.data || json || []; }
            },
            columns: [
                { data: 'referenceNo', className: 'fw-bold font-monospace text-primary ps-4', defaultContent: '-' },
                { data: 'date', render: d => d ? new Date(d).toLocaleDateString() : '-' },
                { data: 'partyName', className: 'fw-bold text-dark' },
                { data: 'accountName' },
                { data: 'amount', className: 'text-end pe-4 fw-bold text-danger', render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) }
            ],
            order: [[1, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    _loadLookups: async function () {
        try {
            // Get Bank/Cash Accounts
            const accRes = await api.get('/api/finance/accounts/banks'); // Adjust to your actual accounts endpoint
            const accounts = accRes.data || accRes || [];
            let $acc = $('#accountId').empty().append('<option value="">-- Select Bank/Cash --</option>');
            accounts.forEach(a => $acc.append($('<option></option>').val(a.id).text(`[${a.code}] ${a.name}`)));

            // Supplier Select2
            $('#supplierId').select2({
                theme: 'bootstrap-5',
                dropdownParent: $('#paymentModal'),
                placeholder: 'Search Supplier...',
                ajax: {
                    url: '/api/purchasing/suppliers/search',
                    headers: { 'Authorization': `Bearer ${localStorage.getItem('nexus_token')}` },
                    dataType: 'json',
                    delay: 250,
                    data: params => ({ query: params.term }),
                    processResults: data => ({ results: $.map(data, item => ({ id: item.id, text: item.name })) })
                }
            });
        } catch (e) {
            console.error("Lookup Error:", e);
        }
    },

    openCreateModal: function () {
        $('#paymentForm')[0].reset();
        document.getElementById('paymentDate').valueAsDate = new Date();
        $('#supplierId').val(null).trigger('change');
        this._modal.show();
    },

    save: async function (e) {
        if (!$('#paymentForm')[0].checkValidity()) {
            $('#paymentForm')[0].reportValidity();
            return;
        }

        const payload = {
            type: 2, // PaymentType.Payment
            date: $('#paymentDate').val(),
            partyId: parseInt($('#supplierId').val()),
            accountId: parseInt($('#accountId').val()),
            amount: parseFloat($('#amount').val()),
            referenceNo: $('#referenceNo').val().trim(),
            notes: $('#notes').val().trim()
        };

        var $btn = $(e.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Posting...');

        try {
            const res = await api.post('/api/treasury/record-payment', payload);
            if (res && res.succeeded) {
                toastr.success(res.message || "Payment posted successfully");
                this._modal.hide();
                this._table.ajax.reload(null, false);
            } else if (res && res.messages) { toastr.error(res.messages[0]); }
        } catch (err) {
            toastr.error(err.responseJSON?.messages?.[0] || "Failed to execute payment.");
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    }
};
$(document).ready(() => window.paymentApp.init());