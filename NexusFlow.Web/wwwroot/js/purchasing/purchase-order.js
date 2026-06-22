window.poApp = {
    _table: null,
    _modal: null,
    _viewModal: null,
    _currentDocId: null,
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
        $('#PoProductTypeFilter').on('change', () => {
            const selectedType = $('#PoProductTypeFilter').val();
            const emptyRows = $('#poItemsBody tr').filter(function () {
                return !$(this).find('.line-variant').val();
            });

            if (emptyRows.length) {
                const row = emptyRows.last();
                const select = row.find('.line-variant');
                if (select.data('select2')) select.select2('destroy');
                row.remove();
            }

            this.addLine(selectedType);
            this.calculateTotals();
        });
    },

    _initFilters: function () {
        api.get('/api/Supplier').then(res => {
            let suppliers = Array.isArray(res) ? res : (res?.data || []);
            let $sup = $('#filterSupplier').empty().append('<option value="">All Suppliers</option>');
            suppliers.forEach(s => $sup.append($('<option></option>').val(s.name).text(s.name)));
        }).catch(err => console.error(err));

        $('#filterSupplier, #filterStatus, #filterStartDate, #filterEndDate').on('change', () => this.reloadGrid());

        $.fn.dataTable.ext.search.push((settings, data) => {
            if (settings.nTable.id !== 'poGrid') return true;

            const filterSup = ($('#filterSupplier').val() || '').toLowerCase();
            const filterStatus = $('#filterStatus').val();
            const filterStart = $('#filterStartDate').val();
            const filterEnd = $('#filterEndDate').val();

            const rowDateStr = data[1] || '';
            const rowSup = (data[2] || '').toLowerCase();
            const rowStatusBadge = data[3] || '';

            if (filterSup && !rowSup.includes(filterSup)) return false;

            if (filterStatus) {
                const statusMap = { '1': 'Draft', '2': 'Approved', '6': 'Partial Receipt', '3': 'Received', '4': 'Closed', '5': 'Cancelled' };
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
        $('#filterSupplier').val('');
        $('#filterStatus').val('');
        $('#filterStartDate').val('');
        $('#filterEndDate').val('');
        this.reloadGrid();
    },

    reloadGrid: function () { this._table.ajax.reload(); },

    _initGrid: function () {
        this._table = $('#poGrid').DataTable({
            ajax: function (data, callback) {
                (async () => {
                    try {
                        const response = await api.get('/api/purchasing/purchase-orders');
                        callback({ data: response.data || response || [] });
                    } catch (e) {
                        callback({ data: [] });
                    }
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
                        btns += `<button class="btn btn-sm btn-outline-secondary shadow-sm me-1" onclick="NexusPrint.printDocument('PurchaseOrder', ${row.id})" title="Print"><i class="fa-solid fa-print"></i></button>`;
                        btns += `<button class="btn btn-sm btn-outline-danger shadow-sm me-1" onclick="NexusPrint.downloadDocument('PurchaseOrder', ${row.id})" title="Download PDF"><i class="fa-solid fa-file-pdf"></i></button>`;

                        if (row.status === 'Draft') {
                            btns += `<button class="btn btn-sm btn-outline-primary shadow-sm me-1" onclick="poApp.edit(${row.id})" title="Edit Draft"><i class="fa-solid fa-pen-to-square"></i></button>`;
                        }

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
            const supRes = await api.get('/api/Supplier');
            this._suppliers = Array.isArray(supRes) ? supRes : (supRes?.data || []);

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
        $('#Id').val(0);
        $('#PoProductTypeFilter').val('1');
        $('#poItemsBody').empty();
        $('#SupplierId').val('').trigger('change');
        this.addLine();
        this.calculateTotals();
        this._modal.show();
    },

    addLine: function (productType) {
        const lineProductType = productType || $('#PoProductTypeFilter').val() || '1';
        const id = Date.now() + Math.floor(Math.random() * 1000);
        const html = `
            <tr id="row_${id}" data-product-type="${lineProductType}">
                <td>
                    <select class="form-select form-select-sm line-variant" style="width: 100%;"></select>
                    <div class="small text-muted mt-1 line-product-type">${this._getProductTypeLabel(lineProductType)}</div>
                </td>
                <td>
                    <div class="input-group input-group-sm">
                        <input type="number" class="form-control text-center line-qty calc-trigger" value="1" min="1" step="1" onclick="this.select()">
                        <span class="input-group-text fw-bold text-muted line-uom" style="width:50px; justify-content:center;">-</span>
                    </div>
                </td>
                <td><input type="number" class="form-control form-control-sm text-end line-cost calc-trigger" value="0.00" min="0.01" step="0.01" onclick="this.select()"></td>
                <td class="text-end fw-bold align-middle line-total">0.00</td>
                <td class="text-center align-middle">
                    <button type="button" class="btn btn-sm text-danger" onclick="poApp.removeLine(${id})">
                        <i class="fa-solid fa-trash-can"></i>
                    </button>
                </td>
            </tr>`;

        const newRow = $(html);
        $('#poItemsBody').append(newRow);
        this._initVariantSelect(newRow.find('.line-variant'), null, lineProductType);
    },

    removeLine: function (id) {
        const $row = $(`#row_${id}`);
        const $select = $row.find('.line-variant');
        if ($select.data('select2')) $select.select2('destroy');
        $row.remove();
        this.calculateTotals();
    },

    _initVariantSelect: function ($select, selectedItem, productType) {
        const lineProductType = productType || $select.closest('tr').attr('data-product-type') || 'stocked';

        if (selectedItem) {
            const selectedText = `${selectedItem.sku} - ${selectedItem.productName}`;
            const option = new Option(selectedText, selectedItem.productVariantId, true, true);
            $(option)
                .attr('data-cost', selectedItem.unitCost)
                .attr('data-uom', selectedItem.uomSymbol || '-');
            $select.append(option);
        }

        $select.select2({
            dropdownParent: $('#poModal'),
            width: '100%',
            placeholder: this._getProductPlaceholder(lineProductType),
            minimumInputLength: 0,
            ajax: {
                url: '/api/masterdata/variants/search',
                dataType: 'json',
                delay: 250,
                headers: {
                    'Authorization': `Bearer ${localStorage.getItem('nexus_token')}`,
                    'RequestVerificationToken': api.getToken()
                },
                data: params => this._buildVariantSearchParams(params.term || '', lineProductType),
                processResults: data => ({
                    results: $.map(data || [], item => ({
                        id: item.id,
                        text: `${item.sku} - ${item.name}`,
                        costPrice: item.costPrice || 0,
                        uomSymbol: item.uomSymbol || '-'
                    }))
                })
            }
        }).on('select2:select change', function () {
            poApp.onVariantSelect(this);
        });

        if (selectedItem) this.onVariantSelect($select[0]);
    },

    _buildVariantSearchParams: function (query, productType) {
        const params = {
            query: query,
            stockedOnly: true
        };

        if (productType && productType !== 'stocked') {
            params.productType = productType;
        }

        return params;
    },

    _getProductPlaceholder: function (productType) {
        if (productType === '1') return 'Search Raw Material';
        if (productType === '2') return 'Search Finished Good';
        if (productType === '4') return 'Search Work In Progress';
        return 'Search Stocked Product';
    },

    _getProductTypeLabel: function (productType) {
        if (productType === '1') return 'Raw Material';
        if (productType === '2') return 'Finished Good';
        if (productType === '4') return 'Work In Progress';
        return 'All Stocked Products';
    },

    _isWholeNumberUom: function (uom) {
        const normalized = (uom || '').toLowerCase();
        return normalized === 'pcs' || normalized === 'pc' || normalized === 'doz' || normalized === 'box';
    },

    onVariantSelect: function (selectEl) {
        const selectedData = ($(selectEl).select2('data') || [])[0] || {};
        const selectedOption = $(selectEl).find('option:selected');
        const cost = selectedData.costPrice ?? selectedOption.data('cost') ?? 0;
        const uom = selectedData.uomSymbol || selectedOption.data('uom') || '-';

        const row = $(selectEl).closest('tr');
        row.find('.line-cost').val(parseFloat(cost || 0).toFixed(2));
        row.find('.line-uom').text(uom);

        const qtyInput = row.find('.line-qty');
        if (this._isWholeNumberUom(uom)) {
            qtyInput.attr('step', '1').attr('min', '1');
            qtyInput.val(Math.max(1, Math.round(parseFloat(qtyInput.val()) || 1)));
        } else {
            qtyInput.attr('step', '0.01').attr('min', '0.01');
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
            $('#PoProductTypeFilter').val('stocked');

            $('#Id').val(po.id);
            $('#SupplierId').val(po.supplierId).trigger('change');

            if (po.date) $('#Date').val(po.date.split('T')[0]);
            if (po.expectedDate) $('#ExpectedDate').val(po.expectedDate.split('T')[0]);
            $('#Note').val(po.note);

            if (po.items && po.items.length > 0) {
                po.items.forEach(item => this._addExistingLine(item));
            } else {
                this.addLine();
            }

            this.calculateTotals();
            this._modal.show();
        } catch (e) {
            toastr.error("Could not load PO for editing.");
        }
    },

    _addExistingLine: function (item) {
        const lineProductType = item.productType ? String(item.productType) : 'stocked';
        const id = Date.now() + Math.floor(Math.random() * 1000);
        const html = `
            <tr id="row_${id}" data-product-type="${lineProductType}">
                <td>
                    <select class="form-select form-select-sm line-variant" style="width: 100%;"></select>
                    <div class="small text-muted mt-1 line-product-type">${this._getProductTypeLabel(lineProductType)}</div>
                </td>
                <td>
                    <div class="input-group input-group-sm">
                        <input type="number" class="form-control text-center line-qty calc-trigger" value="${item.quantityOrdered}" min="0.01" step="0.01" onclick="this.select()">
                        <span class="input-group-text fw-bold text-muted line-uom" style="width:50px; justify-content:center;">${item.uomSymbol || '-'}</span>
                    </div>
                </td>
                <td><input type="number" class="form-control form-control-sm text-end line-cost calc-trigger" value="${parseFloat(item.unitCost || 0).toFixed(2)}" min="0.01" step="0.01" onclick="this.select()"></td>
                <td class="text-end fw-bold align-middle line-total">0.00</td>
                <td class="text-center align-middle">
                    <button type="button" class="btn btn-sm text-danger" onclick="poApp.removeLine(${id})">
                        <i class="fa-solid fa-trash-can"></i>
                    </button>
                </td>
            </tr>`;

        const newRow = $(html);
        $('#poItemsBody').append(newRow);
        this._initVariantSelect(newRow.find('.line-variant'), item, lineProductType);
    },

    savePO: async function (isDraft) {
        var form = document.getElementById('frmPO');
        var poId = parseInt($('#Id').val()) || 0;

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
            Id: poId,
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

        if (payload.Items.length === 0) { toastr.warning("Add at least one item."); return; }

        var $btn = $(event.currentTarget);
        var originalText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Processing...');

        try {
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
        } catch (e) {
            console.error(e);
        } finally {
            $btn.prop('disabled', false).html(originalText);
        }
    },

    viewDocument: async function (id) {
        try {
            this._currentDocId = id;
            const res = await api.get(`/api/purchasing/purchase-orders/${id}`);
            const doc = res.data || res;

            $('#docPoNo').text(doc.poNumber);
            $('#docDate').text(new Date(doc.date).toLocaleDateString());

            if (doc.expectedDate) $('#docExpectedDate').text(new Date(doc.expectedDate).toLocaleDateString());
            else $('#docExpectedDate').text('TBA');

            $('#docSupplier').text(doc.supplierName);
            $('#docNotes').text(doc.note || '-');
            $('#docTotal, #docTotalLarge').text(parseFloat(doc.totalAmount || 0).toLocaleString(undefined, { minimumFractionDigits: 2 }));

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
