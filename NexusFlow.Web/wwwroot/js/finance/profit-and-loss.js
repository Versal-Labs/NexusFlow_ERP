window.plApp = {
    _currentData: null,

    init: function () {
        // Bind the Date Preset Dropdown
        $('#DatePreset').on('change', (e) => this.applyDatePreset($(e.target).val()));
        
        // Auto-switch to "Custom" if the user manually changes the dates
        $('#StartDate, #EndDate').on('change', () => $('#DatePreset').val('Custom'));

        // Initialize to "This Month"
        this.applyDatePreset('ThisMonth');
        this.generateReport();
    },

    applyDatePreset: function(preset) {
        if (preset === 'Custom') return;

        const today = new Date();
        let start, end;

        switch (preset) {
            case 'Today':
                start = today; end = today;
                break;
            case 'ThisWeek':
                const day = today.getDay() || 7; 
                start = new Date(today); start.setDate(today.getDate() - day + 1);
                end = new Date(start); end.setDate(start.getDate() + 6);
                break;
            case 'ThisMonth':
                start = new Date(today.getFullYear(), today.getMonth(), 1);
                end = new Date(today.getFullYear(), today.getMonth() + 1, 0);
                break;
            case 'ThisQuarter':
                const quarter = Math.floor(today.getMonth() / 3);
                start = new Date(today.getFullYear(), quarter * 3, 1);
                end = new Date(start.getFullYear(), start.getMonth() + 3, 0);
                break;
            case 'ThisYear':
                start = new Date(today.getFullYear(), 0, 1);
                end = new Date(today.getFullYear(), 11, 31);
                break;
            case 'LastMonth':
                start = new Date(today.getFullYear(), today.getMonth() - 1, 1);
                end = new Date(today.getFullYear(), today.getMonth(), 0);
                break;
            case 'LastYear':
                start = new Date(today.getFullYear() - 1, 0, 1);
                end = new Date(today.getFullYear() - 1, 11, 31);
                break;
        }

        const formatDate = (dateObj) => {
            const year = dateObj.getFullYear();
            const month = String(dateObj.getMonth() + 1).padStart(2, '0');
            const d = String(dateObj.getDate()).padStart(2, '0');
            return `${year}-${month}-${d}`;
        };

        $('#StartDate').val(formatDate(start));
        $('#EndDate').val(formatDate(end));
    },

    generateReport: async function () {
        const start = $('#StartDate').val();
        const end = $('#EndDate').val();
        const basis = $('#ReportBasis').val();

        if (!start || !end) {
            toastr.error("Please select a valid date range.");
            return;
        }

        const $btn = $('button[onclick="plApp.generateReport()"]');
        const ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="fa-solid fa-spinner fa-spin me-1"></i> Calculating...');

        try {
            const res = await api.get(`/api/finance/reports/profit-and-loss?startDate=${start}&endDate=${end}&basis=${basis}`);
            this._currentData = res;
            this.renderStatement(res);
        } catch (e) {
            toastr.error(e.responseJSON?.messages?.[0] || "Failed to generate statement.");
            $('#StatementCard').hide();
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    },

    renderStatement: function (data) {
        $('#StatementCard').show();
        
        const sDate = new Date(data.startDate).toLocaleDateString();
        const eDate = new Date(data.endDate).toLocaleDateString();
        $('#lblReportPeriod').text(`For the Period: ${sDate} - ${eDate}`);
        $('#lblReportBasis').text(`Basis: ${data.basis}`);

        const formatMoney = (val) => val.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

        const renderRows = (accounts) => {
            if (!accounts || accounts.length === 0) return `<tr><td colspan="2" class="text-muted fst-italic acct-indent py-2">No activity in this period.</td></tr>`;
            return accounts.map(a => `
                <tr class="acct-row">
                    <td class="acct-indent py-1"><span class="text-muted me-2">${a.accountCode}</span> ${a.accountName}</td>
                    <td class="text-end py-1">${formatMoney(a.balance)}</td>
                </tr>
            `).join('');
        };

        $('#revenueBody').html(renderRows(data.revenueAccounts));
        $('#lblTotalRevenue').text(formatMoney(data.totalRevenue));

        $('#cogsBody').html(renderRows(data.cogsAccounts));
        $('#lblTotalCogs').text(formatMoney(data.totalCogs));

        $('#lblGrossProfit').text(formatMoney(data.grossProfit));

        $('#expenseBody').html(renderRows(data.expenseAccounts));
        $('#lblTotalExpenses').text(formatMoney(data.totalExpenses));

        const $ni = $('#lblNetIncome');
        $ni.text(formatMoney(data.netIncome));
        if (data.netIncome < 0) {
            $ni.removeClass('text-success').addClass('text-danger');
        } else {
            $ni.removeClass('text-danger').addClass('text-success');
        }
    },

    exportExcel: function () {
        if (!this._currentData) return;
        const wb = XLSX.utils.table_to_book(document.getElementById('plTable'), {sheet: "Profit & Loss"});
        XLSX.writeFile(wb, `Profit_And_Loss_${$('#StartDate').val()}_to_${$('#EndDate').val()}.xlsx`);
    }
};

$(document).ready(() => plApp.init());