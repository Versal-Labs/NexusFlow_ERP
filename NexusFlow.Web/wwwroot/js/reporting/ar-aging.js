window.arApp = {
    _table: null,

    init: function () {
        this._loadLookups();
        this._initGrid();
    },

    _loadLookups: async function () {
        try {
            const custRes = await api.get('/api/customer');
            const customers = custRes.data || custRes || [];
            let $cust = $('#filterCustomer').empty().append('<option value="">All Customers</option>');
            customers.forEach(c => $cust.append(`<option value="${c.id}">${c.name}</option>`));
            $cust.select2();
        } catch (e) { console.error("Lookup failed"); }
    },

    _initGrid: function () {
        const self = this;

        this._table = $('#arGrid').DataTable({
            ajax: async function (data, callback) {
                try {
                    let q = $('#filterCustomer').val() ? `?CustomerId=${$('#filterCustomer').val()}` : '';
                    const res = await api.get('/api/reporting/ar-aging' + q);
                    callback({ data: res.data || res || [] });
                } catch (e) { callback({ data: [] }); }
            },
            columns: [
                {
                    data: 'customerName', className: 'fw-bold text-dark ps-3',
                    render: function (d, type, row) {
                        return `${d}<br><small class="text-muted"><i class="fa-solid fa-phone"></i> ${row.phone || 'N/A'}</small>`;
                    }
                },
                { data: 'totalOutstanding', className: 'text-end fw-bold text-primary', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'current', className: 'text-end', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'days1To30', className: 'text-end', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'days31To60', className: 'text-end', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'days61To90', className: 'text-end', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                { data: 'over90Days', className: 'text-end fw-bold text-danger', render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                {
                    data: null, className: 'text-center pe-3', orderable: false,
                    render: function (data, type, row) {
                        let disabled = row.phone ? '' : 'disabled';
                        return `
                            <a href="/Reporting/CustomerStatement?customerId=${row.customerId}" target="_blank" class="btn btn-sm btn-outline-dark shadow-sm me-1" title="View Statement">
                                <i class="fa-solid fa-file-lines"></i>
                            </a>
                            <button class="btn btn-sm btn-primary shadow-sm" ${disabled} onclick="arApp.sendSms(${row.customerId}, '${row.customerName}')" title="Send SMS Reminder">
                                <i class="fa-solid fa-comment-sms"></i> SMS
                            </button>`;
                    }
                }
            ],
            order: [[1, 'desc']], // Order by Total Outstanding descending
            dom: '<"d-flex justify-content-between align-items-center mb-3"l>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 20,

            footerCallback: function () {
                let api = this.api();
                let intVal = i => typeof i === 'string' ? i.replace(/[\$,]/g, '') * 1 : typeof i === 'number' ? i : 0;

                let sums = [1, 2, 3, 4, 5, 6].map(col => api.column(col, { page: 'current' }).data().reduce((a, b) => intVal(a) + intVal(b), 0));

                $('#footTotal').html(sums[0].toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $('#footCurrent').html(sums[1].toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $('#foot30').html(sums[2].toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $('#foot60').html(sums[3].toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $('#foot90').html(sums[4].toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $('#footOver90').html(sums[5].toLocaleString(undefined, { minimumFractionDigits: 2 }));
            }
        });
    },

    reloadGrid: async function () {
        try {
            $('#arGrid_processing').show();
            let q = $('#filterCustomer').val() ? `?CustomerId=${$('#filterCustomer').val()}` : '';
            const res = await api.get('/api/reporting/ar-aging' + q);
            this._table.clear().rows.add(res.data || res || []).draw();
        } catch (e) { toastr.error("Failed to refresh report."); }
        finally { $('#arGrid_processing').hide(); }
    },

    sendSms: async function (customerId, customerName) {
        const result = await Swal.fire({
            title: `Send Reminder to ${customerName}?`,
            text: "This will dispatch an SMS containing their outstanding balance.",
            icon: 'info',
            showCancelButton: true,
            confirmButtonColor: '#0d6efd',
            confirmButtonText: '<i class="fa-solid fa-paper-plane me-1"></i> Send SMS'
        });

        if (!result.isConfirmed) return;

        try {
            const res = await api.post('/api/reporting/ar-aging/remind', { CustomerId: customerId });
            if (res && res.succeeded) {
                toastr.success(res.message);
            } else { toastr.error(res.messages[0]); }
        } catch (e) { toastr.error("Failed to trigger SMS Gateway."); }
    },

    export: function (format) {
        let q = $('#filterCustomer').val() ? `?CustomerId=${$('#filterCustomer').val()}` : '';
        const url = format === 'excel' ? '/api/reporting/ar-aging/export/excel' : '/api/reporting/ar-aging/export/pdf';
        window.open(url + q, '_blank');
    }
};

$(document).ready(() => arApp.init());