window.reconApp = {
    _rawBeginBalance: 0,
    _rawEndBalance: 0,
    _adjModal: null,

    init: function () {
        var adjEl = document.getElementById('adjModal');
        if (adjEl) this._adjModal = new bootstrap.Modal(adjEl);

        document.getElementById('StatementDate').valueAsDate = new Date();
        this._loadBankAccounts();
    },

    _loadBankAccounts: async function() {
        try {
            const res = await api.get('/api/finance/accounts?type=Bank'); 
            const banks = res.data || res || [];
            let $ddl = $('#BankAccountId').empty().append('<option value="">-- Select Bank Account --</option>');
            banks.forEach(b => $ddl.append($('<option></option>').val(b.id).text(`[${b.accountCode}] ${b.name}`)));
            $ddl.select2();
        } catch (e) { console.error("Failed to load bank accounts."); }
    },

    onAccountSelect: async function() {
        const bankId = $('#BankAccountId').val();
        if (!bankId) {
            $('#BeginningBalance').val('0.00');
            this._rawBeginBalance = 0;
            return;
        }
        try {
            const res = await api.get(`/api/finance/banking/beginning-balance?bankAccountId=${bankId}`);
            this._rawBeginBalance = parseFloat(res.data || res || 0);
            $('#BeginningBalance').val(this._rawBeginBalance.toLocaleString(undefined, { minimumFractionDigits: 2 }));
        } catch(e) { toastr.error("Failed to fetch beginning balance."); }
    },

    startReconciliation: async function() {
        const bankId = $('#BankAccountId').val();
        const date = $('#StatementDate').val();
        this._rawEndBalance = parseFloat($('#EndingBalance').val()) || 0;

        if (!bankId || !date || this._rawEndBalance === 0) {
            toastr.warning("Please select a bank, date, and enter the Statement Ending Balance.");
            return;
        }

        $('#setupCard input, #setupCard select, #btnStart').prop('disabled', true);
        $('#splitBoardCard, #reconFooter').removeClass('d-none');
        $('#calcBegin').text(this._rawBeginBalance.toLocaleString(undefined, { minimumFractionDigits: 2 }));

        this.loadTransactions(bankId, date);
    },

    loadTransactions: async function(bankId, date) {
        try {
            const res = await api.get(`/api/finance/banking/uncleared?bankAccountId=${bankId}&statementDate=${date}`);
            const lines = res.data || res || [];

            let inHtml = ''; let outHtml = '';

            lines.forEach(l => {
                const tr = `
                    <tr class="recon-row" data-id="${l.journalLineId}" data-amount="${l.amount}">
                        <td class="text-center"><input type="checkbox" class="form-check-input recon-cb" onchange="reconApp.calculateMath()"></td>
                        <td>${new Date(l.date).toLocaleDateString()}</td>
                        <td class="font-monospace fw-bold text-primary" title="${l.description}">${l.referenceNo}</td>
                        <td class="text-end fw-bold">${l.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                    </tr>`;

                if (l.type === 'IN') inHtml += tr;
                else outHtml += tr;
            });

            $('#bodyMoneyIn').html(inHtml || '<tr><td colspan="4" class="text-center text-muted">No uncleared deposits found.</td></tr>');
            $('#bodyMoneyOut').html(outHtml || '<tr><td colspan="4" class="text-center text-muted">No uncleared payments found.</td></tr>');
            
            this.calculateMath();
        } catch (e) { toastr.error("Failed to load transactions."); }
    },

    toggleAll: function(type, sourceCb) {
        const isChecked = $(sourceCb).is(':checked');
        const targetTbody = type === 'IN' ? '#bodyMoneyIn' : '#bodyMoneyOut';
        
        $(`${targetTbody} .recon-cb`).prop('checked', isChecked);
        this.calculateMath();
    },

    calculateMath: function() {
        let selectedIn = 0;
        let selectedOut = 0;

        $('#bodyMoneyIn .recon-cb:checked').each(function() {
            selectedIn += parseFloat($(this).closest('tr').data('amount'));
            $(this).closest('tr').addClass('selected');
        });
        $('#bodyMoneyIn .recon-cb:not(:checked)').each(function() { $(this).closest('tr').removeClass('selected'); });

        $('#bodyMoneyOut .recon-cb:checked').each(function() {
            selectedOut += parseFloat($(this).closest('tr').data('amount'));
            $(this).closest('tr').addClass('selected');
        });
        $('#bodyMoneyOut .recon-cb:not(:checked)').each(function() { $(this).closest('tr').removeClass('selected'); });

        $('#lblTotalMoneyIn, #calcIn').text(selectedIn.toLocaleString(undefined, { minimumFractionDigits: 2 }));
        $('#lblTotalMoneyOut, #calcOut').text(selectedOut.toLocaleString(undefined, { minimumFractionDigits: 2 }));

        // THE GOLDEN MATH FORMULA
        let clearedBalance = this._rawBeginBalance + selectedIn - selectedOut;
        let difference = this._rawEndBalance - clearedBalance;

        $('#calcCleared').text(clearedBalance.toLocaleString(undefined, { minimumFractionDigits: 2 }));

        let $diffLbl = $('#calcDiff');
        $diffLbl.text(difference.toLocaleString(undefined, { minimumFractionDigits: 2 }));

        // ENTERPRISE LOCK
        if (Math.abs(difference) < 0.01) { // Floating point safety
            $diffLbl.removeClass('text-danger').addClass('text-success');
            $('#btnFinalize').prop('disabled', false);
        } else {
            $diffLbl.removeClass('text-success').addClass('text-danger');
            $('#btnFinalize').prop('disabled', true);
        }
    },

    openAdjustment: function(type) {
        $('#adjForm')[0].reset();
        $('#AdjType').val(type);
        document.getElementById('AdjDate').valueAsDate = new Date($('#StatementDate').val());
        
        if(type === 'FEE') {
            $('#adjTitle').html('<i class="fa-solid fa-minus-circle text-danger me-2"></i>Add Bank Fee');
            $('#AdjNote').val('Bank Service Charge');
        } else {
            $('#adjTitle').html('<i class="fa-solid fa-plus-circle text-success me-2"></i>Add Earned Interest');
            $('#AdjNote').val('Interest Earned');
        }
        this._adjModal.show();
    },

    postAdjustment: async function() {
        var form = $('#adjForm')[0];
        if (!form.checkValidity()) { form.reportValidity(); return; }

        const payload = {
            BankAccountId: parseInt($('#BankAccountId').val()),
            Date: $('#AdjDate').val(),
            Amount: parseFloat($('#AdjAmount').val()),
            Type: $('#AdjType').val(),
            Reference: `ADJ-${Date.now().toString().slice(-6)}`,
            Note: $('#AdjNote').val()
        };

        try {
            const res = await api.post('/api/finance/banking/adjustment', payload);
            if (res && res.succeeded) {
                toastr.success("Adjustment posted successfully.");
                this._adjModal.hide();
                // Reload transactions to pull the new adjustment in so they can clear it
                this.loadTransactions($('#BankAccountId').val(), $('#StatementDate').val());
            }
        } catch (e) { toastr.error("Failed to post adjustment."); }
    },

    finalizeReconciliation: async function() {
        let clearedIds = [];
        $('.recon-cb:checked').each(function() {
            clearedIds.push(parseInt($(this).closest('tr').data('id')));
        });

        if(clearedIds.length === 0) {
            toastr.warning("You must select at least one transaction to clear."); return;
        }

        const payload = {
            BankAccountId: parseInt($('#BankAccountId').val()),
            StatementDate: $('#StatementDate').val(),
            StatementEndingBalance: this._rawEndBalance,
            ClearedJournalLineIds: clearedIds
        };

        $('#btnFinalize').prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i> Locking...');

        try {
            const res = await api.post('/api/finance/banking/finalize', payload);
            if (res && res.succeeded) {
                await Swal.fire('Reconciliation Complete!', res.message, 'success');
                window.location.reload(); // Refresh entirely to start fresh
            } else if (res && res.messages) {
                toastr.error(res.messages[0]);
                $('#btnFinalize').prop('disabled', false).html('<i class="fa-solid fa-lock me-2"></i> Finalize');
            }
        } catch (e) {
            toastr.error("Reconciliation failed.");
            $('#btnFinalize').prop('disabled', false).html('<i class="fa-solid fa-lock me-2"></i> Finalize');
        }
    }
};

$(document).ready(() => reconApp.init());