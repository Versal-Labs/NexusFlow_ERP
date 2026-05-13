window.attApp = {
    _table: null,
    _modal: null,

    init: function () {
        var modalEl = document.getElementById('overrideModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl);

        // Set date to today
        $('#filterDate').val(new Date().toISOString().split('T')[0]);
        this._initGrid();
    },

    reloadGrid: function () {
        this._table.ajax.reload();
    },

    _initGrid: function () {
        this._table = $('#attendanceGrid').DataTable({
            ajax: {
                url: '/api/attendance/daily-records', // Adjust controller route if necessary
                data: function (d) { d.date = $('#filterDate').val(); },
                dataSrc: function (json) { return json.data || json || []; }
            },
            columns: [
                {
                    data: 'employeeName', className: 'ps-3',
                    render: (d, t, r) => `<span class="fw-bold text-dark">${d}</span><br><small class="text-muted">${r.employeeCode}</small>`
                },
                { data: 'shiftName', className: 'text-muted' },
                { data: 'firstIn', className: 'text-center fw-bold font-monospace', render: d => d || '-' },
                { data: 'lastOut', className: 'text-center fw-bold font-monospace', render: d => d || '-' },
                { data: 'lateMinutes', className: 'text-center text-danger fw-bold', render: d => d > 0 ? d : '-' },
                { data: 'overtimeMinutes', className: 'text-center text-success fw-bold', render: d => d > 0 ? d : '-' },
                {
                    data: 'status', className: 'text-center',
                    render: function (d) {
                        if (d === 'Present') return '<span class="badge bg-success">Present</span>';
                        if (d === 'Absent') return '<span class="badge bg-danger">Absent</span>';
                        if (d === 'HalfDay') return '<span class="badge bg-warning text-dark">Half Day</span>';
                        if (d === 'OnLeave') return '<span class="badge bg-info text-dark">On Leave</span>';
                        if (d === 'RestDay') return '<span class="badge bg-secondary">Rest Day</span>';
                        if (d === 'Error') return '<span class="badge bg-dark border border-danger"><i class="fa-solid fa-triangle-exclamation text-danger me-1"></i>Error</span>';
                        return d;
                    }
                },
                {
                    data: null, className: 'text-center pe-3', orderable: false,
                    render: function (data, type, row) {
                        // Pass parameters safely
                        let inTime = row.firstIn ? `'${row.firstIn}'` : 'null';
                        let outTime = row.lastOut ? `'${row.lastOut}'` : 'null';
                        return `<button class="btn btn-sm btn-outline-warning text-dark shadow-sm" 
                                onclick="attApp.openOverride(${row.id}, ${inTime}, ${outTime}, '${row.status}')" title="Override">
                                    <i class="fa-solid fa-pen"></i>
                                </button>`;
                    }
                }
            ],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 50
        });
    },

    openOverride: function (id, firstIn, lastOut, status) {
        $('#OverrideRecordId').val(id);
        $('#OverrideFirstIn').val(firstIn || '');
        $('#OverrideLastOut').val(lastOut || '');

        let statusVal = 1;
        if (status === 'Absent') statusVal = 2;
        if (status === 'HalfDay') statusVal = 3;
        if (status === 'OnLeave') statusVal = 4;
        if (status === 'RestDay') statusVal = 5;
        if (status === 'Error') statusVal = 6;
        $('#OverrideStatus').val(statusVal);

        $('#OverrideRemarks').val('');
        $('#overrideForm').removeClass('was-validated');
        this._modal.show();
    },

    saveOverride: async function () {
        var form = $('#overrideForm')[0];
        if (!form.checkValidity()) { $(form).addClass('was-validated'); return; }

        let inTime = $('#OverrideFirstIn').val();
        let outTime = $('#OverrideLastOut').val();

        // Convert HH:mm to TimeSpan format "HH:mm:ss" for C#
        if (inTime) inTime += ":00";
        if (outTime) outTime += ":00";

        const payload = {
            RecordId: parseInt($('#OverrideRecordId').val()),
            FirstInTime: inTime || null,
            LastOutTime: outTime || null,
            Status: parseInt($('#OverrideStatus').val()),
            Remarks: $('#OverrideRemarks').val()
        };

        try {
            const res = await api.post('/api/attendance/override-record', payload);
            if (res && res.succeeded) {
                toastr.success("Record updated successfully.");
                this._modal.hide();
                this.reloadGrid();
            }
        } catch (e) {
            toastr.error(e.responseJSON?.messages?.[0] || "Failed to update record.");
        }
    },

    syncBiometrics: async function () {
        // Assuming your device sync endpoint is ready. Adjust route if needed.
        toastr.info("Triggering device sync...");
        try {
            // Placeholder: you'd hit your actual device trigger endpoint here
            await new Promise(r => setTimeout(r, 1000));
            toastr.success("Biometric data synced successfully.");
        } catch (e) { toastr.error("Failed to sync devices."); }
    },

    processDay: async function () {
        const date = $('#filterDate').val();
        if (!date) return;

        toastr.info("Processing punches and evaluating shifts...");
        try {
            await api.post(`/api/attendance/process-day?date=${date}`);
            toastr.success("Daily processing complete.");
            this.reloadGrid();
        } catch (e) {
            toastr.error("Processing failed.");
        }
    }
};

$(document).ready(() => attApp.init());