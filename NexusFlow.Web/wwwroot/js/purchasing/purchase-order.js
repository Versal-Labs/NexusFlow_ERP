window.poApp = {
    _table: null,
    _modal: null,
    _viewModal: null,
    _products: [], // Only Raw Materials will be stored here
    _suppliers: [],

    init: function () {
        var modalEl = document.getElementById('poModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static', keyboard: false });

        var viewEl = document.getElementById('poViewModal');
        if (viewEl) this._viewModal = new bootstrap.Modal(viewEl);

        document.getElementById('Date').valueAsDate = new Date();

        this._initFilters();
        this._initGrid();
        this._loadMasterData();

        $('#frmPO').on('input change', '.calc-trigger', () => this.calculateTotals());
    },

    _initFilters: function () {
        api.get('/api/Supplier').then(res => {
            let suppliers = Array.isArray(res) ? res : (res?.data || []);
            let $sup = $('#filterSupplier').empty().append('<option value="">All Suppliers</option>');
            suppliers.forEach(s => $sup.append($('<option></option>').val(s.name).text(s.name)));
        }).catch(err => console.error(err));

        $('#filterSupplier, #filterStatus, #filterStartDate, #filterEndDate').on('change', () => this.reloadGrid());

        $.fn.dataTable.ext.search.push((settings, data, dataIndex) => {
            if (settings.nTable.id !== 'poGrid') return true;

            const filterSup = ($('#filterSupplier').val() || '').toLowerCase();
            const filterStatus = $('#filterStatus').val();
            const filterStart = $('#filterStartDate').val();
            const filterEnd = $('#filterEndDate').val();

            const rowDateStr = data[1] || '';
            const rowSup = (data[2] || '').toLowerCase();
            const rowStatusBadge = data[3] || ''; // Contains the raw HTML of the badge

            if (filterSup && !rowSup.includes(filterSup)) return false;

            // Map Enum values to badge text
            if (filterStatus) {
                const statusMap = { '1': 'Draft', '2': 'Approved', '6': 'Partial', '3': 'Received', '4': 'Closed', '5': 'Cancelled' };
                if (!rowStatusBadge.includes(statusMap[filterStatus])) return false;
            }

            if (filterStart || filterEnd) {
                if (!rowDateStr) return false;
                const rowDate = new Date(rowDateStr);
                if (filterStart && rowDate < new Date(filterStart)) return false;
                if (filterEnd && rowDate > new Date(filterEnd + 'T23:59:59')) return false;
            }
            return true;
        });
    },

    resetFilters: function () {
        $('#filterSupplier').val(''); $('#filterStatus').val('');
        $('#filterStartDate').val(''); $('#filterEndDate').val('');
        this.reloadGrid();
    },

    reloadGrid: function () { this._table.ajax.reload(); },

    _initGrid: function () {
        this._table = $('#poGrid').DataTable({
            ajax: function (data, callback, settings) {
                (async () => {
                    try {
                        const response = await api.get('/api/purchasing/purchase-orders');
                        callback({ data: response.data || response || [] });
                    } catch (e) { callback({ data: [] }); }
                })();
                return { abort: function () { } };
            },
            columns: [
                { data: "poNumber", className: "font-monospace fw-bold text-primary" },
                { data: "date", render: d => d ? new Date(d).toLocaleDateString() : '-' },
                { data: "supplierName", className: "fw-bold" },
                {
                    data: "status", className: "text-center",
                    render: function (d) {
                        if (d === 'Draft') return '<span class="badge bg-secondary">Draft</span>';
                        if (d === 'Approved') return '<span class="badge bg-primary">Approved</span>';
                        if (d === 'Partial') return '<span class="badge bg-info text-dark">Partial Receipt</span>';
                        if (d === 'Received') return '<span class="badge bg-success">Fully Received</span>';
                        if (d === 'Closed') return '<span class="badge bg-dark">Closed</span>';
                        return `<span class="badge bg-danger">${d}</span>`;
                    }
                },
                { data: "totalAmount", className: "text-end fw-bold", render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }) },
                {
                    data: null, className: "text-end pe-3", orderable: false,
                    render: function (data, type, row) {
                        let btns = `<button class="btn btn-sm btn-outline-dark shadow-sm me-1" onclick="poApp.viewDocument(${row.id})" title="View Document"><i class="fa-solid fa-eye"></i></button>`;

                        // TIER-1 WORKFLOW: Allow Editing if it is a Draft!
                        if (row.status === 'Draft') {
                            btns += `<button class="btn btn-sm btn-outline-primary shadow-sm me-1" onclick="poApp.edit(${row.id})" title="Edit Draft"><i class="fa-solid fa-pen-to-square"></i></button>`;
                        }

                        // Allow receiving if Approved or Partial
                        if (row.status === 'Approved' || row.status === 'Partial') {
                            btns += `<a href="/Purchasing/GRN?poId=${row.id}" class="btn btn-sm btn-success shadow-sm" title="Receive Stock">
                                        <i class="fa-solid fa-truck-ramp-box"></i> Receive
                                     </a>`;
                        }
                        return btns;
                    }
                }
            ],
            order: [[0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 20
        });
    },

    _loadMasterData: async function () {
        try {
            const [supRes, prodRes] = await Promise.all([
                api.get('/api/Supplier'), api.get('/api/Product')
            ]);

            this._suppliers = Array.isArray(supRes) ? supRes : (supRes?.data || []);
            const allProducts = Array.isArray(prodRes) ? prodRes : (prodRes?.data || []);

            // TIER-1 FEATURE: Filter strictly to Raw Materials (ProductType = 1)
            this._products = allProducts.filter(p => p.type === 1 || p.type === 'RawMaterial');

            let $sup = $('#SupplierId').empty().append('<option value="">-- Select Supplier --</option>');
            this._suppliers.forEach(s => $sup.append($('<option></option>').val(s.id).text(s.name)));

            if ($('#SupplierId').hasClass('select2-hidden-accessible')) $('#SupplierId').select2('destroy');
            $('#SupplierId').select2({ dropdownParent: $('#poModal') });

        } catch (e) {
            console.error("[PO App] Master data error", e);
        }
    },

    openCreateModal: function () {
        $('#frmPO')[0].reset();
        $('#Id').val(0); // Reset ID to 0 for New PO
        $('#poItemsBody').empty();
        $('#SupplierId').val('').trigger('change');
        this.addLine();
        this.calculateTotals();
        this._modal.show();
    },

    addLine: function () {
        const id = Date.now();
        let productOptions = '<option value="">-- Search Raw Material --</option>';

        this._products.forEach(p => {
            if (p.variants && p.variants.length > 0) {
                productOptions += `<optgroup label="${p.name}">`;
                p.variants.forEach(v => {
                    let desc = v.sku;
                    if (v.size !== 'N/A' || v.color !== 'N/A') desc += ` (${v.size !== 'N/A' ? v.size : ''} ${v.color !== 'N/A' ? v.color : ''})`;

                    // Injecting the UoM symbol into the data attribute so JS can extract it!
                    const uom = p.unitOfMeasure?.symbol || 'u';
                    productOptions += `<option value="${v.id}" data-cost="${v.costPrice}" data-uom="${uom}">${desc}</option>`;
                });
                productOptions += `</optgroup>`;
            }
        });

        const html = `
            <tr id="row_${id}">
                <td>
                    <select class="form-select form-select-sm line-variant" style="width: 100%;">
                        ${productOptions}
                    </select>
                </td>
                <td>
                    <div class="input-group input-group-sm">
                        <input type="number" class="form-control text-center line-qty calc-trigger" value="1" min="1" step="1" onclick="this.select()">
                        <span class="input-group-text fw-bold text-muted line-uom" style="width:50px; justify-content:center;">-</span>
                    </div>
                </td>
                <td><input type="number" class="form-control form-control-sm text-end line-cost calc-trigger" value="0.00" step="0.01" onclick="this.select()"></td>
                <td class="text-end fw-bold align-middle line-total">0.00</td>
                <td class="text-center align-middle">
                    <button type="button" class="btn btn-sm text-danger" onclick="$('#row_${id}').remove(); poApp.calculateTotals();">
                        <i class="fa-solid fa-trash-can"></i>
                    </button>
                </td>
            </tr>`;

        const newRow = $(html);
        $('#poItemsBody').append(newRow);

        newRow.find('.line-variant').select2({
            dropdownParent: $('#poModal'), width: '100%'
        }).on('change', function () {
            poApp.onVariantSelect(this);
        });
    },

    onVariantSelect: function (selectEl) {
        const selectedOption = $(selectEl).find('option:selected');
        const cost = selectedOption.data('cost') || 0;
        const uom = selectedOption.data('uom') || '-';

        const row = $(selectEl).closest('tr');
        row.find('.line-cost').val(parseFloat(cost).toFixed(2));
        row.find('.line-uom').text(uom);

        // TIER-1 FEATURE: Smart UoM Validation
        const qtyInput = row.find('.line-qty');
        if (uom.toLowerCase() === 'pcs' || uom.toLowerCase() === 'doz' || uom.toLowerCase() === 'box') {
            qtyInput.attr('step', '1');
            qtyInput.val(Math.round(qtyInput.val() || 1)); // Force whole number
        } else {
            qtyInput.attr('step', '0.01'); // Allow decimals for m, kg, ltr
        }

        this.calculateTotals();
    },

    calculateTotals: function () {
        let grandTotal = 0;
        $('#poItemsBody tr').each(function () {
            const qty = parseFloat($(this).find('.line-qty').val()) || 0;
            const cost = parseFloat($(this).find('.line-cost').val()) || 0;
            let lineTotal = qty * cost;
            $(this).find('.line-total').text(lineTotal.toFixed(2));
            grandTotal += lineTotal;
        });
        $('#lblGrandTotal').text(grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2 }));
    },

    edit: async function (id) {
        try {
            const res = await api.get(`/api/purchasing/purchase-orders/${id}`);
            const po = res.data || res;

            $('#frmPO')[0].reset();
            $('#poItemsBody').empty();

            $('#Id').val(po.id);
            $('#SupplierId').val(po.supplierId).trigger('change');

            if (po.date) $('#Date').val(po.date.split('T')[0]);
            if (po.expectedDate) $('#ExpectedDate').val(po.expectedDate.split('T')[0]);
            $('#Note').val(po.note);

            // Rebuild the dynamic grid lines
            if (po.items && po.items.length > 0) {
                po.items.forEach(item => {
                    this._addExistingLine(item);
                });
            } else {
                this.addLine(); // Fallback if empty
            }

            this.calculateTotals();
            this._modal.show();
        } catch (e) {
            toastr.error("Could not load PO for editing.");
        }
    },

    // Helper to rebuild existing lines with Select2 and UOM logic
    _addExistingLine: function (item) {
        const id = Date.now() + Math.floor(Math.random() * 1000);
        let productOptions = '<option value="">-- Search Raw Material --</option>';
        let uomSymbol = '-';

        this._products.forEach(p => {
            if (p.variants && p.variants.length > 0) {
                productOptions += `<optgroup label="${p.name}">`;
                p.variants.forEach(v => {
                    let desc = v.sku;
                    if (v.size !== 'N/A' || v.color !== 'N/A') desc += ` (${v.size !== 'N/A' ? v.size : ''} ${v.color !== 'N/A' ? v.color : ''})`;
                    const uom = p.unitOfMeasure?.symbol || 'u';

                    // Mark as selected if it matches the saved item
                    let isSelected = (v.id === item.productVariantId) ? 'selected' : '';
                    if (isSelected) uomSymbol = uom;

                    productOptions += `<option value="${v.id}" data-cost="${v.costPrice}" data-uom="${uom}" ${isSelected}>${desc}</option>`;
                });
                productOptions += `</optgroup>`;
            }
        });

        let step = (uomSymbol.toLowerCase() === 'pcs' || uomSymbol.toLowerCase() === 'doz' || uomSymbol.toLowerCase() === 'box') ? '1' : '0.01';

        const html = `
            <tr id="row_${id}">
                <td>
                    <select class="form-select form-select-sm line-variant" style="width: 100%;">
                        ${productOptions}
                    </select>
                </td>
                <td>
                    <div class="input-group input-group-sm">
                        <input type="number" class="form-control text-center line-qty calc-trigger" value="${item.quantityOrdered}" min="0.01" step="${step}" onclick="this.select()">
                        <span class="input-group-text fw-bold text-muted line-uom" style="width:50px; justify-content:center;">${uomSymbol}</span>
                    </div>
                </td>
                <td><input type="number" class="form-control form-control-sm text-end line-cost calc-trigger" value="${item.unitCost.toFixed(2)}" step="0.01" onclick="this.select()"></td>
                <td class="text-end fw-bold align-middle line-total">0.00</td>
                <td class="text-center align-middle">
                    <button type="button" class="btn btn-sm text-danger" onclick="$('#row_${id}').remove(); poApp.calculateTotals();">
                        <i class="fa-solid fa-trash-can"></i>
                    </button>
                </td>
            </tr>`;

        const newRow = $(html);
        $('#poItemsBody').append(newRow);

        newRow.find('.line-variant').select2({
            dropdownParent: $('#poModal'), width: '100%'
        }).on('change', function () {
            poApp.onVariantSelect(this);
        });
    },

    savePO: async function (isDraft) {
        var form = document.getElementById('frmPO');
        var poId = parseInt($('#Id').val()) || 0; // TIER-1 FIX: Grab ID

        var dateVal = $('#Date').val();
        var expDateVal = $('#ExpectedDate').val();
        if (expDateVal && new Date(expDateVal) < new Date(dateVal)) {
            toastr.warning("Expected Delivery Date cannot be earlier than the Order Date.");
            return;
        }

        var supplierId = parseInt($('#SupplierId').val()) || 0;
        if (supplierId === 0) { toastr.warning("Select a Supplier."); return; }
        if (!form.checkValidity()) { form.reportValidity(); return; }

        const payload = {
            Id: poId, // Add ID to payload
            SupplierId: supplierId,
            Date: dateVal,
            ExpectedDate: expDateVal,
            Note: $('#Note').val(),
            IsDraft: isDraft,
            Items: []
        };

        $('#poItemsBody tr').each(function () {
            const varId = $(this).find('.line-variant').val();
            if (varId) {
                payload.Items.push({
                    ProductVariantId: parseInt(varId),
                    QuantityOrdered: parseFloat($(this).find('.line-qty').val()) || 0,
                    UnitCost: parseFloat($(this).find('.line-cost').val()) || 0
                });
            }
        });

        if (payload.Items.length === 0) { toastr.warning("Add at least one raw material."); return; }

        var $btn = $(event.currentTarget);
        var originalText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Processing...');

        try {
            // TIER-1 FIX: Smart Routing (POST for New, PUT for Edit)
            const res = poId === 0
                ? await api.post('/api/purchasing/purchase-orders', payload)
                : await api.put(`/api/purchasing/purchase-orders/${poId}`, payload);

            if (res && res.succeeded) {
                toastr.success(res.message || "Purchase Order saved.");
                this._modal.hide();
                this._table.ajax.reload(null, false);
            } else {
                toastr.error(res?.messages?.[0] || "Failed to save PO.");
            }
        } catch (e) { console.error(e); }
        finally { $btn.prop('disabled', false).html(originalText); }
    },

    viewDocument: async function (id) {
        try {
            const res = await api.get(`/api/purchasing/purchase-orders/${id}`);
            const doc = res.data || res;

            $('#docPoNo').text(doc.poNumber);
            $('#docDate').text(new Date(doc.date).toLocaleDateString());

            if (doc.expectedDate) $('#docExpectedDate').text(new Date(doc.expectedDate).toLocaleDateString());
            else $('#docExpectedDate').text('TBA');

            $('#docSupplier').text(doc.supplierName);
            $('#docNotes').text(doc.note || '-');
            $('#docTotal, #docTotalLarge').text(parseFloat(doc.totalAmount || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }));

            // Map Status to colored badge inside the viewer
            const sMap = { 'Draft': 'bg-secondary', 'Approved': 'bg-primary', 'Partial': 'bg-info text-dark', 'Received': 'bg-success', 'Closed': 'bg-dark', 'Cancelled': 'bg-danger' };
            $('#docStatusBadge').removeClass().addClass(`badge ${sMap[doc.status] || 'bg-secondary'}`).text(doc.status);

            let tbody = '';
            if (doc.items) {
                doc.items.forEach(i => {
                    let qtyOrdered = parseFloat(i.quantityOrdered || 0);
                    let qtyReceived = parseFloat(i.quantityReceived || 0);
                    let cost = parseFloat(i.unitCost || 0);
                    let lineGross = qtyOrdered * cost;

                    let recHtml = qtyReceived >= qtyOrdered ? `<span class="text-success fw-bold">${qtyReceived}</span>` : qtyReceived;

                    tbody += `
                        <tr>
                            <td class="fw-bold">${i.productName}</td>
                            <td class="text-center font-monospace">${qtyOrdered}</td>
                            <td class="text-center">${recHtml}</td>
                            <td class="text-end">${cost.toFixed(2)}</td>
                            <td class="text-end fw-bold">${lineGross.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                        </tr>`;
                });
            }
            $('#docItemsBody').html(tbody);
            this._viewModal.show();
        } catch (e) {
            toastr.error("Failed to load PO document.");
        }
    }
};

$(document).ready(function () { window.poApp.init(); });