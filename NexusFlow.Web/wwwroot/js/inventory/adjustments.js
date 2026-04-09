window.adjApp = {
    _table: null,
    _modal: null,
    _warehouses: [],
    _products: [],

    init: function () {
        var modalEl = document.getElementById('adjModal');
        if (modalEl) this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });

        this._initGrid();
        this._loadLookups();

        $('#WarehouseId').on('change', () => {
            $('#linesBody').empty();
            this.addLine();
        });
    },

    _initGrid: function () {
        this._table = $('#adjustmentsGrid').DataTable({
            ajax: async function (data, callback) {
                try {
                    const res = await api.get('/api/inventory/adjustments');
                    callback({ data: res.data || res || [] });
                } catch (e) { callback({ data: [] }); }
            },
            columns: [
                { data: 'referenceNo', className: 'fw-bold text-warning text-dark font-monospace ps-3' },
                { data: 'date', render: d => new Date(d).toLocaleDateString() },
                { data: 'warehouse', className: 'fw-bold text-dark' },
                { data: 'notes', className: 'text-muted fst-italic' },
                { data: 'itemsAffected', className: 'text-center' },
                { data: 'totalImpactValue', className: 'text-end fw-bold pe-3', render: d => '$' + parseFloat(Math.abs(d)).toLocaleString(undefined, { minimumFractionDigits: 2 }) }
            ],
            order: [[1, 'desc'], [0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    _loadLookups: async function () {
        try {
            const [whRes, prodRes] = await Promise.all([
                api.get('/api/masterdata/warehouses'),
                api.get('/api/product') 
            ]);

            this._warehouses = whRes.data || whRes || [];
            this._products = prodRes.data || prodRes || [];

            let $wh = $('#WarehouseId').empty().append('<option value="">-- Select Warehouse --</option>');
            this._warehouses.forEach(w => $wh.append(`<option value="${w.id}">${w.name}</option>`));

        } catch (e) { console.error("Lookup Error:", e); }
    },

    openCreateModal: function () {
        $('#adjForm')[0].reset();
        $('#adjForm').removeClass('was-validated');
        $('#AdjDate').val(new Date().toISOString().split('T')[0]);
        $('#linesBody').empty();
        this.addLine(); 
        this._modal.show();
    },

    addLine: function () {
        const id = Date.now();
        let prodOpts = '<option value="">-- Select Item --</option>';
        
        this._products.forEach(p => {
            if(p.variants) {
                p.variants.forEach(v => {
                    prodOpts += `<option value="${v.id}">${p.name} - ${v.sku}</option>`;
                });
            }
        });

        const html = `
            <tr id="row_${id}">
                <td class="ps-3">
                    <select class="form-select form-select-sm variant-select select2" required style="width:100%">${prodOpts}</select>
                </td>
                <td class="text-end align-middle">
                    <span class="badge bg-primary bg-opacity-10 text-primary border border-primary w-100 lbl-on-hand">0.00</span>
                </td>
                <td>
                    <select class="form-select form-select-sm type-select fw-bold text-center" required>
                        <option value="1" class="text-danger">Remove (Shrinkage)</option>
                        <option value="2" class="text-success">Add (Surplus)</option>
                    </select>
                </td>
                <td>
                    <input type="number" class="form-control form-control-sm qty-input text-center fw-bold" value="1" min="0.01" step="0.01" required disabled>
                </td>
                <td>
                    <input type="number" class="form-control form-control-sm cost-input text-end bg-light" placeholder="FIFO Auto" disabled>
                </td>
                <td class="text-center pe-2 align-middle">
                    <button type="button" class="btn btn-sm text-danger" onclick="$('#row_${id}').remove();"><i class="fa-solid fa-trash-can"></i></button>
                </td>
            </tr>`;
            
        $('#linesBody').append(html);
        const $select = $(`#row_${id} .select2`).select2({ dropdownParent: $('#adjModal') });

        // Logic 1: Handle Type Change (Enable/Disable Cost Input)
        $(`#row_${id} .type-select`).on('change', function() {
            const isSurplus = $(this).val() === "2";
            const $costInput = $(`#row_${id} .cost-input`);
            
            if(isSurplus) {
                $costInput.prop('disabled', false).prop('required', true).removeClass('bg-light').attr('placeholder', '0.00');
            } else {
                $costInput.prop('disabled', true).prop('required', false).addClass('bg-light').val('').attr('placeholder', 'FIFO Auto');
            }
        });

        // Logic 2: Fetch Stock on Variant Change
        $select.on('change', async function() {
            const variantId = $(this).val();
            const warehouseId = $('#WarehouseId').val();
            const $row = $(this).closest('tr');
            const $lbl = $row.find('.lbl-on-hand');
            const $qtyInput = $row.find('.qty-input');

            if (!variantId) { $lbl.text('0.00'); $qtyInput.prop('disabled', true); return; }
            if (!warehouseId) { toastr.error("Select Warehouse first."); $(this).val('').trigger('change.select2'); return; }

            $lbl.html('<i class="fa-solid fa-spinner fa-spin"></i>');

            try {
                const res = await api.get(`/api/inventory/stock-level?variantId=${variantId}&warehouseId=${warehouseId}`);
                const qty = res.data || 0;
                
                $lbl.text(qty.toLocaleString(undefined, { minimumFractionDigits: 2 }));
                $qtyInput.prop('disabled', false);
                
                // Add validation for Shrinkage max
                $qtyInput.on('input', function() {
                    let type = $row.find('.type-select').val();
                    let currentVal = parseFloat($(this).val()) || 0;
                    if (type === "1" && currentVal > qty) { // Shrinkage check
                        $(this).val(qty);
                        toastr.warning(`Cannot shrink more than available stock (${qty}).`);
                    }
                });

            } catch(e) { $lbl.text('Error'); }
        });
    },

    saveAdjustment: async function(e) {
        var form = $('#adjForm')[0];
        if (!form.checkValidity()) { $(form).addClass('was-validated'); return; }

        let items = [];
        let hasError = false;

        $('#linesBody tr').each(function() {
            let variantId = parseInt($(this).find('.variant-select').val());
            let type = parseInt($(this).find('.type-select').val());
            let qty = parseFloat($(this).find('.qty-input').val()) || 0;
            let cost = parseFloat($(this).find('.cost-input').val()) || 0;
            
            if (!variantId || qty <= 0) hasError = true;
            if (type === 2 && cost <= 0) hasError = true; // Surplus must have a cost

            items.push({ ProductVariantId: variantId, Type: type, Quantity: qty, UnitCost: cost });
        });

        if (hasError || items.length === 0) {
            toastr.error("Invalid entry. Ensure items are selected, quantities > 0, and Surplus items have a Unit Cost.");
            return;
        }

        const payload = {
            WarehouseId: parseInt($('#WarehouseId').val()),
            AdjustmentDate: $('#AdjDate').val(),
            Reason: $('#Reason').val(),
            Items: items
        };

        var $btn = $(e.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-2"></i>Posting to GL...');

        try {
            const res = await api.post('/api/inventory/adjustments', payload);
            if (res && res.succeeded) {
                toastr.success(res.message);
                this._modal.hide();
                this._table.ajax.reload(null, false);
            } else if (res && res.messages) { toastr.error(res.messages[0]); }
        } catch (err) { 
            toastr.error(err.responseJSON?.messages?.[0] || "Failed to post adjustment."); 
        } finally { 
            $btn.prop('disabled', false).html(ogText); 
        }
    }
};

$(document).ready(() => adjApp.init());