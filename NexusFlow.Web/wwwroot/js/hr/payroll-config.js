window.pcApp = {
    _componentsGrid: null,
    _assignmentsGrid: null,
    _loansGrid: null,
    _loanModal: null,

    init: function () {
        var loanEl = document.getElementById('loanModal');
        if (loanEl) this._loanModal = new bootstrap.Modal(loanEl);

        // Load employees for dropdowns (Assuming an endpoint exists)
        this._loadEmployees();

        // Initialize Grids (Assuming standard GET endpoints in PayrollController)
        this._initComponentsGrid();
        this._initAssignmentsGrid();
        this._initLoansGrid();

        // Set defaults
        $('#LoanDate').val(new Date().toISOString().split('T')[0]);
    },

    _loadEmployees: async function () {
        try {
            const res = await api.get('/api/hr/employees/lookup');
            let opts = '<option value="">-- Select Employee --</option>';
            (res.data || res || []).forEach(e => opts += `<option value="${e.id}">${e.firstName} ${e.lastName} (${e.employeeCode})</option>`);
            $('.select2-emp').html(opts).select2({ dropdownParent: $('#loanModal') });
        } catch (e) { }
    },

    _initComponentsGrid: function () {
        this._componentsGrid = $('#componentsGrid').DataTable({
            ajax: { url: '/api/payroll/components', dataSrc: d => d.data || d || [] },
            columns: [
                { data: 'name', className: 'fw-bold' },
                { data: 'type', render: d => d === 1 ? '<span class="badge bg-success">Allowance</span>' : d === 2 ? '<span class="badge bg-danger">Deduction</span>' : '<span class="badge bg-secondary">Statutory</span>' },
                { data: 'calculationType', render: d => d === 1 ? 'Fixed' : d === 2 ? 'Per Day' : '%' },
                { data: 'defaultRate', className: 'text-end', render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'isTaxable', className: 'text-center', render: d => d ? '<i class="fa-solid fa-check text-success"></i>' : '-' },
                { data: 'isEPFCalculable', className: 'text-center', render: d => d ? '<i class="fa-solid fa-check text-success"></i>' : '-' },
                { data: null, orderable: false, className: 'text-end', render: () => `<button class="btn btn-sm btn-outline-secondary"><i class="fa-solid fa-pen"></i></button>` }
            ]
        });
    },

    _initAssignmentsGrid: function () {
        this._assignmentsGrid = $('#assignmentsGrid').DataTable({
            ajax: { url: '/api/payroll/assignments', dataSrc: d => d.data || d || [] },
            columns: [
                { data: 'employeeName', className: 'fw-bold' },
                { data: 'componentName' },
                { data: 'overrideRate', className: 'text-end text-primary fw-bold', render: d => d ? parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) : '<span class="text-muted fst-italic">Default</span>' },
                { data: 'isActive', className: 'text-center', render: d => d ? '<span class="badge bg-success">Active</span>' : '<span class="badge bg-danger">Disabled</span>' },
                { data: null, orderable: false, className: 'text-end', render: () => `<button class="btn btn-sm btn-outline-danger"><i class="fa-solid fa-trash"></i></button>` }
            ]
        });
    },

    _initLoansGrid: function () {
        this._loansGrid = $('#loansGrid').DataTable({
            ajax: { url: '/api/payroll/loans', dataSrc: d => d.data || d || [] },
            columns: [
                { data: 'employeeName', className: 'fw-bold' },
                { data: 'disbursementDate', render: d => new Date(d).toLocaleDateString() },
                { data: 'principalAmount', className: 'text-end', render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'termInMonths', className: 'text-center font-monospace' },
                { data: 'emiAmount', className: 'text-end text-danger fw-bold', render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'status', className: 'text-center', render: d => d === 1 ? '<span class="badge bg-warning text-dark">Active</span>' : '<span class="badge bg-success">Cleared</span>' }
            ]
        });
    },

    // ==================
    // MODALS & SAVING
    // ==================
    openLoanModal: function () {
        $('#loanForm')[0].reset();
        $('#loanForm').removeClass('was-validated');

        // Default start deduction month to NEXT month
        const now = new Date();
        now.setMonth(now.getMonth() + 1);
        $('#LoanStartMonth').val(now.getFullYear() + '-' + String(now.getMonth() + 1).padStart(2, '0'));

        $('#LoanEmployeeId').val('').trigger('change');
        this._loanModal.show();
    },

    saveLoan: async function () {
        var form = $('#loanForm')[0];
        if (!form.checkValidity()) { $(form).addClass('was-validated'); return; }

        const payload = {
            EmployeeId: parseInt($('#LoanEmployeeId').val()),
            DisbursementDate: $('#LoanDate').val(),
            PrincipalAmount: parseFloat($('#LoanPrincipal').val()),
            InterestRatePercentage: parseFloat($('#LoanInterest').val() || 0),
            TermInMonths: parseInt($('#LoanTerm').val()),
            StartDeductionMonth: $('#LoanStartMonth').val()
        };

        const result = await Swal.fire({
            title: 'Issue Loan?',
            text: "This will generate the amortization schedule automatically.",
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Yes, Issue Loan'
        });

        if (!result.isConfirmed) return;

        try {
            const res = await api.post('/api/payroll/loans/issue', payload);
            if (res && res.succeeded) {
                toastr.success(res.message);
                this._loanModal.hide();
                this._loansGrid.ajax.reload();
            } else {
                toastr.error(res.messages[0]);
            }
        } catch (e) {
            toastr.error(e.responseJSON?.messages?.[0] || "Failed to issue loan.");
        }
    }
};

$(document).ready(() => pcApp.init());