window.payrollApp = {
    _table: null,
    _currentPeriodId: null,

    init: function () {
        // Default to current month
        const now = new Date();
        const monthStr = now.getFullYear() + '-' + String(now.getMonth() + 1).padStart(2, '0');
        $('#filterMonthYear').val(monthStr);

        this._initGrid();
        this.loadPeriod();
    },

    loadPeriod: async function () {
        const monthYear = $('#filterMonthYear').val();
        if (!monthYear) return;

        try {
            $('#payrollGrid_processing').show();
            // Assuming you have an endpoint that returns the PayrollPeriod and its Slips by MonthYear
            const res = await api.get(`/api/payroll/period?monthYear=${monthYear}`);
            const period = res.data || res;

            if (!period || !period.id) {
                // No payroll exists for this month yet
                this._currentPeriodId = null;
                this._table.clear().draw();
                this._updateUI(null);
                return;
            }

            this._currentPeriodId = period.id;
            this._table.clear().rows.add(period.slips || []).draw();
            this._updateUI(period.status);

        } catch (e) {
            this._currentPeriodId = null;
            this._table.clear().draw();
            this._updateUI(null);
        } finally {
            $('#payrollGrid_processing').hide();
        }
    },

    _updateUI: function (status) {
        $('#btnGenerate, #btnApprove, #btnExports').addClass('d-none');
        let $banner = $('#statusBanner');

        if (status === null) {
            $banner.html('<span class="badge bg-secondary px-3 py-2"><i class="fa-solid fa-circle-info me-1"></i> No Payroll Generated</span>');
            $('#btnGenerate').removeClass('d-none');
        }
        else if (status === 1 || status === 'Draft') {
            $banner.html('<span class="badge bg-warning text-dark px-3 py-2"><i class="fa-solid fa-pen-ruler me-1"></i> Draft Mode (Unposted)</span>');
            $('#btnGenerate').removeClass('d-none').html('<i class="fa-solid fa-rotate me-1"></i> Re-Calculate Draft');
            $('#btnApprove').removeClass('d-none');
        }
        else if (status === 3 || status === 'Posted' || status === 4 || status === 'Paid') {
            $banner.html('<span class="badge bg-success px-3 py-2"><i class="fa-solid fa-lock me-1"></i> Posted to GL & Finalized</span>');
            $('#btnExports').removeClass('d-none');
        }
    },

    _initGrid: function () {
        this._table = $('#payrollGrid').DataTable({
            data: [],
            columns: [
                {
                    data: 'employeeName', className: 'ps-3',
                    render: (d, t, r) => `<span class="fw-bold text-dark">${d}</span><br><small class="text-muted font-monospace">${r.employeeCode}</small>`
                },
                { data: 'grossBasic', className: 'text-end', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'totalAllowances', className: 'text-end text-success', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'totalDeductions', className: 'text-end text-danger', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'netPay', className: 'text-end fw-bold text-primary', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                {
                    data: null, className: 'text-center pe-3', orderable: false,
                    render: function (data, type, row) {
                        return `<button class="btn btn-sm btn-outline-dark shadow-sm" onclick="payrollApp.viewSlip(${row.id})" title="View Payslip PDF">
                                    <i class="fa-solid fa-file-pdf text-danger"></i>
                                </button>`;
                    }
                }
            ],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 50,
            footerCallback: function () {
                let api = this.api();
                let intVal = i => typeof i === 'string' ? i.replace(/[\$,]/g, '') * 1 : typeof i === 'number' ? i : 0;

                $('#footBasic').html(api.column(1, { page: 'current' }).data().reduce((a, b) => intVal(a) + intVal(b), 0).toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $('#footAllowances').html(api.column(2, { page: 'current' }).data().reduce((a, b) => intVal(a) + intVal(b), 0).toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $('#footDeductions').html(api.column(3, { page: 'current' }).data().reduce((a, b) => intVal(a) + intVal(b), 0).toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $('#footNetPay').html(api.column(4, { page: 'current' }).data().reduce((a, b) => intVal(a) + intVal(b), 0).toLocaleString(undefined, { minimumFractionDigits: 2 }));
            }
        });
    },

    // =====================================
    // ACTIONS
    // =====================================
    generateDraft: async function () {
        const monthYear = $('#filterMonthYear').val(); // format: "YYYY-MM"
        if (!monthYear) return;

        const [year, month] = monthYear.split('-');

        const result = await Swal.fire({
            title: 'Generate Draft Payroll?',
            text: "This will calculate attendance, loans, and statutory taxes. It will NOT post to the GL yet.",
            icon: 'info',
            showCancelButton: true,
            confirmButtonColor: '#0d6efd',
            confirmButtonText: 'Yes, Calculate!'
        });

        if (!result.isConfirmed) return;

        try {
            // Note: Update your API to accept this POST request to trigger the Hangfire job/Command
            await api.post(`/api/payroll/generate?year=${year}&month=${month}`);
            toastr.success("Draft Payroll Generated Successfully!");
            this.loadPeriod(); // Reload the grid
        } catch (e) {
            toastr.error(e.responseJSON?.message || "Failed to generate payroll.");
        }
    },

    approveAndPost: async function () {
        if (!this._currentPeriodId) return;

        const result = await Swal.fire({
            title: 'Approve & Post to General Ledger?',
            text: "WARNING: This action is irreversible. It will create financial journal entries and lock the payroll.",
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#198754',
            confirmButtonText: 'Yes, Approve & Post!'
        });

        if (!result.isConfirmed) return;

        try {
            const res = await api.post(`/api/payroll/${this._currentPeriodId}/post`);
            if (res && res.succeeded) {
                toastr.success(res.message || "Payroll successfully posted to GL!");
                this.loadPeriod(); // Reload to update status banner and buttons
            }
        } catch (e) {
            toastr.error(e.responseJSON?.messages?.[0] || "Failed to post payroll.");
        }
    },

    viewSlip: function (slipId) {
        NexusPrint.openPreview('Payslip', slipId);
    },

    exportBank: function () {
        if (!this._currentPeriodId) return;
        window.open(`/api/payroll/${this._currentPeriodId}/export-bank-file`, '_blank');
    },

    exportEpf: function () {
        if (!this._currentPeriodId) return;
        window.open(`/api/payroll/${this._currentPeriodId}/export-epf-return`, '_blank');
    }
};

$(document).ready(() => payrollApp.init());
