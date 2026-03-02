$(document).ready(function () {
    // 1. Initialize DataTable
    var table = $('#financialPeriodsTable').DataTable({
        ajax: {
            url: '/api/finance/periods',
            dataSrc: 'data',
            headers: {
                "Authorization": "Bearer " + localStorage.getItem("jwtToken")
            }
        },
        columns: [
            { data: 'year' },
            { data: 'month' },
            { data: 'startDate' },
            { data: 'endDate' },
            {
                data: 'isClosed',
                render: function (data) {
                    return data
                        ? '<span class="badge bg-danger">Closed</span>'
                        : '<span class="badge bg-success">Open</span>';
                }
            },
            {
                data: 'id',
                orderable: false,
                render: function (data, type, row) {
                    if (!row.isClosed) {
                        return `<button class="btn btn-sm btn-outline-danger py-0 px-1" onclick="closePeriod(${data})"><i class="bi bi-lock"></i> Close</button>`;
                    }
                    return `<span class="text-muted"><i class="bi bi-lock-fill"></i> Locked</span>`;
                }
            }
        ],
        order: [[0, 'desc'], [1, 'desc']],
        pageLength: 25,
        dom: '<"row"<"col-md-6"f><"col-md-6 text-end"B>>rtip',
    });

    // 2. Auto-fill dates based on Year/Month selection
    $('#Year, #Month').on('change', function () {
        var y = $('#Year').val();
        var m = $('#Month').val();
        if (y && m) {
            var firstDay = new Date(y, m - 1, 2).toISOString().split('T')[0];
            var lastDay = new Date(y, m, 1).toISOString().split('T')[0];
            $('#StartDate').val(firstDay);
            $('#EndDate').val(lastDay);
        }
    });

    // Initialize with current year/month
    const now = new Date();
    $('#Year').val(now.getFullYear());
    $('#Month').val(now.getMonth() + 1).trigger('change');

    // 3. Save Form (AJAX)
    $('#btnSavePeriod').click(function () {
        var form = $('#createPeriodForm')[0];
        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        var payload = {
            year: parseInt($('#Year').val()),
            month: parseInt($('#Month').val()),
            startDate: $('#StartDate').val(),
            endDate: $('#EndDate').val()
        };

        $.ajax({
            url: '/api/finance/periods',
            type: 'POST',
            contentType: 'application/json',
            headers: { "Authorization": "Bearer " + localStorage.getItem("jwtToken") },
            data: JSON.stringify(payload),
            success: function (response) {
                if (response.succeeded) {
                    $('#createPeriodModal').modal('hide');
                    table.ajax.reload();
                    toastr.success('Financial Period opened successfully.');
                }
            },
            error: function (xhr) {
                toastr.error(xhr.responseJSON?.messages?.[0] || 'Error saving period');
            }
        });
    });
});

function closePeriod(id) {
    // We use standard Swal for the blocking prompt, but toastr for the result response
    Swal.fire({
        title: 'Close Period?',
        text: "Are you sure you want to close this period? No further transactions can be posted.",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'Yes, close it!'
    }).then((result) => {
        if (result.isConfirmed) {
            toastr.info('Close logic pending implementation.');
        }
    });
}