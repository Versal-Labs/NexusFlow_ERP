window.supplierApp = {
    _table: null,
    _modal: null,

    init: function () {
        try {
            var modalEl = document.getElementById('supplierModal');
            if (modalEl) {
                this._modal = new bootstrap.Modal(modalEl, {
                    backdrop: 'static',
                    keyboard: false
                });
            }

            this._initGrid();
            this._loadDropdowns();
        } catch (e) {
            console.error("[SupplierApp] Initialization Error:", e);
        }
    },

    _initGrid: function () {
        this._table = $('#supplierGrid').DataTable({
            ajax: {
                url: '/api/Supplier',
                type: 'GET',
                dataSrc: function (json) {
                    return json.data || json || [];
                },
                headers: { "Authorization": "Bearer " + localStorage.getItem("jwtToken") }
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
                { data: 'supplierGroupName', defaultContent: '-' },
                {
                    data: 'isActive',
                    className: 'text-center',
                    render: function (d) {
                        return d
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
                                <button type="button" class="btn btn-sm btn-light border px-2" title="Edit" onclick="supplierApp.edit(${data})"><i class="fa-solid fa-pen-to-square text-secondary"></i></button>
                            </div>`;
                    }
                }
            ],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 20
        });
    },

    _loadDropdowns: async function () {
        try {
            const [groupRes, termRes, accRes] = await Promise.all([
                api.get('/api/config/lookups?type=SupplierGroup'),
                api.get('/api/config/lookups?type=PaymentTerm'),
                api.get('/api/finance/accounts')
            ]);

            const groups = Array.isArray(groupRes) ? groupRes : (groupRes?.data || []);
            const terms = Array.isArray(termRes) ? termRes : (termRes?.data || []);
            const accounts = Array.isArray(accRes) ? accRes : (accRes?.data || []);

            if (groups.length > 0) this._populateDropdown('SupplierGroupId', groups);
            if (terms.length > 0) this._populateDropdown('PaymentTermId', terms);

            if (accounts.length > 0) {
                // Filter liabilities for AP Account
                var apAccounts = accounts.filter(a => a.type === 'Liability' || a.type === '2');
                // Filter expenses for Default Expense Account
                var expAccounts = accounts.filter(a => a.type === 'Expense' || a.type === '5');

                this._populateDropdown('DefaultPayableAccountId', apAccounts, 'id', 'name', 'code');
                this._populateDropdown('DefaultExpenseAccountId', expAccounts, 'id', 'name', 'code');
            }
        } catch (e) {
            console.error("[SupplierApp] Dropdown Load Error:", e);
        }
    },

    _populateDropdown: function (id, data, valProp = 'id', textProp = 'value', codeProp = null) {
        var $el = $(`#${id}`);
        $el.empty().append('<option value="">-- Select --</option>');
        data.forEach(item => {
            var text = codeProp ? `[${item[codeProp]}] ${item[textProp]}` : item[textProp];
            $el.append($('<option></option>').val(item[valProp]).text(text));
        });
    },

    openEditor: function () {
        var form = document.getElementById('frmSupplier');
        if (form) form.reset();

        $('#drawerTitle').html('<i class="fa-solid fa-truck-field text-primary me-2"></i>New Vendor');
        $('#Id').val(0);

        if (this._modal) this._modal.show();
    },

    edit: async function (id) {
        const res = await api.get(`/api/Supplier/${id}`);
        if (!res || (!res.succeeded && !res.id)) {
            toastr.error("Could not load supplier");
            return;
        }

        // Handle both raw object and wrapped response
        const d = res.data || res;

        $('#drawerTitle').html('<i class="fa-solid fa-truck-field text-primary me-2"></i>Edit Vendor');
        $('#Id').val(d.id);

        // Map Fields
        Object.keys(d).forEach(key => {
            // Capitalize first letter to match HTML IDs
            const elId = key.charAt(0).toUpperCase() + key.slice(1);
            const el = document.getElementById(elId);
            if (el) {
                if (el.type === 'checkbox') {
                    el.checked = d[key];
                } else {
                    el.value = d[key] === null ? '' : d[key];
                }
            }
        });

        if (this._modal) this._modal.show();
    },

    saveSupplier: async function () {
        var form = document.getElementById('frmSupplier');
        if (!form || !form.checkValidity()) {
            if (form) form.reportValidity();
            return;
        }

        var id = parseInt($('#Id').val()) || 0;

        // Auto-Harvest Data
        var dto = {
            Id: id,
            Name: $('#Name').val(),
            TradeName: $('#TradeName').val(),
            TaxRegNo: $('#TaxRegNo').val(),
            SupplierGroupId: parseInt($('#SupplierGroupId').val()) || 0,
            BusinessRegNo: $('#BusinessRegNo').val(),

            ContactPerson: $('#ContactPerson').val(),
            Email: $('#Email').val(),
            Phone: $('#Phone').val(),
            Website: $('#Website').val(),

            AddressLine1: $('#AddressLine1').val(),
            AddressLine2: $('#AddressLine2').val(),
            City: $('#City').val(),
            State: $('#State').val(),
            ZipCode: $('#ZipCode').val(),
            Country: $('#Country').val(),

            CurrencyCode: $('#CurrencyCode').val(),
            PaymentTermId: parseInt($('#PaymentTermId').val()) || 0,
            DefaultPayableAccountId: parseInt($('#DefaultPayableAccountId').val()) || null,
            DefaultExpenseAccountId: parseInt($('#DefaultExpenseAccountId').val()) || null,
            CreditLimit: parseFloat($('#CreditLimit').val()) || 0,

            BankName: $('#BankName').val(),
            BankBranch: $('#BankBranch').val(),
            BankAccountNumber: $('#BankAccountNumber').val(),
            BankSwiftCode: $('#BankSwiftCode').val(),
            BankIBAN: $('#BankIBAN').val(),

            IsActive: $('#IsActive').is(':checked')
        };

        // Note: Assuming your API accepts { Supplier: dto } based on your original code
        var payload = { Supplier: dto };
        var res;

        if (id === 0) {
            res = await api.post('/api/Supplier', payload);
        } else {
            res = await api.put(`/api/Supplier/${id}`, payload);
        }

        if (res && (res.succeeded || res.id)) {
            if (this._modal) this._modal.hide();
            if (this._table) this._table.ajax.reload(null, false);
        }
    }
};

$(document).ready(function () {
    window.supplierApp.init();
});