window.tbImportApp = {
    _currentData: [],

    _getToken: function() {
        return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') || '';
    },

    downloadTemplate: function() {
        const csvContent = "data:text/csv;charset=utf-8,AccountCode,Debit,Credit\n1001,50000.00,0\n2001,0,25000.00\n3001,0,25000.00";
        const encodedUri = encodeURI(csvContent);
        const link = document.createElement("a");
        link.setAttribute("href", encodedUri);
        link.setAttribute("download", "Trial_Balance_Migration_Template.csv");
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    },

    previewCsv: async function() {
        const fileInput = document.getElementById('csvFileInput');
        if (!fileInput.files.length) {
            toastr.warning("Please select a CSV file first."); return;
        }

        const formData = new FormData();
        formData.append('file', fileInput.files[0]);

        var $btn = $('button[onclick="tbImportApp.previewCsv()"]');
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Parsing...');

        try {
            const response = await fetch('/api/finance/preview-tb-import', {
                method: 'POST',
                headers: { 'RequestVerificationToken': this._getToken() },
                body: formData
            });
            
            const res = await response.json();
            
            if (res.succeeded) {
                this._currentData = res.data;
                this.renderTable();
                $('#CutoverDate').val(new Date().toISOString().split('T')[0]); // Default to today
                $('#uploadSection').addClass('d-none');
                $('#previewSection').removeClass('d-none');
                toastr.success("Trial Balance parsed successfully.");
            } else {
                toastr.error(res.messages?.[0] || "Failed to parse CSV.");
            }
        } catch (e) {
            toastr.error("Network error during file upload.");
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    },

    renderTable: function() {
        let html = '';
        this._currentData.forEach((row, index) => {
            html += `
                <tr id="row_${index}">
                    <td><input type="text" class="form-control form-control-sm border-0 bg-transparent row-data fw-bold font-monospace text-primary" data-field="accountCode" value="${row.accountCode}"></td>
                    <td class="bg-primary bg-opacity-10"><input type="number" class="form-control form-control-sm border-0 bg-transparent row-data text-end fw-bold text-dark debit-input" data-field="debit" value="${row.debit}" step="0.01"></td>
                    <td class="bg-warning bg-opacity-10"><input type="number" class="form-control form-control-sm border-0 bg-transparent row-data text-end fw-bold text-dark credit-input" data-field="credit" value="${row.credit}" step="0.01"></td>
                    <td class="text-center align-middle">
                        <button class="btn btn-sm text-danger" onclick="$('#row_${index}').remove(); tbImportApp.calculateTotals();"><i class="fa-solid fa-trash-can"></i></button>
                    </td>
                </tr>`;
        });
        
        $('#previewBody').html(html);
        
        // Bind event to recalculate totals when amounts change
        $('.debit-input, .credit-input').on('change input', () => this.calculateTotals());
        this.calculateTotals();
    },

    calculateTotals: function() {
        let totalDebit = 0;
        let totalCredit = 0;

        $('#previewBody tr').each(function() {
            let debit = parseFloat($(this).find('.debit-input').val()) || 0;
            let credit = parseFloat($(this).find('.credit-input').val()) || 0;
            
            totalDebit += debit;
            totalCredit += credit;
        });

        const variance = Math.abs(totalDebit - totalCredit);
        const formatMoney = (val) => '$' + val.toLocaleString(undefined, {minimumFractionDigits: 2, maximumFractionDigits: 2});

        $('#lblTotalDebit').text(formatMoney(totalDebit));
        $('#lblTotalCredit').text(formatMoney(totalCredit));
        $('#lblVariance').text(formatMoney(variance));

        // STRICT ACCOUNTING ENFORCEMENT
        const $varianceLbl = $('#lblVariance');
        const $btnCommit = $('#btnCommit');

        // Round to 2 decimal places to avoid floating point math bugs (e.g. 0.0000000001 difference)
        if (Math.round(variance * 100) / 100 === 0) {
            $varianceLbl.removeClass('text-danger').addClass('text-success');
            $btnCommit.prop('disabled', false);
        } else {
            $varianceLbl.removeClass('text-success').addClass('text-danger');
            $btnCommit.prop('disabled', true);
        }
    },

    executeImport: async function(event) {
        let finalData = [];
        let hasErrors = false;

        $('#previewBody tr').each(function() {
            let row = {};
            $(this).find('.row-data').each(function() {
                let field = $(this).data('field');
                let val = $(this).val();
                if (field === 'debit' || field === 'credit') row[field] = parseFloat(val) || 0;
                else row[field] = val;
            });
            
            if (!row.accountCode) {
                $(this).addClass('table-danger');
                hasErrors = true;
            } else {
                $(this).removeClass('table-danger');
            }
            
            finalData.push(row);
        });

        const cutoverDate = $('#CutoverDate').val();
        if (!cutoverDate) {
            toastr.error("Please select a Cutover Date.");
            return;
        }

        if (hasErrors || finalData.length === 0) {
            toastr.warning("Please provide valid account codes for all rows.");
            return;
        }

        var $btn = $(event.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-1"></i> Committing to GL...');

        const payload = {
            CutoverDate: cutoverDate,
            Lines: finalData
        };

        try {
            const response = await fetch('/api/finance/execute-tb-import', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': this._getToken()
                },
                body: JSON.stringify(payload)
            });
            
            const res = await response.json();
            
            if (res.succeeded) {
                Swal.fire({
                    title: 'Trial Balance Posted!',
                    text: res.message,
                    icon: 'success',
                    confirmButtonColor: '#198754'
                }).then(() => {
                    // Redirect them directly to the Trial Balance Report to admire their work!
                    window.location.href = '/Finance/TrialBalance'; 
                });
            } else {
                toastr.error(res.messages?.[0] || "Migration failed. Check your GL mappings.");
            }
        } catch (e) {
            toastr.error("Network error during execution.");
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    }
};
</script>