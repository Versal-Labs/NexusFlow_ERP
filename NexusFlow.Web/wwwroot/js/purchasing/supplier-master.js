window.supplierApp = {
    _table: null,
    _modal: null,
    _locationData: [],
    _bankData: [],

    init: function () {
        var modalEl = document.getElementById('supplierModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });

        this._initGrid();
        this._loadLookups();
        this._registerEvents();
    },

    _registerEvents: function () {
        const self = this;

        // 1. CASCADING LOCATION DROPDOWNS
        $('#Province').on('change', function () {
            const provName = $(this).val();
            let $dist = $('#District').empty().append('<option value="">-- Select District --</option>');
            $('#City').empty().append('<option value="">-- Select City --</option>').prop('disabled', true);

            if (provName) {
                const prov = self._locationData.find(p => p.name === provName);
                if (prov && prov.districts) {
                    prov.districts.forEach(d => $dist.append(`<option value="${d.name}">${d.name}</option>`));
                    $dist.prop('disabled', false);
                }
            } else {
                $dist.prop('disabled', true);
            }
        });

        $('#District').on('change', function () {
            const distName = $(this).val();
            const provName = $('#Province').val();
            let $city = $('#City').empty().append('<option value="">-- Select City --</option>');

            if (distName && provName) {
                const prov = self._locationData.find(p => p.name === provName);
                const dist = prov.districts.find(d => d.name === distName);
                if (dist && dist.cities) {
                    dist.cities.forEach(c => $city.append(`<option value="${c.name}">${c.name}</option>`));
                    $city.prop('disabled', false);
                }
            } else {
                $city.prop('disabled', true);
            }
        });

        // 2. CASCADING BANK DROPDOWNS
        $('#BankId').on('change', function () {
            const bankId = parseInt($(this).val());
            let $branch = $('#BankBranchId').empty().append('<option value="">-- Select Branch --</option>');

            if (bankId) {
                const bank = self._bankData.find(b => b.id === bankId);
                if (bank && bank.branches) {
                    bank.branches.forEach(br => $branch.append(`<option value="${br.id}">[${br.branchCode}] ${br.branchName}</option>`));
                    $branch.prop('disabled', false);
                }
            } else {
                $branch.prop('disabled', true);
            }
        });

        // 3. FINANCIAL LOCK TOGGLE
        $('#DefaultPayableAccountId, #DefaultExpenseAccountId').on('mousedown', function (e) {
            if (!$('#unlockAccountsToggle').is(':checked')) {
                e.preventDefault();
                toastr.warning("GL Accounts are locked by default to protect financial integrity. Toggle 'Unlock GL Mapping' to edit.");
            }
        });

        $('#unlockAccountsToggle').on('change', function () {
            if ($(this).is(':checked')) {
                $('#DefaultPayableAccountId, #DefaultExpenseAccountId').removeClass('locked-field');
            } else {
                $('#DefaultPayableAccountId, #DefaultExpenseAccountId').addClass('locked-field');
            }
        });

        // 4. DATATABLES FILTERS
        $('#filterDistrict, #filterCity').on('change', function () {
            self._table.ajax.reload();
        });

        // 5. "SAME AS PRIMARY" EMAIL LOGIC
        $('#chkSameAsPrimary').on('change', function () {
            if ($(this).is(':checked')) {
                // Copy the value and lock the field
                $('#AccountsEmail').val($('#Email').val()).prop('readonly', true).addClass('bg-light');
            } else {
                // Unlock the field
                $('#AccountsEmail').prop('readonly', false).removeClass('bg-light');
            }
        });

        // If the user fixes a typo in the Primary Email while the box is checked, update it live!
        $('#Email').on('input', function () {
            if ($('#chkSameAsPrimary').is(':checked')) {
                $('#AccountsEmail').val($(this).val());
            }
        });
    },

    _initGrid: function () {
        this._table = $('#supplierGrid').DataTable({
            ajax: function (data, callback, settings) {
                (async () => {
                    try {
                        const response = await api.get('/api/Supplier');
                        let rowData = response.data || response || [];

                        const distFilter = $('#filterDistrict').val();
                        const cityFilter = $('#filterCity').val();

                        // Fallback checking in case old DTO didn't project locations
                        if (distFilter) rowData = rowData.filter(d => d.district === distFilter);
                        if (cityFilter) rowData = rowData.filter(d => d.city === cityFilter);

                        callback({ data: rowData });
                    } catch (e) {
                        callback({ data: [] });
                    }
                })();
                return { abort: function () { } }; // TIER-1 FIX: Prevents abort() crash
            },
            columns: [
                { data: 'name', className: 'fw-bold text-dark' },
                { data: 'taxRegNo', className: 'font-monospace text-muted' },
                {
                    data: null,
                    render: function (d) {
                        return `<div><i class="fa-solid fa-user text-muted me-1"></i>${d.contactPerson || '-'}</div>
                                <div class="small text-muted">${d.email || ''}</div>`;
                    }
                },
                { data: 'district', defaultContent: '-' },
                { data: 'supplierGroupName', defaultContent: '-' },
                {
                    data: 'isActive',
                    className: 'text-center',
                    render: d => d ? '<span class="badge bg-success bg-opacity-10 text-success border">Active</span>'
                        : '<span class="badge bg-danger bg-opacity-10 text-danger border">Inactive</span>'
                },
                {
                    data: 'id', className: 'text-end pe-3', orderable: false,
                    render: function (data) {
                        return `<button type="button" class="btn btn-sm btn-light border px-2 shadow-sm" onclick="supplierApp.edit(${data})"><i class="fa-solid fa-pen-to-square text-primary"></i> Edit</button>`;
                    }
                }
            ],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    _loadLookups: async function () {
        try {
            const [groupRes, termRes, accRes, locRes, bankRes] = await Promise.all([
                api.get('/api/config/lookups?type=SupplierGroup'),
                api.get('/api/config/lookups?type=PaymentTerm'),
                api.get('/api/finance/accounts'),
                api.get('/api/locations/provinces'),
                api.get('/api/banks')
            ]);

            this._locationData = Array.isArray(locRes) ? locRes : (locRes?.data || []);
            if (!Array.isArray(this._locationData)) this._locationData = [];

            this._bankData = Array.isArray(bankRes) ? bankRes : (bankRes?.data || []);
            if (!Array.isArray(this._bankData)) this._bankData = [];

            const groups = Array.isArray(groupRes) ? groupRes : (groupRes?.data || []);
            const terms = Array.isArray(termRes) ? termRes : (termRes?.data || []);
            const accounts = Array.isArray(accRes) ? accRes : (accRes?.data || []);

            this._populateDropdown('SupplierGroupId', groups);
            this._populateDropdown('PaymentTermId', terms);

            // Populate Provinces & Filters
            let $prov = $('#Province').empty().append('<option value="">-- Select Province --</option>');
            let $fDist = $('#filterDistrict');
            this._locationData.forEach(p => {
                $prov.append(`<option value="${p.name}">${p.name}</option>`);
                p.districts.forEach(d => {
                    $fDist.append(`<option value="${d.name}">${d.name}</option>`);
                    d.cities.forEach(c => $('#filterCity').append(`<option value="${c.name}">${c.name}</option>`));
                });
            });

            // Populate Banks
            let $bank = $('#BankId');
            this._bankData.forEach(b => $bank.append(`<option value="${b.id}">[${b.bankCode}] ${b.name}</option>`));

            if (accounts.length > 0) {
                var apAccounts = accounts.filter(a => a.type === 'Liability' || a.type === '2');
                var expAccounts = accounts.filter(a => a.type === 'Expense' || a.type === '5');

                this._populateDropdown('DefaultPayableAccountId', apAccounts, 'id', 'name', 'code');
                this._populateDropdown('DefaultExpenseAccountId', expAccounts, 'id', 'name', 'code');
            }
        } catch (e) {
            console.error("[SupplierApp] Lookup Load Error:", e);
        }
    },

    _populateDropdown: function (id, data, valProp = 'id', textProp = 'value', codeProp = null) {
        var $el = $(`#${id}`);
        $el.find('option:not(:first)').remove();
        data.forEach(item => {
            var text = codeProp ? `[${item[codeProp]}] ${item[textProp]}` : item[textProp];
            $el.append($('<option></option>').val(item[valProp]).text(text));
        });
    },

    openEditor: function () {
        $('#frmSupplier')[0].reset();
        $('#drawerTitle').html('<i class="fa-solid fa-truck-field text-primary me-2"></i>New Vendor');
        $('#Id').val(0);

        $('#District, #City, #BankBranchId').empty().prop('disabled', true);

        // RELOCK ACCOUNTS & AUTO SELECT
        $('#unlockAccountsToggle').prop('checked', false).trigger('change');

        // Reset the checkbox state
        $('#chkSameAsPrimary').prop('checked', false);
        $('#AccountsEmail').prop('readonly', false).removeClass('bg-light');

        // Target Accounts Payable (e.g. 2110)
        const apAcc = $('#DefaultPayableAccountId option:contains("[2110]")').val();
        if (apAcc) $('#DefaultPayableAccountId').val(apAcc);

        this._modal.show();
    },

    edit: async function (id) {
        const res = await api.get(`/api/Supplier/${id}`);
        if (!res || (!res.succeeded && !res.id)) {
            toastr.error("Could not load supplier"); return;
        }

        const d = res.data || res;
        $('#frmSupplier')[0].reset();
        $('#drawerTitle').html('<i class="fa-solid fa-truck-field text-primary me-2"></i>Edit Vendor');
        $('#Id').val(d.id);

        // Bind Base Fields manually for strict typing
        $('#Name').val(d.name);
        $('#TradeName').val(d.tradeName);
        $('#TaxRegNo').val(d.taxRegNo);
        $('#BusinessRegNo').val(d.businessRegNo);
        $('#SupplierGroupId').val(d.supplierGroupId);
        $('#ContactPerson').val(d.contactPerson);
        $('#Email').val(d.email);

        $('#AccountsEmail').val(d.accountsEmail);

        // Smart detection: If emails match (and aren't empty), auto-check the box!
        if (d.email && d.accountsEmail && d.email.toLowerCase() === d.accountsEmail.toLowerCase()) {
            $('#chkSameAsPrimary').prop('checked', true);
            $('#AccountsEmail').prop('readonly', true).addClass('bg-light');
        } else {
            $('#chkSameAsPrimary').prop('checked', false);
            $('#AccountsEmail').prop('readonly', false).removeClass('bg-light');
        }

        $('#Phone').val(d.phone);
        $('#AddressLine1').val(d.addressLine1);
        $('#CurrencyCode').val(d.currencyCode);
        $('#PaymentTermId').val(d.paymentTermId);
        $('#CreditLimit').val(d.creditLimit);
        $('#BankAccountNumber').val(d.bankAccountNumber);
        $('#IsActive').prop('checked', d.isActive);

        // Bind Hierarchies
        $('#Province').val(d.province).trigger('change');
        setTimeout(() => {
            $('#District').val(d.district).trigger('change');
            setTimeout(() => { $('#City').val(d.city); }, 50);
        }, 50);

        if (d.bankId) {
            $('#BankId').val(d.bankId).trigger('change');
            setTimeout(() => { $('#BankBranchId').val(d.bankBranchId); }, 50);
        }

        // Bind & Lock Accounts
        $('#unlockAccountsToggle').prop('checked', false).trigger('change');
        $('#DefaultPayableAccountId').val(d.defaultPayableAccountId);
        $('#DefaultExpenseAccountId').val(d.defaultExpenseAccountId);

        this._modal.show();
    },

    saveSupplier: async function (e) {
        var form = document.getElementById('frmSupplier');
        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        var $btn = $(e.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-1"></i> Saving...');

        var formData = new FormData(form);
        var payload = Object.fromEntries(formData.entries());

        // TIER-1 FIX: Sanitize empty strings to null to prevent ASP.NET validation crashes
        for (let key in payload) {
            if (payload[key] === "") payload[key] = null;
        }

        // Parse Types
        payload.Id = parseInt(payload.Id) || 0;
        payload.SupplierGroupId = parseInt(payload.SupplierGroupId) || 0;
        payload.PaymentTermId = parseInt(payload.PaymentTermId) || 0;
        payload.DefaultPayableAccountId = parseInt(payload.DefaultPayableAccountId) || 0;
        payload.DefaultExpenseAccountId = payload.DefaultExpenseAccountId ? parseInt(payload.DefaultExpenseAccountId) : null;
        payload.CreditLimit = parseFloat(payload.CreditLimit) || 0;
        payload.BankId = payload.BankId ? parseInt(payload.BankId) : null;
        payload.BankBranchId = payload.BankBranchId ? parseInt(payload.BankBranchId) : null;
        payload.IsActive = $('#IsActive').is(':checked');

        try {
            const response = payload.Id === 0
                ? await api.post('/api/Supplier', { Supplier: payload })
                : await api.put(`/api/Supplier/${payload.Id}`, { Supplier: payload });

            if (response && response.succeeded) {
                toastr.success(response.message || "Vendor saved successfully.");
                this._modal.hide();
                this._table.ajax.reload(null, false);
            } else {
                toastr.error(response?.messages?.[0] || "Failed to save vendor.");
            }
        } catch (err) {
            toastr.error("Network error during save.");
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    }
};

$(document).ready(() => supplierApp.init());