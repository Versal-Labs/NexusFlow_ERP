window.arapImportApp = {
    _currentData: [],

    _getToken: function() {
        return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') || '';
    },

    previewCsv: async function() {
        const fileInput = document.getElementById('csvFileInput');
        if (!fileInput.files.length) {
            toastr.warning("Please select a CSV file first."); return;
        }

        const formData = new FormData();
        formData.append('file', fileInput.files[0]);

        var $btn = $('button[onclick="arapImportApp.previewCsv()"]');
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Parsing...');

        try {
            const response = await fetch('/api/finance/preview-arap-import', {
                method: 'POST',
                headers: { 'RequestVerificationToken': this._getToken() },
                body: formData
            });
            
            const res = await response.json();
            
            if (res.succeeded) {
                this._currentData = res.data;
                this.renderTable();
                $('#uploadSection').addClass('d-none');
                $('#previewSection').removeClass('d-none');
                toastr.success("File parsed successfully. Please review the rows.");
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
            
            // Tier-1 UX: Color code AR (Asset/Green) vs AP (Liability/Red)
            let typeColor = row.type.toUpperCase() === 'AR' ? 'bg-success text-success' : 'bg-danger text-danger';
            
            // Format date for the HTML5 date input (YYYY-MM-DD)
            let dateVal = row.date ? new Date(row.date).toISOString().split('T')[0] : '';

            html += `
                <tr id="row_${index}">
                    <td class="${typeColor} bg-opacity-10">
                        <select class="form-select form-select-sm border-0 bg-transparent row-data fw-bold text-center" data-field="type">
                            <option value="AR" ${row.type.toUpperCase() === 'AR' ? 'selected' : ''}>AR</option>
                            <option value="AP" ${row.type.toUpperCase() === 'AP' ? 'selected' : ''}>AP</option>
                        </select>
                    </td>
                    <td><input type="text" class="form-control form-control-sm border-0 bg-transparent row-data fw-bold" data-field="partyName" value="${row.partyName}"></td>
                    <td><input type="text" class="form-control form-control-sm border-0 bg-transparent row-data font-monospace text-primary" data-field="documentNo" value="${row.documentNo}"></td>
                    <td><input type="date" class="form-control form-control-sm border-0 bg-transparent row-data" data-field="date" value="${dateVal}"></td>
                    <td class="bg-warning bg-opacity-10"><input type="number" class="form-control form-control-sm border-0 bg-transparent row-data text-end fw-bold text-dark amount-input" data-field="outstandingAmount" value="${row.outstandingAmount}" step="0.01"></td>
                    <td class="text-center align-middle">
                        <button class="btn btn-sm text-danger" onclick="$('#row_${index}').remove(); arapImportApp.calculateTotals();"><i class="fa-solid fa-trash-can"></i></button>
                    </td>
                </tr>`;
        });
        
        $('#previewBody').html(html);
        
        // Bind event to recalculate totals when amounts change
        $('.amount-input, select[data-field="type"]').on('change input', () => this.calculateTotals());
        this.calculateTotals();
    },

    calculateTotals: function() {
        let totalAr = 0;
        let totalAp = 0;
        let count = 0;

        $('#previewBody tr').each(function() {
            let type = $(this).find('select[data-field="type"]').val();
            let amount = parseFloat($(this).find('input[data-field="outstandingAmount"]').val()) || 0;
            
            if (type === 'AR') totalAr += amount;
            if (type === 'AP') totalAp += amount;
            count++;
        });

        $('#lblTotalRows').text(`${count} Records`);
        $('#lblTotalAr').text('$' + totalAr.toLocaleString(undefined, {minimumFractionDigits: 2}));
        $('#lblTotalAp').text('$' + totalAp.toLocaleString(undefined, {minimumFractionDigits: 2}));
    },

    executeImport: async function(event) {
        let finalData = [];
        let hasValidationErrors = false;

        $('#previewBody tr').each(function() {
            let row = {};
            $(this).find('.row-data').each(function() {
                let field = $(this).data('field');
                let val = $(this).val();
                
                if (field === 'outstandingAmount') row[field] = parseFloat(val) || 0;
                else row[field] = val;
            });
            
            // STRICT VALIDATION
            if (!row.partyName || !row.documentNo || !row.date) {
                $(this).addClass('table-danger');
                hasValidationErrors = true;
            } else {
                $(this).removeClass('table-danger');
            }

            if (row.outstandingAmount <= 0) {
                $(this).find('input[data-field="outstandingAmount"]').addClass('is-invalid');
                hasValidationErrors = true;
            } else {
                $(this).find('input[data-field="outstandingAmount"]').removeClass('is-invalid');
            }
            
            finalData.push(row);
        });

        if (finalData.length === 0) {
            toastr.warning("No data to import."); return;
        }

        if (hasValidationErrors) {
            toastr.error("Please fix the highlighted rows. Document numbers and dates are required, and amounts must be greater than 0.");
            return;
        }

        var $btn = $(event.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-1"></i> Committing to GL...');

        try {
            const response = await fetch('/api/finance/execute-arap-import', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': this._getToken()
                },
                body: JSON.stringify({ invoices: finalData })
            });
            
            const res = await response.json();
            
            if (res.succeeded) {
                Swal.fire({
                    title: 'Migration Successful!',
                    text: res.message,
                    icon: 'success',
                    confirmButtonColor: '#198754'
                }).then(() => {
                    window.location.href = '/Sales/Invoices'; // Redirect to the AR grid to see the results
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