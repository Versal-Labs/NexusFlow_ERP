window.fulfillmentApp = {
    _table: null,
    _modal: null,

    init: function() {
        var modalEl = document.getElementById('conversionModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl);

        this._initFilters();
        this._initGrid();
        this._loadLookups();
    },

    // ==========================================
    // FILTERS
    // ==========================================
    _initFilters: function () {
        // Load Customers for dropdown safely
        api.get('/api/customer').then(res => {
            // Safely parse the array (handles both raw arrays and your Result<T> wrappers)
            let customers = Array.isArray(res) ? res : (res?.data || []);
            if (!Array.isArray(customers)) customers = [];

            let $cust = $('#filterCustomer').empty().append('<option value="">-- All Customers --</option>');
            customers.forEach(c => $cust.append($('<option></option>').val(c.name).text(c.name)));
        }).catch(err => console.error("Failed to load customers for filter", err));

        // Setup Custom DataTables Filter Logic
        $.fn.dataTable.ext.search.push((settings, data, dataIndex) => {
            if (settings.nTable.id !== 'fulfillmentGrid') return true;

            // TIER-1 FIX: Safely handle null dropdown values
            const filterCust = ($('#filterCustomer').val() || '').toLowerCase();
            const filterStart = $('#filterStartDate').val();
            const filterEnd = $('#filterEndDate').val();

            // TIER-1 FIX: Safely handle null cell data so .toLowerCase() never crashes
            const rowDateStr = data[1] || '';
            const rowCust = (data[2] || '').toLowerCase();

            // Customer Check
            if (filterCust && !rowCust.includes(filterCust)) return false;

            // Date Check
            if (filterStart || filterEnd) {
                // If date is completely missing in the row, hide it if a filter is active
                if (!rowDateStr) return false;

                const rowDate = new Date(rowDateStr);

                if (filterStart && rowDate < new Date(filterStart)) return false;

                // UX FIX: Append T23:59:59 so the end date includes the *entire* last day 
                if (filterEnd && rowDate > new Date(filterEnd + 'T23:59:59')) return false;
            }

            return true;
        });
    },

    applyFilters: function() {
        this._table.draw(); // Triggers the custom search extension above
    },

    resetFilters: function() {
        $('#filterCustomer').val('');
        $('#filterStartDate').val('');
        $('#filterEndDate').val('');
        this._table.draw();
    },

    // ==========================================
    // GRID
    // ==========================================
    _initGrid: function() {
        this._table = $('#fulfillmentGrid').DataTable({
            ajax: {
                url: '/api/sales/orders',
                dataSrc: function(json) { 
                    const data = json.data || json || [];
                    // ARCHITECTURAL FILTER: Only show 'Submitted' orders
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
                                    Fulfill <i class="fa-solid fa-boxes-stacked ms-1"></i>
                                </button>`;
                    }
                }
            ],
            order: [[1, 'asc']], // Oldest submitted orders first!
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

    // ==========================================
    // MODAL & STOCK VALIDATION
    // ==========================================
    openConversionModal: async function(orderId, orderNo) {
        $('#conversionForm')[0].reset();
        $('#conversionForm').removeClass('was-validated');
        $('#ConvertOrderId').val(orderId);
        $('#lblConvertOrderNo').text(orderNo);
        $('#btnExecuteConversion').prop('disabled', true); // Disabled until warehouse selected
        $('#stockWarningAlert').addClass('d-none');

        $('#orderItemsBody').html('<tr><td colspan="6" class="text-center text-muted py-4"><i class="spinner-border spinner-border-sm me-2"></i>Fetching order details...</td></tr>');
        $('#lblOrderGrandTotal').text('0.00');

        this._modal.show();

        try {
            const res = await api.get(`/api/sales/orders/${orderId}/document`);
            const order = res.data || res;

            let $tbody = $('#orderItemsBody');
            $tbody.empty();

            if (order && order.items && order.items.length > 0) {
                order.items.forEach(item => {
                    // Injecting variant ID and required qty as data attributes for the stock validator
                    $tbody.append(`
                        <tr data-vid="${item.productVariantId}" data-qty="${item.quantity}">
                            <td class="fw-bold text-dark">${item.productDescription}</td>
                            <td class="text-center font-monospace fw-bold">${item.quantity}</td>
                            
                            <td class="text-center bg-warning bg-opacity-10 stock-cell text-muted">- Select WH -</td>
                            
                            <td class="text-end text-muted">${item.unitPrice.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                            <td class="text-end text-danger">${item.discount > 0 ? '-' + item.discount.toLocaleString(undefined, { minimumFractionDigits: 2 }) : '-'}</td>
                            <td class="text-end fw-bold">${item.lineTotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                        </tr>
                    `);
                });
                
                $('#lblOrderGrandTotal').text(order.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 }));
            } else {
                $tbody.html('<tr><td colspan="6" class="text-center text-danger py-3">No line items found for this order.</td></tr>');
            }
        } catch (e) {
            console.error("Failed to load order details", e);
            $('#orderItemsBody').html('<tr><td colspan="6" class="text-center text-danger py-3">Failed to load order details.</td></tr>');
        }
    },

    validateStock: async function() {
        const warehouseId = $('#DispatchWarehouseId').val();
        let allStockAvailable = true;
        
        if (!warehouseId) {
            $('#orderItemsBody tr').each(function() { $(this).find('.stock-cell').html('<span class="text-muted">- Select WH -</span>'); });
            $('#btnExecuteConversion').prop('disabled', true);
            $('#stockWarningAlert').addClass('d-none');
            return;
        }

        // Show loading spinners in all stock cells
        $('#orderItemsBody tr').each(function() {
            $(this).find('.stock-cell').html('<i class="spinner-border spinner-border-sm text-secondary"></i>');
        });

        // Fetch stock asynchronously for every line item
        const rows = $('#orderItemsBody tr').toArray();
        for (const row of rows) {
            const $row = $(row);
            const varId = $row.data('vid');
            const reqQty = parseFloat($row.data('qty'));
            const $cell = $row.find('.stock-cell');

            try {
                const res = await api.get(`/api/inventory/stock/available?variantId=${varId}&warehouseId=${warehouseId}`);
                const available = res.data !== undefined ? res.data : (res || 0);

                if (available >= reqQty) {
                    $cell.html(`<span class="text-success fw-bold"><i class="fa-solid fa-check me-1"></i> ${available}</span>`);
                    $row.removeClass('table-danger');
                } else {
                    $cell.html(`<span class="text-danger fw-bold"><i class="fa-solid fa-xmark me-1"></i> ${available}</span>`);
                    $row.addClass('table-danger');
                    allStockAvailable = false; // Flag a shortage
                }
            } catch (e) {
                $cell.html('<span class="text-danger">Error</span>');
                allStockAvailable = false;
            }
        }

        // Enterprise Guard: Toggle UI based on stock validation
        if (allStockAvailable) {
            $('#btnExecuteConversion').prop('disabled', false);
            $('#stockWarningAlert').addClass('d-none');
        } else {
            $('#btnExecuteConversion').prop('disabled', true);
            $('#stockWarningAlert').removeClass('d-none');
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

        var $btn = $('#btnExecuteConversion');
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm"></i> Processing...');

        try {
            const res = await api.post(`/api/sales/orders/${payload.SalesOrderId}/convert`, payload);
            if (res && res.succeeded) {
                toastr.success(res.message || "Order successfully converted to Invoice!");
                this._modal.hide();
                this._table.ajax.reload(null, false);
            } else {
                toastr.error(res.messages?.[0] || "Conversion failed. Please check stock levels.");
            }
        } catch (e) {
            toastr.error(e.responseJSON?.messages?.[0] || "An error occurred during conversion.");
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    }
};

$(document).ready(() => fulfillmentApp.init());