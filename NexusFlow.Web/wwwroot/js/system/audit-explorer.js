$(document).ready(function () {
    // 1. Initialize DataTable
    var auditTable = $('#auditTable').DataTable({
        serverSide: false,
        processing: true,
        ajax: {
            url: '/api/SystemAudit/datatable',
            type: 'GET',
            data: function (d) {
                // Pass filter values to the API
                d.startDate = $('#filterStartDate').val();
                d.endDate = $('#filterEndDate').val();
                d.action = $('#filterAction').val();
                d.userId = $('#filterUserId').val();
                d.searchTerm = $('#filterSearch').val();
            },
            // The API wraps the array in a 'data' property
            dataSrc: 'data'
        },
        columns: [
            {
                // ARCHITECTURAL FIX: Use PascalCase to match C# DTO
                data: 'Timestamp',
                defaultContent: '',
                render: function (data) {
                    if (!data) return '';
                    return new Date(data).toLocaleString();
                },
                width: '15%'
            },
            {
                data: 'Action',
                defaultContent: 'UNKNOWN',
                render: function (data) {
                    let badgeClass = data && data.includes("FAIL") ? "bg-danger" : "bg-secondary";
                    return `<span class="badge ${badgeClass}">${data}</span>`;
                },
                width: '15%'
            },
            { data: 'EntityName', width: '10%', defaultContent: '' },
            { data: 'UserId', width: '10%', defaultContent: 'SYSTEM' },
            { data: 'Details', width: '40%', defaultContent: '' },
            { data: 'IPAddress', width: '10%', defaultContent: 'N/A' }
        ],
        order: [[0, 'desc']], // Default sort by Timestamp Descending
        dom: '<"top">rt<"bottom"ilp><"clear">',
        pageLength: 50,
        language: {
            emptyTable: "No audit logs found matching the current filters."
        }
    });

    // 2. Attach Live Search & Filter Events
    $('#filterStartDate, #filterEndDate, #filterAction, #filterUserId').on('change', function () {
        auditTable.ajax.reload();
    });

    let searchTimeout;
    $('#filterSearch').on('keyup', function () {
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(function () {
            auditTable.ajax.reload();
        }, 400);
    });

    // 3. Manual Refresh Button
    $('#btnRefresh').on('click', function () {
        auditTable.ajax.reload();
    });
});