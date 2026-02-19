var supplierApp = (function () {
    "use strict";
    var table, drawer;

    var init = function () {
        _initGrid();
        _loadDropdowns();

        var el = document.getElementById('supplierDrawer');
        if (el) drawer = new bootstrap.Offcanvas(el);

        $('#frmSupplier').on('submit', function (e) {
            e.preventDefault();
            _save();
        });
    };

    var _initGrid = function () {
        table = $('#supplierGrid').DataTable({
            ajax: { url: '/api/Supplier', type: 'GET', dataSrc: 'data' },
            columns: [
                { data: "name", className: "fw-bold text-primary" },
                {
                    data: null,
                    render: function (d) {
                        return `<div><i class="bi bi-person me-1"></i>${d.contactPerson || '-'}</div>
                                <div class="small text-muted">${d.email}</div>`;
                    }
                },
                { data: "supplierGroupName", defaultContent: "-" }, // Ensure Query returns this
                { data: "taxRegNo", className: "font-monospace small" },
                {
                    data: "isActive", className: "text-center",
                    render: function (d) {
                        return d ? '<span class="badge bg-success-subtle text-success">Active</span>'
                            : '<span class="badge bg-danger-subtle text-danger">Inactive</span>';
                    }
                },
                {
                    data: null, className: "text-end",
                    render: function (data, type, row) {
                        const safeRow = JSON.stringify(row).replace(/"/g, '&quot;');
                        return `<button class="btn btn-sm btn-outline-secondary" onclick="supplierApp.edit(${row.id})">Edit</button>`;
                    }
                }
            ]
        });
    };

    var _loadDropdowns = async function () {
        // Load Lookup: Supplier Groups & Payment Terms
        const [groups, terms, accounts] = await Promise.all([
            api.get('/api/Config/lookups?type=SupplierGroup'),
            api.get('/api/Config/lookups?type=PaymentTerm'),
            api.get('/api/Finance/accounts') // Assuming this exists
        ]);

        // Helper to populate select
        const fill = (id, data) => {
            let opts = '<option value="">Select...</option>';
            if (data && data.data) data.data.forEach(x => opts += `<option value="${x.id}">${x.value || x.name}</option>`);
            $(`#${id}`).html(opts);
        };

        fill('SupplierGroupId', groups);
        fill('PaymentTermId', terms);
        // Filter accounts for AP
        // fill('DefaultPayableAccountId', accounts... filter by Liability);
    };

    var openEditor = function () {
        $('#drawerTitle').text("New Vendor");
        $('#Id').val(0);
        $('#frmSupplier')[0].reset();

        // Reset Tabs
        var firstTab = new bootstrap.Tab(document.querySelector('#supplierTabs button[data-bs-target="#tabGeneral"]'));
        firstTab.show();

        drawer.show();
    };

    var edit = async function (id) {
        // Fetch full details (Grid might have partial data)
        const res = await api.get(`/api/Supplier/${id}`);
        if (!res || !res.succeeded) { toastr.error("Could not load supplier"); return; }

        const d = res.data;

        $('#drawerTitle').text("Edit Vendor");
        $('#Id').val(d.id);

        // Map Fields (Auto-populate by ID match)
        // Note: IDs in HTML match DTO properties exactly
        Object.keys(d).forEach(key => {
            const el = document.getElementById(key.charAt(0).toUpperCase() + key.slice(1));
            if (el) {
                if (el.type === 'checkbox') el.checked = d[key];
                else el.value = d[key] || '';
            }
        });

        drawer.show();
    };

    var _save = async function () {
        var id = parseInt($('#Id').val());

        // Auto-Harvest Data
        // Loops through DTO keys we care about
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
            CreditLimit: parseFloat($('#CreditLimit').val()) || 0,

            BankName: $('#BankName').val(),
            BankBranch: $('#BankBranch').val(),
            BankAccountNumber: $('#BankAccountNumber').val(),
            BankSwiftCode: $('#BankSwiftCode').val(),
            BankIBAN: $('#BankIBAN').val(),

            IsActive: $('#IsActive').is(':checked')
        };

        var payload = { Supplier: dto };
        var res;

        if (id === 0) res = await api.post('/api/Supplier', payload);
        else res = await api.put(`/api/Supplier/${id}`, payload);

        if (res && res.succeeded) {
            toastr.success("Vendor saved successfully.");
            drawer.hide();
            table.ajax.reload();
        }
    };

    return { init, openEditor, edit };
})();

$(document).ready(function () { supplierApp.init(); });