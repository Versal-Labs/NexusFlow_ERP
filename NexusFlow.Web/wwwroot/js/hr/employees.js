window.employeeApp = {
    _table: null,
    _modal: null,
    API_URL: "/api/Employee",

    init: function () {
        var modalEl = document.getElementById('employeeModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl);

        this._initGrid();
        this._registerEvents();
    },

    _registerEvents: function () {
        // Toggle the Warning Alert when the Sales Rep switch is flipped
        $('#IsSalesRep').on('change', function () {
            if ($(this).is(':checked')) {
                $('#salesRepAlert').slideDown(200);
            } else {
                $('#salesRepAlert').slideUp(200);
            }
        });

        // Prevent double bindings on save
        $('#employeeForm').off('submit').on('submit', function (e) {
            e.preventDefault();
            e.stopPropagation();
            if (!$(this)[0].checkValidity()) {
                $(this).addClass('was-validated');
                return;
            }
            window.employeeApp.save();
        });
    },

    _initGrid: function () {
        this._table = $('#employeesGrid').DataTable({
            ajax: {
                url: this.API_URL,
                type: "GET",
                dataSrc: function (json) { return json.data || json || []; },
                headers: { "Authorization": "Bearer " + localStorage.getItem("jwtToken") }
            },
            columns: [
                { data: "employeeCode", className: "font-monospace fw-bold text-primary" },
                { 
                    data: null, 
                    render: function(data, type, row) { 
                        return `${row.firstName} ${row.lastName}`; 
                    } 
                },
                { data: "email" },
                { data: "phone" },
                {
                    data: "isSalesRep",
                    render: function (d) {
                        return d === true 
                            ? '<span class="badge bg-success bg-opacity-10 text-success border border-success"><i class="bi bi-briefcase-fill me-1"></i>Sales Rep</span>' 
                            : '<span class="badge bg-secondary bg-opacity-10 text-secondary border border-secondary">Standard Staff</span>';
                    }
                },
                {
                    data: null,
                    className: "text-end pe-3",
                    orderable: false,
                    render: function (data, type, row) {
                        const safeRow = JSON.stringify(row).replace(/"/g, '&quot;');
                        return `<button type="button" class="btn btn-sm btn-light border px-2" onclick="employeeApp.edit(${safeRow})">
                                    <i class="bi bi-pencil text-secondary"></i>
                                </button>`;
                    }
                }
            ],
            order: [[0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
        });
    },

    openEditor: function () {
        var form = $('#employeeForm');
        form[0].reset();
        form.removeClass('was-validated');
        
        $('#modalTitle').html('<i class="bi bi-person-badge text-primary me-2"></i>Employee Onboarding');
        $('#hdnEmployeeId').val(0);
        $('#salesRepAlert').hide(); // Hide alert by default

        if (this._modal) this._modal.show();
    },

    edit: function (row) {
        var form = $('#employeeForm');
        form.removeClass('was-validated');
        
        $('#modalTitle').html('<i class="bi bi-pencil-square text-primary me-2"></i>Edit Employee');
        $('#hdnEmployeeId').val(row.id);
        
        $('#FirstName').val(row.firstName);
        $('#LastName').val(row.lastName);
        $('#Email').val(row.email);
        $('#Phone').val(row.phone);
        $('#NIC').val(row.nic);
        
        $('#BasicSalary').val(row.basicSalary);
        $('#EPF_No').val(row.epF_No);
        $('#BankName').val(row.bankName);
        $('#BankAccountNo').val(row.bankAccountNo);

        $('#IsSalesRep').prop('checked', row.isSalesRep).trigger('change');

        if (this._modal) this._modal.show();
    },

    save: async function () {
        var form = $('#employeeForm');
        if (!form[0].checkValidity()) {
            form.addClass('was-validated');
            return;
        }

        var id = parseInt($('#hdnEmployeeId').val()) || 0;
        
        var payload = {
            Employee: {
                Id: id,
                FirstName: $('#FirstName').val().trim(),
                LastName: $('#LastName').val().trim(),
                Email: $('#Email').val().trim(),
                Phone: $('#Phone').val().trim(),
                NIC: $('#NIC').val().trim(),
                BasicSalary: parseFloat($('#BasicSalary').val()) || 0,
                EPF_No: $('#EPF_No').val().trim(),
                BankName: $('#BankName').val().trim(),
                BankAccountNo: $('#BankAccountNo').val().trim(),
                IsSalesRep: $('#IsSalesRep').is(':checked')
            }
        };

        var $btn = $('#btnSaveEmployee');
        var originalText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Processing...');

        try {
            var result;
            if (id === 0) {
                result = await api.post(this.API_URL, payload);
            } else {
                result = await api.put(`${this.API_URL}/${id}`, payload);
            }

            if (result && result.succeeded) {
                this._modal.hide();
                this._table.ajax.reload(null, false);
            }
        } catch (e) {
            console.error("Save Error", e);
        } finally {
            $btn.prop('disabled', false).html(originalText);
        }
    }
};

$(document).ready(function () {
    window.employeeApp.init();
});