window.tbApp = {
    init: function () {
        this.loadData();
    },

    loadData: async function() {
        try {
            $('#tbBody').html('<tr><td colspan="5" class="text-center py-4"><i class="spinner-border text-primary"></i></td></tr>');
            
            // Calls your existing GetTrialBalanceQuery via the API
            const res = await api.get('/api/finance/trial-balance'); 
            
            // 1. Unwrap the Result<T> (Usually in res.data, res.value, or just res depending on your setup)
            const resultPayload = res.data || res.value || res;

            // 2. Extract the actual array from the TrialBalanceReport.Lines property
            const data = resultPayload.lines || resultPayload.Lines || [];
            
            let html = '';
            let tDebit = 0;
            let tCredit = 0;

            // Now data is guaranteed to be the array of TrialBalanceLine objects!
            data.forEach(row => {
                // Formatting values
                let dr = row.debit > 0 ? parseFloat(row.debit).toLocaleString(undefined, { minimumFractionDigits: 2 }) : '-';
                let cr = row.credit > 0 ? parseFloat(row.credit).toLocaleString(undefined, { minimumFractionDigits: 2 }) : '-';
                
                tDebit += row.debit;
                tCredit += row.credit;

                // Account Type formatting
                let typeBadge = `<span class="badge bg-secondary">${row.accountType || 'Account'}</span>`;
                if (row.accountType === 'Asset') typeBadge = `<span class="badge bg-info text-dark">Asset</span>`;
                if (row.accountType === 'Liability') typeBadge = `<span class="badge bg-warning text-dark">Liability</span>`;
                if (row.accountType === 'Revenue') typeBadge = `<span class="badge bg-success">Revenue</span>`;
                if (row.accountType === 'Expense') typeBadge = `<span class="badge bg-danger">Expense</span>`;

                html += `
                    <tr>
                        <td class="ps-4 font-monospace text-muted">${row.accountCode}</td>
                        <td class="fw-bold text-dark">${row.accountName}</td>
                        <td>${typeBadge}</td>
                        <td class="text-end">${dr}</td>
                        <td class="text-end pe-4">${cr}</td>
                    </tr>
                `;
            });

            if (data.length === 0) {
                html = '<tr><td colspan="5" class="text-center text-muted py-4">No transactions found.</td></tr>';
            }

            $('#tbBody').html(html);
            $('#totDebit').text(tDebit.toLocaleString(undefined, { minimumFractionDigits: 2 }));
            $('#totCredit').text(tCredit.toLocaleString(undefined, { minimumFractionDigits: 2 }));

            // Verification Check
            if (Math.abs(tDebit - tCredit) > 0.01) {
                $('#totDebit, #totCredit').removeClass('text-primary').addClass('text-danger');
                toastr.error("WARNING: Trial Balance is out of balance!");
            } else {
                $('#totDebit, #totCredit').removeClass('text-danger').addClass('text-success');
            }

        } catch (e) {
            console.error(e);
            $('#tbBody').html('<tr><td colspan="5" class="text-center text-danger py-4">Failed to load Trial Balance.</td></tr>');
            toastr.error("Failed to fetch ledger data.");
        }
    }
};

$(document).ready(() => window.tbApp.init());