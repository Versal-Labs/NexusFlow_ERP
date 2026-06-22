window.openingStockApp = {
    _table: null,
    _modal: null,
    _lineNo: 0,

    init: function () {
        const modalEl = document.getElementById('openingStockModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });

        this._initGrid();
        this._loadWarehouses();

        $('#openingWarehouseId').on('change', () => {
            $('#openingLinesBody').empty();
            if ($('#openingWarehouseId').val()) this.addLine();
        });
    },

    _initGrid: function () {
        this._table = $('#openingStockGrid').DataTable({
            ajax: async function (data, callback) {
                try {
                    const res = await api.get('/api/inventory/opening-stock');
                    callback({ data: res.data || res || [] });
                } catch (e) {
                    callback({ data: [] });
                }
            },
            columns: [
                { data: 'referenceNo', className: 'fw-bold text-success font-monospace ps-3' },
                { data: 'date', render: d => d ? new Date(d).toLocaleDateString() : '-' },
                { data: 'warehouse', className: 'fw-bold text-dark' },
                { data: 'notes', className: 'text-muted fst-italic', defaultContent: '' },
                { data: 'itemsAffected', className: 'text-center' },
                {
                    data: 'totalValue',
                    className: 'text-end fw-bold pe-3',
                    render: d => parseFloat(d || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
                }
            ],
            order: [[1, 'desc'], [0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    _loadWarehouses: async function () {
        try {
            const res = await api.get('/api/masterdata/warehouses');
            const warehouses = res.data || res || [];
            const $warehouse = $('#openingWarehouseId').empty().append('<option value="">-- Select Warehouse --</option>');

            warehouses.forEach(w => {
                $warehouse.append($('<option></option>').val(w.id).text(w.name));
            });
        } catch (e) {
            console.error("Failed to load warehouses.", e);
        }
    },

    openCreateModal: function () {
        $('#openingStockForm')[0].reset();
        $('#openingStockForm').removeClass('was-validated');
        $('#openingDate').val(new Date().toISOString().split('T')[0]);
        $('#openingLinesBody').empty();
        this._modal.show();
    },

    addLine: function () {
        const warehouseId = $('#openingWarehouseId').val();
        if (!warehouseId) {
            toastr.warning("Select a warehouse first.");
            return;
        }

        const id = ++this._lineNo;
        const html = `
            <tr id="opening_row_${id}">
                <td class="ps-3">
                    <select class="form-select form-select-sm opening-variant-select" required style="width:100%"></select>
                </td>
                <td>
                    <input type="number" class="form-control form-control-sm text-center fw-bold opening-qty-input" value="1" min="1" step="1" required>
                </td>
                <td>
                    <input type="number" class="form-control form-control-sm text-end fw-bold opening-cost-input" value="0.00" min="0.01" step="0.01" required>
                </td>
                <td>
                    <input type="number" class="form-control form-control-sm text-end opening-selling-input" value="0.00" min="0" step="0.01">
                </td>
                <td>
                    <input type="text" class="form-control form-control-sm opening-batch-input" maxlength="50" placeholder="Optional">
                </td>
                <td class="text-center pe-2 align-middle">
                    <button type="button" class="btn btn-sm text-danger" onclick="openingStockApp.removeLine(${id})" title="Remove line">
                        <i class="fa-solid fa-trash-can"></i>
                    </button>
                </td>
            </tr>`;

        $('#openingLinesBody').append(html);

        $(`#opening_row_${id} .opening-variant-select`).select2({
            theme: 'bootstrap-5',
            dropdownParent: $('#openingStockModal'),
            placeholder: 'Search stocked product...',
            minimumInputLength: 0,
            ajax: {
                url: '/api/masterdata/variants/search',
                dataType: 'json',
                delay: 250,
                headers: {
                    'Authorization': `Bearer ${localStorage.getItem('nexus_token')}`,
                    'RequestVerificationToken': api.getToken()
                },
                data: params => ({
                    query: params.term || '',
                    stockedOnly: true,
                    warehouseId: $('#openingWarehouseId').val(),
                    excludeWarehouseActivity: true
                }),
                processResults: data => ({
                    results: $.map(data || [], item => ({
                        id: item.id,
                        text: `${item.sku} - ${item.name}`,
                        costPrice: item.costPrice || 0,
                        sellingPrice: item.sellingPrice || 0
                    }))
                })
            }
        }).on('select2:select', function (e) {
            const selected = e.params.data || {};
            const $row = $(this).closest('tr');
            $row.find('.opening-cost-input').val(parseFloat(selected.costPrice || 0).toFixed(2));
            $row.find('.opening-selling-input').val(parseFloat(selected.sellingPrice || 0).toFixed(2));
        });
    },

    removeLine: function (id) {
        const $row = $(`#opening_row_${id}`);
        const $select = $row.find('.opening-variant-select');
        if ($select.data('select2')) $select.select2('destroy');
        $row.remove();
    },

    _collectItems: function () {
        const items = [];
        const variants = new Set();
        let error = null;

        $('#openingLinesBody tr').each(function () {
            const variantId = parseInt($(this).find('.opening-variant-select').val());
            const rawQuantity = $(this).find('.opening-qty-input').val();
            const quantity = Number(rawQuantity);
            const unitCost = parseFloat($(this).find('.opening-cost-input').val()) || 0;
            const sellingPrice = parseFloat($(this).find('.opening-selling-input').val()) || 0;
            const batchNo = ($(this).find('.opening-batch-input').val() || '').trim();

            if (!variantId) error = "Select a product variant on every line.";
            if (quantity <= 0) error = "Quantity must be greater than zero.";
            if (!Number.isInteger(quantity)) error = "Quantity must be a whole number.";
            if (unitCost <= 0) error = "Unit cost must be greater than zero.";
            if (sellingPrice < 0) error = "Selling price cannot be negative.";
            if (variants.has(variantId)) error = "Duplicate variants are not allowed in one opening stock entry.";

            variants.add(variantId);
            items.push({ productVariantId: variantId, quantity, unitCost, sellingPrice, batchNo });
        });

        return { items, error };
    },

    save: async function (e) {
        const form = $('#openingStockForm')[0];
        if (!form.checkValidity()) { $(form).addClass('was-validated'); return; }

        const collected = this._collectItems();
        if (collected.error || collected.items.length === 0) {
            toastr.error(collected.error || "Add at least one opening stock item.");
            return;
        }

        const payload = {
            openingDate: $('#openingDate').val(),
            warehouseId: parseInt($('#openingWarehouseId').val()),
            notes: $('#openingNotes').val().trim(),
            items: collected.items
        };

        const $btn = $(e.currentTarget);
        const originalHtml = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Posting...');

        try {
            const res = await api.post('/api/inventory/opening-stock', payload);

            if (res && res.succeeded) {
                toastr.success(res.message || "Opening stock posted successfully.");
                this._modal.hide();
                this._table.ajax.reload(null, false);
            }
        } catch (err) {
            console.error("Opening stock post failed.", err);
        } finally {
            $btn.prop('disabled', false).html(originalHtml);
        }
    }
};

$(document).ready(() => window.openingStockApp.init());
