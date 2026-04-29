window.iamApp = {
    _usersTable: null,
    _rolesTable: null,
    _userModal: null,
    _roleModal: null,
    _permissionsModal: null,

    init: function () {
        var uModalEl = document.getElementById('userModal');
        if (uModalEl) this._userModal = new bootstrap.Modal(uModalEl, { backdrop: 'static' });

        var rModalEl = document.getElementById('roleModal');
        if (rModalEl) this._roleModal = new bootstrap.Modal(rModalEl, { backdrop: 'static' });

        this._initUsersGrid();
        this._initRolesGrid();
        this._loadRoles();

        // Style the active tab logic
        $('button[data-bs-toggle="tab"]').on('shown.bs.tab', function (e) {
            $('.nav-link').removeClass('text-dark').addClass('text-secondary');
            $(e.target).removeClass('text-secondary').addClass('text-dark');
        });
    },

    // ==========================================
    // GRIDS
    // ==========================================
    _initUsersGrid: function () {
        this._usersTable = $('#usersGrid').DataTable({
            ajax: async function (data, callback) {
                try {
                    const res = await api.get('/api/iam/users');
                    callback({ data: res.data || res || [] });
                } catch (e) { callback({ data: [] }); }
            },
            columns: [
                { 
                    data: 'fullName', className: 'fw-bold text-dark ps-3',
                    render: function(data) {
                        return `<div class="d-flex align-items-center">
                                    <div class="bg-primary text-white rounded-circle d-flex justify-content-center align-items-center me-2" style="width: 30px; height: 30px; font-size: 12px;">${data.charAt(0)}</div>
                                    ${data}
                                </div>`;
                    }
                },
                { data: 'email', className: 'text-muted' },
                { 
                    data: 'role', 
                    render: function(d) {
                        if(!d) return `<span class="text-muted fst-italic">None</span>`;
                        let color = d === 'SuperAdmin' ? "danger" : "secondary";
                        return `<span class="badge bg-${color}">${d}</span>`;
                    }
                },
                { 
                    data: 'isActive', className: 'text-center',
                    render: d => d ? '<span class="badge bg-success bg-opacity-10 text-success border border-success"><i class="fa-solid fa-check-circle me-1"></i> Active</span>' 
                                   : '<span class="badge bg-danger bg-opacity-10 text-danger border border-danger"><i class="fa-solid fa-ban me-1"></i> Locked Out</span>' 
                },
                {
                    data: null, className: 'text-end pe-3', orderable: false,
                    render: function (data, type, row) {
                        // Pass stringified JSON so we don't have to fetch the user again to edit!
                        const userJson = encodeURIComponent(JSON.stringify(row));
                        let icon = row.isActive ? 'fa-lock' : 'fa-unlock';
                        let lockColor = row.isActive ? 'btn-outline-danger' : 'btn-outline-success';
                        let lockTitle = row.isActive ? 'Deactivate (Lock Out)' : 'Activate (Unlock)';
                        
                        return `
                            <button class="btn btn-sm btn-outline-dark shadow-sm me-1" onclick="iamApp.editUser('${userJson}')" title="Edit User"><i class="fa-solid fa-pen"></i></button>
                            <button class="btn btn-sm ${lockColor} shadow-sm" onclick="iamApp.toggleStatus('${row.id}')" title="${lockTitle}"><i class="fa-solid ${icon}"></i></button>
                        `;
                    }
                }
            ],
            order: [[0, 'asc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    _initRolesGrid: function () {
        this._rolesTable = $('#rolesGrid').DataTable({
            ajax: async function (data, callback) {
                try {
                    const res = await api.get('/api/iam/roles');
                    callback({ data: res.data || res || [] });
                } catch (e) { callback({ data: [] }); }
            },
            columns: [
                { data: 'name', className: 'fw-bold text-dark ps-3 font-monospace' },
                {
                    data: null, className: 'text-end pe-3', orderable: false,
                    render: function (data, type, row) {
                        let btns = `<button class="btn btn-sm btn-outline-dark shadow-sm me-1" onclick="iamApp.editRole('${row.id}', '${row.name}')" title="Edit Role"><i class="fa-solid fa-pen"></i> Edit</button>`;

                        // TIER-1 FEATURE: Manage Permissions Button
                        if (row.name !== 'SuperAdmin') { // SuperAdmin usually bypasses everything, no need to edit
                            btns += `<button class="btn btn-sm btn-warning shadow-sm fw-bold text-dark" onclick="iamApp.openPermissionsModal('${row.id}', '${row.name}')" title="Manage Permissions"><i class="fa-solid fa-user-lock"></i> Permissions</button>`;
                        }
                        return btns;
                    }
                }
            ],
            order: [[0, 'asc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    _loadRoles: async function () {
        try {
            const res = await api.get('/api/iam/roles');
            const roles = res.data || res || [];
            let $role = $('#Role').empty().append('<option value="">-- Assign Role --</option>');
            roles.forEach(r => $role.append(`<option value="${r.name}">${r.name}</option>`));
        } catch (e) { console.error("Failed to load roles."); }
    },


    // ==========================================
    // USER ACTIONS
    // ==========================================
    openUserModal: function () {
        $('#userForm')[0].reset();
        $('#userForm').removeClass('was-validated');
        $('#UserId').val('');
        $('#Email').prop('readonly', false);
        $('#Password').prop('required', true);
        $('#divPassword').show();
        $('#userModalTitle').html('<i class="fa-solid fa-user-shield me-2"></i>Provision New User');
        this._userModal.show();
    },

    editUser: function(userJsonEncoded) {
        const user = JSON.parse(decodeURIComponent(userJsonEncoded));
        $('#userForm')[0].reset();
        $('#userForm').removeClass('was-validated');
        
        $('#UserId').val(user.id);
        
        // UPDATED: Bind to FullName
        $('#FullName').val(user.fullName);
        
        $('#Email').val(user.email).prop('readonly', true); // Cannot change email
        $('#Role').val(user.role);
        
        // Hide password field for edits
        $('#Password').prop('required', false);
        $('#divPassword').hide();
        
        $('#userModalTitle').html('<i class="fa-solid fa-user-pen me-2"></i>Edit User');
        this._userModal.show();
    },

    saveUser: async function(e) {
        var form = $('#userForm')[0];
        if (!form.checkValidity()) { $(form).addClass('was-validated'); return; }

        const id = $('#UserId').val();
        const isEdit = id !== '';

        // UPDATED: Pull from FullName
        const payload = {
            FullName: $('#FullName').val(),
            Role: $('#Role').val()
        };

        // If creating a new user, grab the Email and Password
        if (!isEdit) {
            payload.Email = $('#Email').val();
            payload.Password = $('#Password').val();
        }

        var $btn = $(e.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Saving...');

        try {
            const url = isEdit ? `/api/iam/users/${id}` : '/api/iam/users';
            const req = isEdit ? api.put(url, payload) : api.post(url, payload);
            
            const res = await req;
            if (res && res.succeeded) {
                toastr.success(res.message || "User saved successfully.");
                this._userModal.hide();
                this._usersTable.ajax.reload(null, false);
            } else if (res && res.messages) { toastr.error(res.messages[0]); }
        } catch (err) { toastr.error(err.responseJSON?.messages?.[0] || "Operation failed."); } 
        finally { $btn.prop('disabled', false).html(ogText); }
    },

    toggleStatus: async function(id) {
        const result = await Swal.fire({
            title: 'Change Access?', text: "Instantly lock or unlock this user.", icon: 'warning',
            showCancelButton: true, confirmButtonColor: '#0d6efd', confirmButtonText: 'Yes, proceed'
        });

        if (!result.isConfirmed) return;

        try {
            const res = await api.post(`/api/iam/users/${id}/toggle-status`, {});
            if (res && res.succeeded) {
                toastr.success(res.message);
                this._usersTable.ajax.reload(null, false);
            } else { toastr.error(res.messages[0]); }
        } catch (e) { toastr.error(e.responseJSON?.messages?.[0] || "Action failed."); }
    },

    // ==========================================
    // ROLE ACTIONS
    // ==========================================
    openRoleModal: function () {
        $('#roleForm')[0].reset();
        $('#roleForm').removeClass('was-validated');
        $('#RoleId').val('');
        $('#roleModalTitle').html('<i class="fa-solid fa-shield-halved me-2"></i>Create Security Role');
        this._roleModal.show();
    },

    editRole: function(id, name) {
        $('#roleForm')[0].reset();
        $('#roleForm').removeClass('was-validated');
        $('#RoleId').val(id);
        $('#RoleName').val(name);
        $('#roleModalTitle').html('<i class="fa-solid fa-pen me-2"></i>Edit Security Role');
        this._roleModal.show();
    },

    saveRole: async function(e) {
        var form = $('#roleForm')[0];
        if (!form.checkValidity()) { $(form).addClass('was-validated'); return; }

        const id = $('#RoleId').val();
        const isEdit = id !== '';
        const payload = { RoleName: $('#RoleName').val() };

        var $btn = $(e.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Saving...');

        try {
            const url = isEdit ? `/api/iam/roles/${id}` : '/api/iam/roles';
            const req = isEdit ? api.put(url, payload) : api.post(url, payload);

            const res = await req;
            if (res && res.succeeded) {
                toastr.success(res.message || "Role saved successfully.");
                this._roleModal.hide();
                this._rolesTable.ajax.reload(null, false);
                this._loadRoles(); // Refresh User dropdown!
            } else if (res && res.messages) { toastr.error(res.messages[0]); }
        } catch (err) { toastr.error(err.responseJSON?.messages?.[0] || "Operation failed."); } 
        finally { $btn.prop('disabled', false).html(ogText); }
    },

    

    openPermissionsModal: async function (roleId, roleName) {
        if (!this._permissionsModal) {
            this._permissionsModal = new bootstrap.Modal(document.getElementById('permissionsModal'), { backdrop: 'static' });
        }

        $('#PermRoleId').val(roleId);
        $('#lblPermRoleName').text(roleName);
        $('#permissionsContainer').html('<div class="text-center py-4 w-100"><i class="spinner-border text-primary"></i> Loading...</div>');
        this._permissionsModal.show();

        try {
            // 1. Fetch ALL possible permissions in the system
            const allPermsRes = await api.get('/api/iam/permissions');
            const allPerms = allPermsRes.data || allPermsRes;

            // 2. Fetch assigned permissions for THIS role
            const rolePermsRes = await api.get(`/api/iam/roles/${roleId}/permissions`);
            const assignedPerms = rolePermsRes.assignedPermissions || [];

            // 3. Group all permissions by Module (Sales, Purchasing, etc.)
            const grouped = allPerms.reduce((acc, curr) => {
                if (!acc[curr.module]) acc[curr.module] = [];
                acc[curr.module].push(curr.permission);
                return acc;
            }, {});

            // 4. Render the UI
            let html = '';
            for (const [module, perms] of Object.entries(grouped)) {
                let moduleIcon = 'fa-cube'; // default
                if (module === 'Sales') moduleIcon = 'fa-cart-shopping';
                if (module === 'Purchasing') moduleIcon = 'fa-truck-fast';
                if (module === 'Inventory') moduleIcon = 'fa-boxes-stacked';
                if (module === 'Treasury') moduleIcon = 'fa-vault';
                if (module === 'Reporting') moduleIcon = 'fa-chart-bar';

                html += `
                    <div class="col-md-6">
                        <div class="card border shadow-sm h-100">
                            <div class="card-header bg-white fw-bold text-primary py-2">
                                <i class="fa-solid ${moduleIcon} me-2"></i>${module}
                            </div>
                            <div class="card-body p-3 fs-13">
                `;

                perms.forEach(p => {
                    const isChecked = assignedPerms.includes(p) ? 'checked' : '';
                    // Extract just the action name (e.g. "Permissions.Sales.CreateInvoice" -> "CreateInvoice")
                    const shortName = p.split('.').pop().replace(/([A-Z])/g, ' $1').trim();

                    html += `
                        <div class="form-check form-switch mb-2">
                            <input class="form-check-input perm-checkbox" type="checkbox" id="${p}" value="${p}" ${isChecked} style="cursor:pointer;">
                            <label class="form-check-label fw-semibold text-dark" for="${p}" style="cursor:pointer;">${shortName}</label>
                        </div>
                    `;
                });

                html += `</div></div></div>`;
            }

            $('#permissionsContainer').html(html);

        } catch (e) {
            $('#permissionsContainer').html('<div class="text-danger fw-bold text-center w-100 py-4">Failed to load permissions.</div>');
            console.error(e);
        }
    },

    selectAllPerms: function () {
        $('.perm-checkbox').prop('checked', true);
    },

    savePermissions: async function (e) {
        const roleId = $('#PermRoleId').val();

        // Harvest all checked boxes
        let selectedPerms = [];
        $('.perm-checkbox:checked').each(function () {
            selectedPerms.push($(this).val());
        });

        var $btn = $(e.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Saving...');

        try {
            const res = await api.post(`/api/iam/roles/${roleId}/permissions`, selectedPerms);
            if (res && res.succeeded) {
                toastr.success(res.message || "Permissions updated successfully.");
                this._permissionsModal.hide();
            } else {
                toastr.error("Failed to update permissions.");
            }
        } catch (err) {
            toastr.error("Network error saving permissions.");
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    }
};

$(document).ready(() => iamApp.init());