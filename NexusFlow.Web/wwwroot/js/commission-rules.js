window.commissionApp = {
    _table: null,
    _modal: null,

    init: function() {
        var modalEl = document.getElementById('commissionModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl);

        this._initGrid();
        this._loadLookups();
    },

    _initGrid: function() {
        this._table = $('#rulesGrid').DataTable({
            ajax: {
                url: '/api/CommissionRule',
                dataSrc: function(json) { return json.data || json || []; }
            },
            columns: [
                { data: 'name', className: 'fw-bold text-dark ps-4' },
                { 
                    data: null,
                    render: function(row) {
                        return row.ruleType === 1 
                            ? `<span class="badge bg-secondary"><i class="fa-solid fa-globe me-1"></i>Global Scope</span>`
                            : `<span class="badge bg-info text-dark"><i class="fa-solid fa-tags me-1"></i>${row.categoryName}</span>`;
                    }
                },
                { data: 'employeeName', className: 'fst-italic' },
                { 
                    data: 'commissionPercentage', 
                    className: 'text-end fw-bold text-success',
                    render: d => d.toFixed(2) + '%'
                },
                {
                    data: null,
                    render: function(row) {
                        if (!row.validFrom && !row.validTo) return 'Permanent';
                        let f = row.validFrom ? new Date(row.validFrom).toLocaleDateString() : 'Forever';
                        let t = row.validTo ? new Date(row.validTo).toLocaleDateString() : 'Forever';
                        return `<small class="text-muted">${f} - ${t}</small>`;
                    }
                },
                {
                    data: 'isActive',
                    className: 'text-center',
                    render: d => d ? '<span class="text-success"><i class="fa-solid fa-circle-check"></i> Active</span>' : '<span class="text-danger"><i class="fa-solid fa-circle-xmark"></i> Disabled</span>'
                },
                {
                    data: null,
                    className: 'text-end pe-4',
                    orderable: false,
                    render: function(data, type, row) {
                        var safeRow = JSON.stringify(row).replace(/"/g, '&quot;');
                        return `<button class="btn btn-sm btn-light border" onclick="commissionApp.edit(${safeRow})"><i class="fa-solid fa-pencil text-secondary"></i></button>`;
                    }
                }
            ],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    _loadLookups: async function() {
        try {
            const [catRes, empRes] = await Promise.all([
            api.get('/api/category'),
            api.get('/api/employee')
        ]);

            const categories = Array.isArray(catRes) ? catRes : (catRes?.data || []);
            const employees = Array.isArray(empRes) ? empRes : (empRes?.data || []);

            let $cat = $('#CategoryId');
            categories.forEach(c => $cat.append($('<option></option>').val(c.id).text(`[${c.code}] ${c.name}`)));

            let $emp = $('#EmployeeId');
            employees.filter(e => e.isSalesRep).forEach(e => {
            $emp.append($('<option></option>').val(e.id).text(`[${e.employeeCode}] ${e.firstName} ${e.lastName}`));
        });
        } catch (e) {
            console.error("Lookup Error:", e);
        }
    },

    onRuleTypeChange: function() {
        let type = $('#RuleType').val();
        let $cat = $('#CategoryId');
        
        if (type == "1") { // Global
            $cat.val('').prop('disabled', true).removeAttr('required');
        } else { // Category
            $cat.prop('disabled', false).attr('required', 'required');
        }
    },

    openCreateModal: function() {
        $('#commissionForm')[0].reset();
        $('#commissionForm').removeClass('was-validated');
        $('#Id').val(0);
        $('#modalTitle').html('<i class="fa-solid fa-plus text-primary me-2"></i>New Commission Rule');
        this.onRuleTypeChange();
        this._modal.show();
    },

    edit: function(row) {
        $('#commissionForm').removeClass('was-validated');
        $('#modalTitle').html('<i class="fa-solid fa-pencil text-primary me-2"></i>Edit Commission Rule');
        
        $('#Id').val(row.id);
        $('#Name').val(row.name);
        $('#RuleType').val(row.ruleType);
        this.onRuleTypeChange(); // Trigger cascading logic

        $('#CategoryId').val(row.categoryId || '');
        $('#EmployeeId').val(row.employeeId || '');
        $('#CommissionPercentage').val(row.commissionPercentage);
        $('#IsActive').prop('checked', row.isActive);

        $('#ValidFrom').val(row.validFrom ? row.validFrom.split('T')[0] : '');
        $('#ValidTo').val(row.validTo ? row.validTo.split('T')[0] : '');

        this._modal.show();
    },

    save: async function() {
        var form = $('#commissionForm')[0];
        if (!form.checkValidity()) {
            $(form).addClass('was-validated');
            return;
        }

        const payload = {
            Rule: {
                Id: parseInt($('#Id').val()) || 0,
                Name: $('#Name').val().trim(),
                RuleType: parseInt($('#RuleType').val()),
                CategoryId: parseInt($('#CategoryId').val()) || null,
                EmployeeId: parseInt($('#EmployeeId').val()) || null,
                CommissionPercentage: parseFloat($('#CommissionPercentage').val()),
                ValidFrom: $('#ValidFrom').val() || null,
                ValidTo: $('#ValidTo').val() || null,
                IsActive: $('#IsActive').is(':checked')
            }
        };

        var $btn = $(event.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm"></i> Saving...');

        try {
            const res = await api.post('/api/CommissionRule', payload);
            if (res && res.succeeded) {
                toastr.success("Commission Matrix updated.");
                this._modal.hide();
                this._table.ajax.reload(null, false);
            }
        } catch (e) {
            console.error(e);
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    }
};

$(document).ready(() => commissionApp.init());