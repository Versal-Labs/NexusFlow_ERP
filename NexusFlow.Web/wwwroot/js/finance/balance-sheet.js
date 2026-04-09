window.bsApp = {
    _currentData: null,

    init: function () {
        $('#DatePreset').on('change', (e) => this.applyDatePreset($(e.target).val()));
        $('#AsOfDate').on('change', () => $('#DatePreset').val('Custom'));

        this.applyDatePreset('Today');
        this.generateReport();
    },

    applyDatePreset: function(preset) {
        if (preset === 'Custom') return;

        const today = new Date();
        let targetDate;

        switch (preset) {
            case 'Today':
                targetDate = today; break;
            case 'EndThisMonth':
                targetDate = new Date(today.getFullYear(), today.getMonth() + 1, 0); break;
            case 'EndLastMonth':
                targetDate = new Date(today.getFullYear(), today.getMonth(), 0); break;
            case 'EndThisYear':
                targetDate = new Date(today.getFullYear(), 11, 31); break;
            case 'EndLastYear':
                targetDate = new Date(today.getFullYear() - 1, 11, 31); break;
        }

        const formatDate = (dateObj) => {
            const year = dateObj.getFullYear();
            const month = String(dateObj.getMonth() + 1).padStart(2, '0');
            const d = String(dateObj.getDate()).padStart(2, '0');
            return `${year}-${month}-${d}`;
        };

        $('#AsOfDate').val(formatDate(targetDate));
    },

    generateReport: async function () {
        const asOf = $('#AsOfDate').val();

        if (!asOf) {
            toastr.error("Please select a valid 'As Of' date.");
            return;
        }

        const $btn = $('button[onclick="bsApp.generateReport()"]');
        const ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-1"></i> Calculating...');

        try {
            const res = await api.get(`/api/finance/reports/balance-sheet?asOfDate=${asOf}`);
            this._currentData = res;
            this.renderStatement(res);
        } catch (e) {
            toastr.error(e.responseJSON?.messages?.[0] || "Failed to generate Balance Sheet.");
            $('#StatementCard').hide();
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    },

    renderStatement: function (data) {
        $('#StatementCard').show();
        
        const dateStr = new Date(data.asOfDate).toLocaleDateString();
        $('#lblReportPeriod').text(`As of: ${dateStr}`);

        // Display Warning if math is broken!
        if (!data.isBalanced) {
            $('#outOfBalanceWarning').show();
        } else {
            $('#outOfBalanceWarning').hide();
        }

        const formatMoney = (val) => val.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

        const renderRows = (accounts) => {
            if (!accounts || accounts.length === 0) return `<tr><td colspan="2" class="text-muted fst-italic acct-indent py-2">No activity.</td></tr>`;
            return accounts.map(a => `
                <tr class="acct-row">
                    <td class="acct-indent py-1"><span class="text-muted me-2">${a.accountCode}</span> ${a.accountName}</td>
                    <td class="text-end py-1">${formatMoney(a.balance)}</td>
                </tr>
            `).join('');
        };

        $('#assetsBody').html(renderRows(data.assetAccounts));
        $('#lblTotalAssets').text(formatMoney(data.totalAssets));

        $('#liabilitiesBody').html(renderRows(data.liabilityAccounts));
        $('#lblTotalLiabilities').text(formatMoney(data.totalLiabilities));

        $('#equityBody').html(renderRows(data.equityAccounts));
        $('#lblTotalEquity').text(formatMoney(data.totalEquity));

        $('#lblTotalLiabilitiesAndEquity').text(formatMoney(data.totalLiabilitiesAndEquity));
    },

    exportExcel: function () {
        if (!this._currentData) return;
        const wb = XLSX.utils.table_to_book(document.getElementById('bsTable'), {sheet: "Balance Sheet"});
        XLSX.writeFile(wb, `Balance_Sheet_AsOf_${$('#AsOfDate').val()}.xlsx`);
    }
};

$(document).ready(() => bsApp.init());