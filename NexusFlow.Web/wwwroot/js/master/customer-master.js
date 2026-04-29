window.customerApp = {
    _table: null,
    _modal: null,
    _locationData: [], // Stores the JSON/DB structure
    _bankData: [],     // Stores the Bank/Branch structure

    init: function () {
        var modalEl = document.getElementById('customerModal');
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

        // 3. TIER-1 FINANCIAL LOCK TOGGLE
        // Prevent editing GL accounts unless authorized
        $('#DefaultReceivableAccountId, #DefaultRevenueAccountId').on('mousedown', function (e) {
            if (!$('#unlockAccountsToggle').is(':checked')) {
                e.preventDefault();
                toastr.warning("GL Accounts are locked by default to protect financial integrity. Toggle 'Unlock GL Mapping' to edit.");
            }
        });

        $('#unlockAccountsToggle').on('change', function () {
            const isUnlocked = $(this).is(':checked');
            if (isUnlocked) {
                $('#DefaultReceivableAccountId, #DefaultRevenueAccountId').removeClass('locked-field');
            } else {
                $('#DefaultReceivableAccountId, #DefaultRevenueAccountId').addClass('locked-field');
            }
        });

        // 4. DATATABLES FILTERS
        $('#filterDistrict, #filterCity').on('change', function () {
            self._table.ajax.reload();
        });
    },

    _initGrid: function () {
        this._table = $('#customerTable').DataTable({
            ajax: function (data, callback, settings) {
                // 1. Execute the async logic in an isolated block
                (async () => {
                    try {
                        const response = await api.get('/api/customer');
                        let rowData = response.data || response || [];

                        // Apply UI Filters
                        const distFilter = $('#filterDistrict').val();
                        const cityFilter = $('#filterCity').val();

                        if (distFilter) rowData = rowData.filter(d => d.district === distFilter);
                        if (cityFilter) rowData = rowData.filter(d => d.city === cityFilter);

                        callback({ data: rowData });
                    } catch (e) {
                        console.error("Grid Load Error", e);
                        callback({ data: [] });
                    }
                })();

                // 2. TIER-1 FIX: Return a dummy abort function. 
                // This prevents DataTables from crashing when .ajax.reload() is called!
                return { abort: function () { /* Do nothing */ } };
            },
            columns: [
                { data: 'name', className: 'fw-bold text-dark' },
                { data: 'taxRegNo', className: 'font-monospace text-muted' },
                { data: 'contactPerson' },
                { data: 'phone' },
                { data: 'district' },
                { data: 'city' },
                { data: 'creditLimit', className: 'text-end fw-bold', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'isActive', className: 'text-center', render: d => d ? '<span class="badge bg-success bg-opacity-10 text-success border">Active</span>' : '<span class="badge bg-danger bg-opacity-10 text-danger border">Inactive</span>' },
                {
                    data: null, className: 'text-end pe-3', orderable: false,
                    render: function (data, type, row) {
                        const safeRow = JSON.stringify(row).replace(/"/g, '&quot;');
                        return `<button type="button" class="btn btn-sm btn-light border shadow-sm px-2" onclick="customerApp.editCustomer(${safeRow})"><i class="fa-solid fa-pen-to-square text-primary"></i> Edit</button>`;
                    }
                }
            ],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    _loadLookups: async function () {
        try {
            // RESTORED: Added api.get('/api/employee') back to the Promise.all
            const [cgRes, plRes, ptRes, accRes, locRes, bankRes, empRes] = await Promise.all([
                api.get('/api/config/lookups?type=CustomerGroup'),
                api.get('/api/config/lookups?type=PriceLevel'),
                api.get('/api/config/lookups?type=PaymentTerm'),
                api.get('/api/finance/accounts'),
                api.get('/api/locations/provinces'),
                api.get('/api/banks'),
                api.get('/api/employee')
            ]);

            // 1. Strict Array Extraction (Prevents .forEach errors)
            this._locationData = Array.isArray(locRes) ? locRes : (locRes?.data || []);
            if (!Array.isArray(this._locationData)) this._locationData = [];

            this._bankData = Array.isArray(bankRes) ? bankRes : (bankRes?.data || []);
            if (!Array.isArray(this._bankData)) this._bankData = [];

            const customerGroups = Array.isArray(cgRes) ? cgRes : (cgRes?.data || []);
            const priceLevels = Array.isArray(plRes) ? plRes : (plRes?.data || []);
            const paymentTerms = Array.isArray(ptRes) ? ptRes : (ptRes?.data || []);
            const accounts = Array.isArray(accRes) ? accRes : (accRes?.data || []);

            // RESTORED: Extract Employees safely
            const employees = Array.isArray(empRes) ? empRes : (empRes?.data || []);

            // 2. Populate Standard Lookups
            this._populateDropdown('#CustomerGroupId', customerGroups);
            this._populateDropdown('#PriceLevelId', priceLevels);
            this._populateDropdown('#PaymentTermId', paymentTerms);

            // 3. RESTORED: Populate Sales Reps
            if (employees.length > 0) {
                const salesReps = employees
                    .filter(e => e.isSalesRep === true)
                    .map(e => ({
                        id: e.id,
                        fullName: `${e.firstName} ${e.lastName}`,
                        code: e.employeeCode
                    }));
                this._populateDropdown('#SalesRepId', salesReps, 'id', 'fullName', 'code');
            }

            // 4. Populate Provinces & Grid Filters
            let $prov = $('#Province').empty().append('<option value="">-- Select Province --</option>');
            let $fDist = $('#filterDistrict');

            this._locationData.forEach(p => {
                $prov.append(`<option value="${p.name}">${p.name}</option>`);
                p.districts.forEach(d => {
                    $fDist.append(`<option value="${d.name}">${d.name}</option>`);
                    d.cities.forEach(c => $('#filterCity').append(`<option value="${c.name}">${c.name}</option>`));
                });
            });

            // 5. Populate Banks
            let $bank = $('#BankId');
            this._bankData.forEach(b => $bank.append(`<option value="${b.id}">[${b.bankCode}] ${b.name}</option>`));

            // 6. Populate Locked Financial Accounts
            if (accounts.length > 0) {
                this._populateDropdown('#DefaultReceivableAccountId', accounts.filter(a => a.type === 'Asset'), 'id', 'name', 'code');
                this._populateDropdown('#DefaultRevenueAccountId', accounts.filter(a => a.type === 'Revenue'), 'id', 'name', 'code');
            }

        } catch (e) {
            console.error("Lookup Load Error:", e);
        }
    },

    _populateDropdown: function (selector, data, valProp = 'id', textProp = 'value', codeProp = null) {
        var $el = $(selector);
        $el.find('option:not(:first)').remove();
        data.forEach(item => {
            var text = codeProp ? `[${item[codeProp]}] ${item[textProp]}` : item[textProp];
            $el.append($('<option></option>').val(item[valProp]).text(text));
        });
    },

    openCreateModal: function () {
        $('#customerForm')[0].reset();
        $('#CustomerId').val(0);
        $('#customerModalLabel').html('<i class="fa-solid fa-building text-primary me-2"></i>New Customer Registration');

        // Reset cascading state
        $('#District, #City, #BankBranchId').empty().prop('disabled', true);

        // RELOCK ACCOUNTS & SET TIER-1 DEFAULTS
        $('#unlockAccountsToggle').prop('checked', false).trigger('change');

        // Auto-select standard GL Accounts based on your exact COA codes
        const arAcc = $('#DefaultReceivableAccountId option:contains("[1120]")').val();
        const revAcc = $('#DefaultRevenueAccountId option:contains("[4110]")').val();
        if (arAcc) $('#DefaultReceivableAccountId').val(arAcc);
        if (revAcc) $('#DefaultRevenueAccountId').val(revAcc);

        this._modal.show();
    },

    editCustomer: function (data) {
        $('#customerForm')[0].reset();
        $('#CustomerId').val(data.id);
        $('#customerModalLabel').html('<i class="fa-solid fa-building text-primary me-2"></i>Edit Customer');

        // Bind Base Fields
        $('input[name="Name"]').val(data.name);
        $('input[name="TaxRegNo"]').val(data.taxRegNo);
        $('input[name="ContactPerson"]').val(data.contactPerson);
        $('input[name="Email"]').val(data.email);
        $('input[name="Phone"]').val(data.phone);
        $('input[name="AddressLine1"]').val(data.addressLine1);
        $('input[name="CreditLimit"]').val(data.creditLimit);
        $('input[name="CreditPeriodDays"]').val(data.creditPeriodDays);
        $('input[name="BankAccountNumber"]').val(data.bankAccountNumber);

        // Bind Selects
        $('#CustomerGroupId').val(data.customerGroupId);
        $('#PriceLevelId').val(data.priceLevelId);
        $('#PaymentTermId').val(data.paymentTermId);

        // Bind Hierarchy: Locations
        $('#Province').val(data.province).trigger('change');
        setTimeout(() => {
            $('#District').val(data.district).trigger('change');
            setTimeout(() => { $('#City').val(data.city); }, 50);
        }, 50);

        // Bind Hierarchy: Banks
        if (data.bankId) {
            $('#BankId').val(data.bankId).trigger('change');
            setTimeout(() => { $('#BankBranchId').val(data.bankBranchId); }, 50);
        }

        // Bind Financial Accounts (Locked by default on edit too!)
        $('#unlockAccountsToggle').prop('checked', false).trigger('change');
        $('#DefaultReceivableAccountId').val(data.defaultReceivableAccountId);
        $('#DefaultRevenueAccountId').val(data.defaultRevenueAccountId);

        this._modal.show();
    },

    saveCustomer: async function (e) {
        var form = document.getElementById('customerForm');
        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        var $btn = $(e.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-1"></i> Saving...');

        var formData = new FormData(form);
        var payload = Object.fromEntries(formData.entries());

        // Parse Integers/Decimals properly
        payload.Id = parseInt(payload.Id) || 0;
        payload.CustomerGroupId = parseInt(payload.CustomerGroupId) || 0;
        payload.PriceLevelId = parseInt(payload.PriceLevelId) || 0;
        payload.PaymentTermId = parseInt(payload.PaymentTermId) || 0;
        payload.DefaultReceivableAccountId = parseInt(payload.DefaultReceivableAccountId) || 0;
        payload.DefaultRevenueAccountId = parseInt(payload.DefaultRevenueAccountId) || 0;
        payload.CreditLimit = parseFloat(payload.CreditLimit) || 0;
        payload.CreditPeriodDays = parseInt(payload.CreditPeriodDays) || 0;
        payload.BankId = payload.BankId ? parseInt(payload.BankId) : null;
        payload.BankBranchId = payload.BankBranchId ? parseInt(payload.BankBranchId) : null;

        // TIER-1 FIX: Let site.js handle the HTTP errors. No need for .responseJSON.
        const response = await api.post('/api/customer/save', payload);

        // Because site.js safely catches errors and returns {succeeded: false}, 
        // we handle business logic linearly without deep try/catch blocks!
        if (response && response.succeeded) {
            toastr.success(response.message || "Customer saved successfully.");
            this._modal.hide();
            this._table.ajax.reload(null, false);
        } else {
            // site.js's getErrorMsg already extracted the error into messages[0]!
            toastr.error(response?.messages?.[0] || "Failed to save customer. Please verify fields.");
        }

        $btn.prop('disabled', false).html(ogText);
    }
};

$(document).ready(() => customerApp.init());