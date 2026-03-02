window.customerApp = {
    _table: null,
    _modal: null,

    init: function () {
        console.log("[CustomerApp] 1. Initialization started...");
        try {
            var modalEl = document.getElementById('customerModal');
            if (modalEl) {
                this._modal = new bootstrap.Modal(modalEl, {
                    backdrop: 'static',
                    keyboard: false
                });
                console.log("[CustomerApp] 2. Modal bound successfully.");
            }

            this._initGrid();
            this._loadLookups();
        } catch (e) {
            console.error("[CustomerApp] FATAL Init Error:", e);
        }
    },

    _initGrid: function () {
        console.log("[CustomerApp] 3. Building DataTables...");
        this._table = $('#customerTable').DataTable({
            ajax: {
                url: '/api/customer',
                dataSrc: function (json) {
                    return json.data || json || [];
                },
                headers: { "Authorization": "Bearer " + localStorage.getItem("jwtToken") }
            },
            columns: [
                { data: 'name', className: 'fw-bold text-dark' },
                { data: 'taxRegNo', className: 'font-monospace text-muted' },
                { data: 'contactPerson' },
                { data: 'phone' },
                { data: 'city' },
                {
                    data: 'creditLimit',
                    className: 'text-end',
                    render: function (data) { return parseFloat(data || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }); }
                },
                {
                    data: 'isActive',
                    className: 'text-center',
                    render: function (data) {
                        return data
                            ? '<span class="badge bg-success bg-opacity-10 text-success border border-success">Active</span>'
                            : '<span class="badge bg-danger bg-opacity-10 text-danger border border-danger">Inactive</span>';
                    }
                },
                {
                    data: 'id',
                    className: 'text-end pe-3',
                    orderable: false,
                    render: function (data) {
                        return `
                            <div class="btn-group shadow-sm">
                                <button type="button" class="btn btn-sm btn-light border px-2" title="Edit" onclick="toastr.info('Edit mode pending Phase 2.')"><i class="fa-solid fa-pen-to-square text-secondary"></i></button>
                            </div>`;
                    }
                }
            ],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 20
        });
        console.log("[CustomerApp] 4. DataTables built.");
    },

    _loadLookups: async function () {
        console.log("[CustomerApp] 5. Fetching Lookups (Parallel)...");
        try {
            // Execute all API calls concurrently for maximum performance
            const [cgRes, plRes, ptRes, accRes] = await Promise.all([
                api.get('/api/config/lookups?type=CustomerGroup'),
                api.get('/api/config/lookups?type=PriceLevel'),
                api.get('/api/config/lookups?type=PaymentTerm'),
                api.get('/api/finance/accounts')
            ]);

            // Safely parse Result<T> wrappers
            const customerGroups = Array.isArray(cgRes) ? cgRes : (cgRes?.data || []);
            const priceLevels = Array.isArray(plRes) ? plRes : (plRes?.data || []);
            const paymentTerms = Array.isArray(ptRes) ? ptRes : (ptRes?.data || []);
            const accounts = Array.isArray(accRes) ? accRes : (accRes?.data || []);

            // 1. Populate System Lookups
            if (customerGroups.length > 0) this._populateDropdown('#CustomerGroupId', customerGroups);
            if (priceLevels.length > 0) this._populateDropdown('#PriceLevelId', priceLevels);
            if (paymentTerms.length > 0) this._populateDropdown('#PaymentTermId', paymentTerms);

            // 2. Populate Financial Accounts
            if (accounts.length > 0) {
                var arAccounts = accounts.filter(a => a.type === 'Asset' || a.type === '1');
                var revAccounts = accounts.filter(a => a.type === 'Revenue' || a.type === '4');

                this._populateDropdown('#DefaultReceivableAccountId', arAccounts, 'id', 'name', 'code');
                this._populateDropdown('#DefaultRevenueAccountId', revAccounts, 'id', 'name', 'code');
            }

            console.log("[CustomerApp] 6. Lookups loaded.");
        } catch (e) {
            console.error("[CustomerApp] Lookup Load Error:", e);
        }
    },

    _populateDropdown: function (selector, data, valProp = 'id', textProp = 'value', codeProp = null) {
        var $el = $(selector);
        $el.find('option:not(:first)').remove();

        if ($el.find('option').length === 0) {
            $el.append('<option value="">-- Select --</option>');
        }

        data.forEach(item => {
            var text = codeProp ? `[${item[codeProp]}] ${item[textProp]}` : item[textProp];
            $el.append($('<option></option>').val(item[valProp]).text(text));
        });
    },

    openCreateModal: function () {
        var form = document.getElementById('customerForm');
        if (form) form.reset();

        if (this._modal) {
            this._modal.show();
        } else {
            var modalEl = document.getElementById('customerModal');
            if (modalEl) {
                this._modal = new bootstrap.Modal(modalEl);
                this._modal.show();
            } else {
                console.error("[CustomerApp] Modal DOM element is missing!");
            }
        }
    },

    saveCustomer: async function () {
        var form = document.getElementById('customerForm');
        if (!form || !form.checkValidity()) {
            if (form) form.reportValidity();
            return;
        }

        var formData = new FormData(form);
        var payload = Object.fromEntries(formData.entries());

        payload.CustomerGroupId = parseInt(payload.CustomerGroupId) || 0;
        payload.PriceLevelId = parseInt(payload.PriceLevelId) || 0;
        payload.PaymentTermId = parseInt(payload.PaymentTermId) || 0;
        payload.DefaultReceivableAccountId = parseInt(payload.DefaultReceivableAccountId) || 0;
        payload.DefaultRevenueAccountId = parseInt(payload.DefaultRevenueAccountId) || 0;
        payload.CreditLimit = parseFloat(payload.CreditLimit) || 0;
        payload.CreditPeriodDays = parseInt(payload.CreditPeriodDays) || 0;
        payload.SalesRepId = payload.SalesRepId ? parseInt(payload.SalesRepId) : null;

        const response = await api.post('/api/customer', payload);

        if (response && (response.succeeded || response.id)) {
            if (this._modal) this._modal.hide();
            if (this._table) this._table.ajax.reload(null, false);
        }
    }
};

// Bind to document ready
$(document).ready(function () {
    window.customerApp.init();
});