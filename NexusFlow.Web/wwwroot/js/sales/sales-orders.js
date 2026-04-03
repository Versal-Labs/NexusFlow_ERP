window.orderApp = {
    _table: null,
    _modal: null,

    init: function () {
        var modalEl = document.getElementById('orderModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl);

        document.getElementById('OrderDate').valueAsDate = new Date();

        this._initGrid();
        this._loadLookups();

        // Recalculate totals on input changes
        $('#cartBody').on('input', '.calc-trigger', () => this.calculateTotals());

        // Select2 Item Adding (POS Style)
        $('#ProductSearch').on('select2:select', function (e) {
            var data = e.params.data;
            if (data.id) {
                orderApp.addCartItem(data.id, data.text, data.price);
                $(this).val(null).trigger('change'); // Reset search box
            }
        });
    },

    _initGrid: function() {
        this._table = $('#ordersGrid').DataTable({
            ajax: {
                url: '/api/sales/orders',
                dataSrc: function(json) { return json.data || json || []; }
            },
            columns: [
                { data: 'orderNumber', className: 'fw-bold font-monospace text-primary ps-4' },
                { data: 'orderDate', render: d => new Date(d).toLocaleDateString() },
                { data: 'customerName', className: 'fw-bold text-dark' },
                { data: 'salesRepName', className: 'text-muted', defaultContent: '-' },
                { 
                    data: 'totalAmount', 
                    className: 'text-end fw-bold',
                    render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 })
                },
                {
                    data: 'statusText',
                    className: 'text-center',
                    render: function(d) {
                        if (d === 'Draft') return '<span class="badge bg-warning text-dark"><i class="fa-solid fa-file-signature me-1"></i>Quotation</span>';
                        if (d === 'Submitted') return '<span class="badge bg-info text-dark"><i class="fa-solid fa-clock-rotate-left me-1"></i>Pending Review</span>';
                        if (d === 'Converted') return '<span class="badge bg-success"><i class="fa-solid fa-check-double me-1"></i>Converted to Invoice</span>';
                        if (d === 'Cancelled') return '<span class="badge bg-danger"><i class="fa-solid fa-ban me-1"></i>Cancelled</span>';
                        return `<span class="badge bg-secondary">${d}</span>`;
                    }
                },
                {
                    data: null,
                    className: 'text-end pe-4',
                    orderable: false,
                    render: function(data, type, row) {
                        // 1. Universal Viewer
                        let btns = `<button class="btn btn-sm btn-outline-dark shadow-sm me-1" onclick="window.orderApp.viewDocument(${row.id})" title="View Document"><i class="fa-solid fa-eye"></i></button>`;

                        // 2. State-Specific Actions (Enforcing strict enum mapping)
                        if (row.statusText === 'Draft') {
                            btns += `<button class="btn btn-sm btn-outline-primary shadow-sm me-1" onclick="window.orderApp.editOrder(${row.id})" title="Edit Quotation"><i class="fa-solid fa-pen"></i></button>`;
                            // Submit = Enum value 2
                            btns += `<button class="btn btn-sm btn-success shadow-sm me-1" onclick="window.orderApp.changeStatus(${row.id}, 2)" title="Submit to Back-Office"><i class="fa-solid fa-paper-plane"></i></button>`;
                        } else if (row.statusText === 'Submitted') {
                            // Revoke to Draft = Enum value 1
                            btns += `<button class="btn btn-sm btn-danger shadow-sm me-1" onclick="window.orderApp.changeStatus(${row.id}, 1)" title="Revoke to Draft"><i class="fa-solid fa-rotate-left"></i></button>`;
                        }

                        // 3. PDF Export
                        btns += `<a href="/api/sales/orders/${row.id}/pdf" target="_blank" class="btn btn-sm btn-outline-danger shadow-sm" title="Export PDF Quote"><i class="fa-solid fa-file-pdf"></i></a>`;
                        return btns;
                    }
                }
            ],
            order: [[0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    // --- NEW: Universal Document Viewer ---
    viewDocument: async function(id) {
        try {
            const doc = await api.get(`/api/sales/orders/${id}/document`);
                
            $('#docOrderNo').text(doc.orderNumber);
            $('#docDate').text(new Date(doc.orderDate).toLocaleDateString());
            $('#docCustomer').text(doc.customerName);
            $('#docRep').text(doc.salesRepName || 'N/A');
            $('#docNotes').text(doc.notes || '-');
            $('#docTotal').text(doc.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 }));

            // Status Badge Formatting (Strictly matching your SalesOrderStatus Enum)
            let badgeClass = 'bg-secondary';
            if (doc.statusText === 'Draft') badgeClass = 'bg-warning text-dark';
            if (doc.statusText === 'Submitted') badgeClass = 'bg-info text-dark';
            if (doc.statusText === 'Converted') badgeClass = 'bg-success';
            if (doc.statusText === 'Cancelled') badgeClass = 'bg-danger';
            
            $('#docStatusBadge').attr('class', `badge ${badgeClass}`).text(doc.statusText);

            let tbody = '';
            doc.items.forEach(i => {
                tbody += `
                    <tr>
                        <td class="fw-bold">${i.productDescription}</td>
                        <td class="text-center">${i.quantity}</td>
                        <td class="text-end">${i.unitPrice.toFixed(2)}</td>
                        <td class="text-end">${i.discount.toFixed(2)}</td>
                        <td class="text-end fw-bold">${i.lineTotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                    </tr>`;
            });
            $('#docItemsBody').html(tbody);
                
            new bootstrap.Modal(document.getElementById('viewerModal')).show();
        } catch (e) {
            toastr.error("Failed to load document.");
            console.error(e);
        }
    },

    // --- NEW: Edit Existing Draft ---
    editOrder: async function(id) {
        try {
            const doc = await api.get(`/api/sales/orders/${id}/document`);
            
            // 1. Reset and Open Modal
            this.openCreateModal(); 
            
            // 2. Populate Header
            // Assuming your backend supports updates, you would normally bind the ID. 
            // For now, we populate the fields so the user can re-submit or save over it.
            $('#OrderDate').val(doc.orderDate.split('T')[0]);
            $('#CustomerId').val(doc.customerId).trigger('change');
            if (doc.salesRepId) $('#SalesRepId').val(doc.salesRepId);
            $('#Notes').val(doc.notes);

            // 3. Populate Cart
            doc.items.forEach(i => {
                this.addCartItem(i.productVariantId, i.productDescription, i.unitPrice);
                
                // Target the freshly added row and override Qty and Discount
                let newRow = $(`#cartBody tr[data-vid="${i.productVariantId}"]`);
                newRow.find('.line-qty').val(i.quantity);
                newRow.find('.line-discount').val(i.discount);
            });
            
            this.calculateTotals();
        } catch (e) {
            toastr.error("Failed to load order for editing.");
        }
    },

    // --- NEW: State Machine (Revoke / Submit) ---
    changeStatus: async function(id, newStatus) {
        // Determine dynamic UI elements based on the state transition
        const isSubmit = newStatus === 2;
        const actionText = isSubmit ? 'Submit this order to the Back-Office' : 'Revoke this order back to Draft';
        const confirmBtnText = isSubmit ? 'Yes, Submit Order' : 'Yes, Revoke Order';
        const confirmBtnColor = isSubmit ? '#198754' : '#dc3545'; // Bootstrap Success vs Danger hex codes

        // Trigger SweetAlert2 instead of browser confirm
        const result = await Swal.fire({
            title: 'Are you sure?',
            text: actionText,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: confirmBtnColor,
            cancelButtonColor: '#6c757d', // Bootstrap Secondary
            confirmButtonText: confirmBtnText,
            reverseButtons: true // Puts the cancel button on the left (Enterprise standard)
        });

        // Exit if the user clicked cancel or clicked outside the modal
        if (!result.isConfirmed) return;

        // Execute the State Change
        try {
            const res = await api.post(`/api/sales/orders/${id}/change-status`, newStatus);
            
            if (res && res.succeeded) {
                toastr.success(res.message);
                this._table.ajax.reload(null, false);
            } else if (res && res.messages) {
                toastr.error(res.messages[0]);
            }
        } catch (e) {
            toastr.error(e.responseJSON?.messages?.[0] || "Failed to change order status.");
            console.error(e);
        }
    },

    _loadLookups: async function () {
        try {
            const [custRes, empRes, prodRes] = await Promise.all([
                api.get('/api/customer'),
                api.get('/api/employee'),
                api.get('/api/product')
            ]);

            const customers = Array.isArray(custRes) ? custRes : (custRes?.data || []);
            const employees = Array.isArray(empRes) ? empRes : (empRes?.data || []);
            const products = Array.isArray(prodRes) ? prodRes : (prodRes?.data || []);

            // 1. Populate Customers
            let $cust = $('#CustomerId').empty().append('<option value="">-- Select Customer --</option>');
            customers.forEach(c => $cust.append($('<option></option>').val(c.id).text(c.name)));

            // 2. Populate Reps
            let $emp = $('#SalesRepId').empty().append('<option value="">-- No Rep Assigned --</option>');
            employees.filter(e => e.isSalesRep).forEach(e => {
                $emp.append($('<option></option>').val(e.id).text(`[${e.employeeCode}] ${e.firstName} ${e.lastName}`));
            });

            // 3. Populate Product Search (POS Catalog)
            let productData = [];
            products.forEach(p => {
                if (p.variants) {
                    p.variants.forEach(v => {
                        let desc = `[${v.sku}] ${p.name}`;
                        if (v.size || v.color) desc += ` - ${v.size || ''} ${v.color || ''}`;
                        productData.push({ id: v.id, text: desc, price: v.sellingPrice });
                    });
                }
            });

            $('#ProductSearch').select2({
                data: productData,
                dropdownParent: $('#orderModal')
            });

            $('#CustomerId').select2({ dropdownParent: $('#orderModal') });

        } catch (e) {
            console.error("Lookup Error:", e);
        }
    },

    openCreateModal: function () {
        $('#orderForm')[0].reset();
        $('#cartBody').empty();
        $('#CustomerId').val('').trigger('change');
        this.calculateTotals();
        this._modal.show();
    },

    addCartItem: function (variantId, desc, price) {
        // Prevent duplicate rows, just increase qty
        let existingRow = $(`#cartBody tr[data-vid="${variantId}"]`);
        if (existingRow.length > 0) {
            let qtyInput = existingRow.find('.line-qty');
            qtyInput.val(parseInt(qtyInput.val()) + 1);
            this.calculateTotals();
            return;
        }

        const id = Date.now();
        const html = `
            <tr id="row_${id}" data-vid="${variantId}">
                <td class="fw-bold text-dark align-middle">${desc}</td>
                <td><input type="number" class="form-control form-control-sm text-center line-qty calc-trigger fw-bold" value="1" min="1"></td>
                <td><input type="number" class="form-control form-control-sm text-end line-price calc-trigger" value="${price.toFixed(2)}" step="0.01"></td>
                <td><input type="number" class="form-control form-control-sm text-end line-discount calc-trigger" value="0.00" step="0.01"></td>
                <td class="text-end fw-bold align-middle text-primary fs-14 line-total">0.00</td>
                <td class="text-center align-middle">
                    <button type="button" class="btn btn-sm text-danger" onclick="$('#row_${id}').remove(); window.orderApp.calculateTotals();">
                        <i class="fa-solid fa-times fs-5"></i>
                    </button>
                </td>
            </tr>`;
        $('#cartBody').prepend(html);
        this.calculateTotals();
    },

    calculateTotals: function () {
        let total = 0;
        $('#cartBody tr').each(function () {
            const qty = parseFloat($(this).find('.line-qty').val()) || 0;
            const price = parseFloat($(this).find('.line-price').val()) || 0;
            let discount = parseFloat($(this).find('.line-discount').val()) || 0;

            let lineGross = qty * price;
            if (discount > lineGross) discount = lineGross; // Guard

            let lineNet = lineGross - discount;
            $(this).find('.line-total').text(lineNet.toLocaleString(undefined, { minimumFractionDigits: 2 }));
            total += lineNet;
        });

        $('#lblGrandTotal').text(total.toLocaleString(undefined, { minimumFractionDigits: 2 }));
    },

    save: async function (isDraft) {
        var form = $('#orderForm')[0];
        if (!form.checkValidity() || parseInt($('#CustomerId').val()) === 0) {
            toastr.warning("Please fill out all required fields, including Customer.");
            return;
        }

        const payload = {
            Order: {
                OrderDate: $('#OrderDate').val(),
                CustomerId: parseInt($('#CustomerId').val()),
                SalesRepId: parseInt($('#SalesRepId').val()) || null,
                Notes: $('#Notes').val(),
                IsDraft: isDraft,
                Items: []
            }
        };

        $('#cartBody tr').each(function () {
            payload.Order.Items.push({
                ProductVariantId: parseInt($(this).data('vid')),
                Quantity: parseFloat($(this).find('.line-qty').val()) || 0,
                UnitPrice: parseFloat($(this).find('.line-price').val()) || 0,
                Discount: parseFloat($(this).find('.line-discount').val()) || 0
            });
        });

        if (payload.Order.Items.length === 0) {
            toastr.warning("Please add at least one product to the order.");
            return;
        }

        var $btn = $(event.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm"></i> Submitting...');

        try {
            const res = await api.post('/api/sales/orders', payload);
            if (res && res.succeeded) {
                toastr.success(res.message);
                this._modal.hide();
                this._table.ajax.reload(null, false);
            }
        } catch(e) {
            console.error(e);
        } finally {
            $btn.prop('disabled', false).html(ogText);
        }
    }
};

$(document).ready(() => orderApp.init());