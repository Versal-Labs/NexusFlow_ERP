window.vaultApp = {
    _table: null,
    _bounceModal: null,
    _endorseModal: null,

    init: function () {
        var bounceEl = document.getElementById('bounceModal');
        if (bounceEl) this._bounceModal = new bootstrap.Modal(bounceEl, { backdrop: 'static' });

        this._initFilters();
        this._initGrid();

        var endorseEl = document.getElementById('endorseModal');
        if (endorseEl) this._endorseModal = new bootstrap.Modal(endorseEl, { backdrop: 'static' });

        const self = this;
        $('#EndorseSupplierId').on('change', function() {
            const suppId = $(this).val();
            if (suppId) self._loadUnpaidSupplierBills(suppId);
            else {
                $('#endorseAllocationsBody').html('<tr><td colspan="5" class="text-center text-muted py-3">Select a supplier.</td></tr>');
                self._calcEndorseTotals();
            }
        });

        $('#endorseAllocationsBody').on('input', '.endorse-alloc-input', () => self._calcEndorseTotals());
    },

    // ==========================================
    // CASCADING FILTERS
    // ==========================================
    _initFilters: async function () {
        try {
            const [custRes, bankRes] = await Promise.all([
                api.get('/api/customer'),
                api.get('/api/finance/banks')
            ]);

            let $cust = $('#filterCustomer').empty().append('<option value="">All Customers</option>');
            (custRes.data || custRes || []).forEach(c => $cust.append(`<option value="${c.id}">${c.name}</option>`));
            $cust.select2();

            let $bank = $('#filterBank').empty().append('<option value="">All Banks</option>');
            (bankRes.data || bankRes || []).forEach(b => $bank.append(`<option value="${b.id}">[${b.bankCode}] ${b.name}</option>`));
            $bank.select2();

            $('#filterBranch').empty().append('<option value="">All Branches</option>').select2();

        } catch (e) { console.error("Filter Load Error", e); }
    },

    onBankFilterChange: async function() {
        const bankId = $('#filterBank').val();
        let $branch = $('#filterBranch');
        
        if (!bankId) {
            $branch.empty().append('<option value="">All Branches</option>').prop('disabled', true).trigger('change');
            return;
        }

        try {
            $branch.prop('disabled', true).append('<option>Loading...</option>');
            const res = await api.get(`/api/finance/banks/${bankId}/branches`);
            $branch.empty().append('<option value="">All Branches</option>');
            (res.data || res || []).forEach(b => $branch.append(`<option value="${b.id}">[${b.branchCode}] ${b.branchName}</option>`));
            $branch.prop('disabled', false).trigger('change');
        } catch(e) { toastr.error("Failed to load branches."); }
    },

    resetFilters: function() {
        $('#filterCustomer').val('').trigger('change');
        $('#filterStatus').val('0'); // Default back to In Safe
        $('#filterBank').val('').trigger('change');
        this.reloadGrid();
    },

    reloadGrid: function() {
        this._table.ajax.reload();
    },

    // ==========================================
    // DATA GRID
    // ==========================================
    _initGrid: function () {
        this._table = $('#chequeGrid').DataTable({
            ajax: {
                url: '/api/treasury/cheques',
                data: function (d) {
                    d.customerId = $('#filterCustomer').val();
                    d.bankId = $('#filterBank').val();
                    d.branchId = $('#filterBranch').val();
                    d.status = $('#filterStatus').val();
                },
                dataSrc: function (json) { return json.data || json || []; }
            },
            columns: [
                { data: 'chequeNumber', className: 'fw-bold text-dark font-monospace' },
                { data: null, render: row => `${row.bankName} - ${row.branchName}` },
                { 
                    data: 'chequeDate', 
                    render: function(d, type, row) {
                        let date = new Date(d);
                        let today = new Date();
                        // Highlight red if the PDC date has passed and it's still in the safe!
                        let isOverdue = (date <= today && row.statusId === 0) ? 'text-danger fw-bold' : 'text-dark fw-bold';
                        return `<span class="${isOverdue}">${date.toLocaleDateString()}</span>`;
                    }
                },
                { data: 'customerName', className: 'text-muted' },
                { data: 'amount', className: 'text-end fw-bold text-success', render: d => d.toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                {
                    data: 'statusId',
                    className: 'text-center',
                    render: function(d) {
                        if(d === 0) return '<span class="badge bg-warning text-dark"><i class="fa-solid fa-safe me-1"></i>In Safe</span>';
                        if(d === 1) return '<span class="badge bg-info text-dark"><i class="fa-solid fa-building-columns me-1"></i>Deposited</span>';
                        if(d === 2) return '<span class="badge bg-secondary"><i class="fa-solid fa-handshake me-1"></i>Endorsed</span>';
                        if(d === 3) return '<span class="badge bg-success"><i class="fa-solid fa-check-double me-1"></i>Cleared</span>';
                        if(d === 4) return '<span class="badge bg-danger"><i class="fa-solid fa-burst me-1"></i>Bounced</span>';
                        return d;
                    }
                },
                {
                    data: null,
                    className: 'text-end pe-3',
                    orderable: false,
                    render: function (data, type, row) {
                        let btns = '';
                        // You can only bounce cheques that are NOT already bounced or cleared.
                        if (row.statusId === 0 || row.statusId === 1 || row.statusId === 2) {
                            btns += `<button class="btn btn-sm btn-outline-danger shadow-sm" onclick="vaultApp.openBounceModal(${row.id})" title="Dishonor / Bounce"><i class="fa-solid fa-burst"></i> Bounce</button>`;
                        }

                        // ACTIONS ONLY AVAILABLE WHEN THE CHEQUE IS PHYSICALLY IN THE SAFE
                        if (row.statusId === 0) { 
                        
                            // ACTION: ENDORSE TO SUPPLIER
                            btns += `<button class="btn btn-sm btn-secondary shadow-sm ms-1" onclick="vaultApp.openEndorseModal(${row.id}, '${row.chequeNumber}', ${row.amount})" title="Endorse to Supplier"><i class="fa-solid fa-handshake"></i> Endorse</button>`;
                        
                            // ACTION: DEPOSIT TO BANK (Just in case you missed this from earlier!)
                            btns += `<button class="btn btn-sm btn-primary shadow-sm ms-1" onclick="vaultApp.openDeposit(${row.id}, '${row.chequeNumber}', ${row.amount})" title="Deposit to Bank"><i class="fa-solid fa-building-columns"></i> Deposit</button>`;
                        }
                        return btns;
                    }
                }
            ],
            order: [[2, 'asc']], // Sort by PDC Date so oldest cheques are at the top
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    // ==========================================
    // ENDRORSE LOGIC
    // ==========================================

    openEndorseModal: async function(id, chequeNo, amount) {
        $('#endorseForm')[0].reset();
        $('#EndorseChequeId').val(id);
        $('#EndorseChequeAmount').val(amount);
        $('#lblEndorseChequeNo').text(chequeNo);
        $('#lblEndorseAmount').text('$' + parseFloat(amount).toLocaleString(undefined, { minimumFractionDigits: 2 }));
        document.getElementById('EndorseDate').valueAsDate = new Date();
        
        // Load Suppliers safely
        try {
            const res = await api.get('/api/supplier');
            let $ddl = $('#EndorseSupplierId').empty().append('<option value="">-- Select Supplier --</option>');
            (res.data || res || []).forEach(s => $ddl.append(`<option value="${s.id}">${s.name}</option>`));
            $ddl.select2({ dropdownParent: $('#endorseModal') });
        } catch (e) { }

        $('#endorseAllocationsBody').html('<tr><td colspan="5" class="text-center text-muted py-3">Select a supplier.</td></tr>');
        this._calcEndorseTotals();
        this._endorseModal.show();
    },

    _loadUnpaidSupplierBills: async function(supplierId) {
        $('#endorseAllocationsBody').html('<tr><td colspan="5" class="text-center py-3"><i class="spinner-border spinner-border-sm text-secondary"></i> Loading...</td></tr>');
        try {
            // Note: Make sure you have an API endpoint to fetch unpaid supplier bills!
            const res = await api.get(`/api/purchasing/suppliers/${supplierId}/unpaid-bills`);
            const bills = res.data || res || [];
            let html = '';
            
            if (bills.length === 0) {
                html = '<tr><td colspan="5" class="text-center text-success fw-bold py-3">No unpaid bills for this supplier.</td></tr>';
            } else {
                bills.forEach(b => {
                    html += `
                    <tr data-id="${b.id}">
                        <td class="font-monospace fw-bold">${b.billNumber}</td>
                        <td>${new Date(b.date).toLocaleDateString()}</td>
                        <td class="text-end">${b.total.toLocaleString()}</td>
                        <td class="text-end fw-bold">${b.balance.toLocaleString()}</td>
                        <td>
                            <input type="number" class="form-control form-control-sm text-end fw-bold text-success endorse-alloc-input" 
                                   data-balance="${b.balance}" value="0.00" min="0" max="${b.balance}" step="0.01" onclick="this.select()">
                        </td>
                    </tr>`;
                });
            }
            $('#endorseAllocationsBody').html(html);
        } catch (e) { $('#endorseAllocationsBody').html('<tr><td colspan="5" class="text-center text-danger">Error loading bills.</td></tr>'); }
    },

    autoAllocateEndorse: function() {
        let remaining = parseFloat($('#EndorseChequeAmount').val()) || 0;
        
        $('.endorse-alloc-input').each(function() {
            let max = parseFloat($(this).data('balance')) || 0;
            if (remaining >= max) {
                $(this).val(max.toFixed(2));
                remaining -= max;
            } else if (remaining > 0) {
                $(this).val(remaining.toFixed(2));
                remaining = 0;
            } else {
                $(this).val('0.00');
            }
        });
        this._calcEndorseTotals();
    },

    _calcEndorseTotals: function() {
        let chequeAmt = parseFloat($('#EndorseChequeAmount').val()) || 0;
        let allocated = 0;

        $('.endorse-alloc-input').each(function() {
            let val = parseFloat($(this).val()) || 0;
            let max = parseFloat($(this).data('balance')) || 0;
            if (val > max) { val = max; $(this).val(max.toFixed(2)); }
            allocated += val;
        });

        $('#lblEndorseAllocated').text(allocated.toLocaleString(undefined, { minimumFractionDigits: 2 }));
        $('#lblEndorseUnallocated').text((chequeAmt - allocated).toLocaleString(undefined, { minimumFractionDigits: 2 }));
    },

    executeEndorse: async function() {
        var form = $('#endorseForm')[0];
        if (!form.checkValidity()) { form.reportValidity(); return; }

        let chequeAmt = parseFloat($('#EndorseChequeAmount').val()) || 0;
        let allocated = 0;
        let allocs = [];

        $('.endorse-alloc-input').each(function() {
            let v = parseFloat($(this).val()) || 0;
            if (v > 0) {
                allocated += v;
                allocs.push({ InvoiceId: parseInt($(this).closest('tr').data('id')), Amount: v });
            }
        });

        if (allocated > chequeAmt) {
            toastr.error("Cannot allocate more money than the cheque value."); return;
        }

        const payload = {
            ChequeId: parseInt($('#EndorseChequeId').val()),
            SupplierId: parseInt($('#EndorseSupplierId').val()),
            EndorsementDate: $('#EndorseDate').val(),
            Allocations: allocs
        };

        var $btn = $(event.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm"></i> Swapping...');

        try {
            const res = await api.post('/api/treasury/cheques/endorse', payload);
            if (res && res.succeeded) {
                toastr.success("Cheque successfully endorsed to supplier!");
                this._endorseModal.hide();
                this._table.ajax.reload(null, false);
            } else if (res && res.messages) { toastr.error(res.messages[0]); }
        } catch (e) { toastr.error("Failed to endorse cheque."); }
        finally { $btn.prop('disabled', false).html(ogText); }
    },

    // ==========================================
    // BOUNCE LOGIC
    // ==========================================
    openBounceModal: function(id) {
        $('#bounceForm')[0].reset();
        $('#BounceChequeId').val(id);
        document.getElementById('BounceDate').valueAsDate = new Date();
        this._bounceModal.show();
    },

    executeBounce: async function() {
        var form = $('#bounceForm')[0];
        if (!form.checkValidity()) { form.reportValidity(); return; }

        const payload = {
            ChequeId: parseInt($('#BounceChequeId').val()),
            Reason: $('#BounceReason').val(),
            BounceDate: $('#BounceDate').val()
        };

        var $btn = $(event.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm"></i> Executing...');

        try {
            const res = await api.post(`/api/treasury/cheques/bounce`, payload);
            if (res && res.succeeded) {
                toastr.success("Cheque Bounced. Accounting records have been successfully reversed.");
                this._bounceModal.hide();
                this._table.ajax.reload(null, false);
            } else if (res && res.messages) {
                toastr.error(res.messages[0]);
            }
        } catch (e) {
            toastr.error(e.responseJSON?.messages?.[0] || "Failed to bounce cheque.");
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    }
};

$(document).ready(() => vaultApp.init());