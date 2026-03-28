window.fulfillmentApp = {
    _table: null,
    _modal: null,

    init: function() {
        var modalEl = document.getElementById('conversionModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl);

        this._initGrid();
        this._loadLookups();
    },

    _initGrid: function() {
        this._table = $('#fulfillmentGrid').DataTable({
            ajax: {
                url: '/api/sales/orders',
                dataSrc: function(json) { 
                    const data = json.data || json || [];
                    // ARCHITECTURAL FILTER: Only show 'Submitted' orders to the Back-Office
                    return data.filter(o => o.statusText === 'Submitted');
                }
            },
            columns: [
                { data: 'orderNumber', className: 'fw-bold font-monospace text-primary ps-4' },
                { data: 'orderDate', render: d => new Date(d).toLocaleDateString() },
                { data: 'customerName', className: 'fw-bold text-dark' },
                { data: 'salesRepName', className: 'text-muted' },
                { 
                    data: 'totalAmount', 
                    className: 'text-end fw-bold',
                    render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 })
                },
                {
                    data: 'statusText',
                    className: 'text-center',
                    render: d => '<span class="badge bg-info text-dark"><i class="fa-solid fa-clock-rotate-left me-1"></i>Awaiting Fulfillment</span>'
                },
                {
                    data: null,
                    className: 'text-end pe-4',
                    orderable: false,
                    render: function(data, type, row) {
                        return `<button class="btn btn-sm btn-primary shadow-sm fw-bold" onclick="fulfillmentApp.openConversionModal(${row.id}, '${row.orderNumber}')">
                                    Convert <i class="fa-solid fa-arrow-right ms-1"></i>
                                </button>`;
                    }
                }
            ],
            order: [[0, 'asc']], // Oldest submitted orders first!
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    _loadLookups: async function() {
        try {
            const whRes = await api.get('/api/masterdata/warehouses');
            const warehouses = Array.isArray(whRes) ? whRes : (whRes?.data || []);

            let $wh = $('#DispatchWarehouseId').empty().append('<option value="">-- Select Target Warehouse --</option>');
            warehouses.forEach(w => $wh.append($('<option></option>').val(w.id).text(w.name)));
        } catch (e) {
            console.error("Lookup Error:", e);
        }
    },

    openConversionModal: async function(orderId, orderNo) {
        $('#conversionForm')[0].reset();
        $('#conversionForm').removeClass('was-validated');
        $('#ConvertOrderId').val(orderId);
        $('#lblConvertOrderNo').text(orderNo);

        // Render loading state
        $('#orderItemsBody').html('<tr><td colspan="5" class="text-center text-muted py-3"><i class="spinner-border spinner-border-sm me-2"></i>Fetching items...</td></tr>');
        $('#lblOrderGrandTotal').text('0.00');

        // Pop the modal immediately while data fetches
        this._modal.show();

        try {
            // Fetch the exact lines via the API we just created
            const res = await api.get(`/api/sales/orders/${orderId}`);
            const order = res.data || res;

            let $tbody = $('#orderItemsBody');
            $tbody.empty();

            if (order && order.items && order.items.length > 0) {
                order.items.forEach(item => {
                    $tbody.append(`
                        <tr>
                            <td class="fw-bold text-dark">${item.description}</td>
                            <td class="text-center font-monospace">${item.quantity}</td>
                            <td class="text-end text-muted">${item.unitPrice.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                            <td class="text-end text-danger">${item.discount > 0 ? '-' + item.discount.toLocaleString(undefined, { minimumFractionDigits: 2 }) : '-'}</td>
                            <td class="text-end fw-bold">${item.lineTotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                        </tr>
                    `);
                });
                
                $('#lblOrderGrandTotal').text(order.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 }));
            } else {
                $tbody.html('<tr><td colspan="5" class="text-center text-danger py-3">No line items found for this order.</td></tr>');
            }
        } catch (e) {
            console.error("Failed to load order details", e);
            $('#orderItemsBody').html('<tr><td colspan="5" class="text-center text-danger py-3">Failed to load order details.</td></tr>');
            toastr.error("Could not fetch order line items.");
        }
    },

    executeConversion: async function() {
        var form = $('#conversionForm')[0];
        if (!form.checkValidity()) {
            $(form).addClass('was-validated');
            return;
        }

        const payload = {
            SalesOrderId: parseInt($('#ConvertOrderId').val()),
            WarehouseId: parseInt($('#DispatchWarehouseId').val())
        };

        var $btn = $(event.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm"></i> Processing...');

        try {
            const res = await api.post(`/api/sales/orders/${payload.SalesOrderId}/convert`, payload);
            if (res && res.succeeded) {
                toastr.success(res.message || "Order successfully converted to Invoice!");
                this._modal.hide();
                this._table.ajax.reload(null, false);
            } else {
                toastr.error(res.message || "Conversion failed. Please check stock levels.");
            }
        } catch (e) {
            console.error(e);
            toastr.error("An error occurred during conversion.");
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    }
};

$(document).ready(() => fulfillmentApp.init());