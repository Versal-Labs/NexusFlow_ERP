window.warehouseApp = {
    _table: null,
    _modal: null,

    init: function () {
        try {
            var modalEl = document.getElementById('warehouseModal');
            if (modalEl) this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });

            this._initGrid();
            this._loadMasterData();
            this._registerEvents();
        } catch (e) {
            console.error("[WarehouseApp] Init Error:", e);
        }
    },

    _registerEvents: function () {
        // UI Logic: Show/Hide Vendor dropdown based on Warehouse Type
        $('#Type').on('change', function () {
            if ($(this).val() == "1") { // 1 = Subcontractor
                $('#divLinkedSupplier').slideDown();
                $('#LinkedSupplierId').prop('required', true);
            } else {
                $('#divLinkedSupplier').slideUp();
                $('#LinkedSupplierId').val('').trigger('change').prop('required', false);
            }
        });
    },

    _initGrid: function () {
        this._table = $('#warehouseGrid').DataTable({
            ajax: {
                url: '/api/masterdata/warehouses',
                dataSrc: function (json) { return json.data || json || []; },
                headers: { "Authorization": "Bearer " + localStorage.getItem("jwtToken") }
            },
            columns: [
                { data: 'code', className: 'fw-bold text-dark font-monospace' },
                { data: 'name', className: 'fw-bold' },
                {
                    data: 'type',
                    render: function (d) {
                        if (d === 1) return '<span class="badge bg-warning text-dark"><i class="fa-solid fa-industry me-1"></i> Subcontractor</span>';
                        if (d === 2) return '<span class="badge bg-info text-dark"><i class="fa-solid fa-truck me-1"></i> Transit</span>';
                        return '<span class="badge bg-secondary"><i class="fa-solid fa-building me-1"></i> Internal</span>';
                    }
                },
                {
                    data: null,
                    render: d => `<div class="small">${d.location || '-'}</div><div class="small text-muted"><i class="fa-solid fa-user me-1"></i>${d.managerName || '-'}</div>`
                },
                { data: 'linkedSupplierName', defaultContent: '<span class="text-muted">-</span>' },
                {
                    data: 'isActive',
                    className: 'text-center',
                    render: d => d ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-danger">Inactive</span>'
                },
                {
                    data: 'id',
                    className: 'text-end pe-3',
                    orderable: false,
                    render: data => `
                        <button class="btn btn-sm btn-light border px-2 shadow-sm" onclick="warehouseApp.edit(${data})">
                            <i class="fa-solid fa-pen-to-square text-secondary"></i>
                        </button>`
                }
            ],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 20
        });
    },

    _loadMasterData: async function () {
        try {
            const [suppRes, accRes] = await Promise.all([
                api.get('/api/supplier'),
                api.get('/api/finance/accounts')
            ]);

            const suppliers = suppRes.data || suppRes;
            const accounts = accRes.data || accRes;

            let suppEl = $('#LinkedSupplierId');
            suppEl.empty().append('<option value="">-- Select Vendor --</option>');
            if (suppliers.length > 0) suppliers.forEach(s => suppEl.append(`<option value="${s.id}">${s.name}</option>`));
            suppEl.select2({ dropdownParent: $('#warehouseModal') });

            let accEl = $('#OverrideInventoryAccountId');
            accEl.empty().append('<option value="">-- System Default (Use Product Asset Account) --</option>');
            if (accounts.length > 0) {
                // Filter Inventory/Asset accounts
                accounts.filter(a => a.type === 'Asset' || a.type === '1').forEach(a => {
                    accEl.append(`<option value="${a.id}">[${a.code}] ${a.name}</option>`);
                });
            }
            accEl.select2({ dropdownParent: $('#warehouseModal') });

        } catch (e) {
            console.error("Lookup Error:", e);
        }
    },

    openModal: function () {
        document.getElementById('warehouseForm').reset();
        $('#Id').val(0);
        $('#modalTitle').html('<i class="fa-solid fa-warehouse text-primary me-2"></i>New Warehouse');
        $('#LinkedSupplierId').val('').trigger('change');
        $('#OverrideInventoryAccountId').val('').trigger('change');
        $('#Type').trigger('change'); // Trigger UI logic
        if (this._modal) this._modal.show();
    },

    edit: async function (id) {
        const res = await api.get(`/api/masterdata/warehouses/${id}`);
        if (res && res.succeeded) {
            const data = res.data;
            $('#Id').val(data.id);
            $('#Code').val(data.code);
            $('#Name').val(data.name);
            $('#Location').val(data.location);
            $('#ManagerName').val(data.managerName);
            $('#IsActive').prop('checked', data.isActive);

            $('#Type').val(data.type).trigger('change'); // Trigger UI logic

            if (data.linkedSupplierId) $('#LinkedSupplierId').val(data.linkedSupplierId).trigger('change');
            if (data.overrideInventoryAccountId) $('#OverrideInventoryAccountId').val(data.overrideInventoryAccountId).trigger('change');

            $('#modalTitle').html('<i class="fa-solid fa-warehouse text-primary me-2"></i>Edit Warehouse');
            if (this._modal) this._modal.show();
        }
    },

    save: async function () {
        var form = document.getElementById('warehouseForm');
        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        const id = parseInt($('#Id').val()) || 0;
        const payload = {
            Warehouse: {
                Id: id,
                Code: $('#Code').val().toUpperCase(),
                Name: $('#Name').val(),
                Type: parseInt($('#Type').val()),
                Location: $('#Location').val(),
                ManagerName: $('#ManagerName').val(),
                LinkedSupplierId: $('#Type').val() == "1" ? (parseInt($('#LinkedSupplierId').val()) || null) : null,
                OverrideInventoryAccountId: parseInt($('#OverrideInventoryAccountId').val()) || null,
                IsActive: $('#IsActive').is(':checked')
            }
        };

        let res;
        if (id === 0) res = await api.post('/api/masterdata/warehouses', payload);
        else res = await api.put(`/api/masterdata/warehouses/${id}`, payload);

        if (res && res.succeeded) {
            toastr.success(res.messages ? res.messages[0] : "Saved Successfully");
            this._modal.hide();
            this._table.ajax.reload(null, false);
        }
    }
};

$(document).ready(function () { window.warehouseApp.init(); });