window.grnApp = {
    _table: null,
    _modal: null,
    _products: [],

    init: function () {
        try {
            var modalEl = document.getElementById('grnModal');
            if (modalEl) this._modal = new bootstrap.Modal(modalEl, { backdrop: 'static' });

            this._initGrid();
            this._loadMasterData();

            $('#linesBody').on('input', '.calc-trigger', () => this.calculateTotals());
        } catch (e) {
            console.error("Init Error:", e);
        }
    },

    _initGrid: function () {
        this._table = $('#grnGrid').DataTable({
            ajax: {
                url: '/api/purchasing/grns',
                dataSrc: function (json) { return json.data || json || []; },
                headers: { "Authorization": "Bearer " + localStorage.getItem("jwtToken") }
            },
            columns: [
                { data: 'grnNumber', className: 'fw-bold text-primary font-monospace' },
                { data: 'receiptDate', render: d => new Date(d).toLocaleDateString() },
                { data: 'supplierName', className: 'fw-bold' },
                { data: 'warehouseName' },
                { data: 'referenceNo', className: 'font-monospace text-muted' },
                {
                    data: 'totalValue',
                    className: 'text-end fw-bold text-dark',
                    render: d => parseFloat(d).toLocaleString(undefined, { minimumFractionDigits: 2 })
                }
            ],
            order: [[0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>',
            pageLength: 20
        });
    },

    _loadMasterData: async function () {
        try {
            const [suppRes, whRes, prodRes] = await Promise.all([
                api.get('/api/supplier'),
                api.get('/api/masterdata/warehouses'),
                api.get('/api/product')
            ]);

            const suppliers = suppRes.data || suppRes;
            const warehouses = whRes.data || whRes;
            this._products = prodRes.data || prodRes; // Cache for line items

            let suppEl = $('#SupplierId');
            suppEl.empty().append('<option value="">-- Select Supplier --</option>');
            suppliers.forEach(s => suppEl.append(`<option value="${s.id}">${s.name}</option>`));
            suppEl.select2({ dropdownParent: $('#grnModal') });

            let whEl = $('#WarehouseId');
            whEl.empty().append('<option value="">-- Select Destination --</option>');
            warehouses.forEach(w => whEl.append(`<option value="${w.id}">[${w.code}] ${w.name}</option>`));

        } catch (e) {
            console.error("Lookup Error:", e);
        }
    },

    openModal: function () {
        document.getElementById('grnForm').reset();
        $('#linesBody').empty();
        document.getElementById('ReceiptDate').valueAsDate = new Date();
        $('#SupplierId').val('').trigger('change');
        this.addLine();
        this.calculateTotals();
        if (this._modal) this._modal.show();
    },

    addLine: function () {
        const id = Date.now();
        let productOptions = '<option value="">-- Select Item --</option>';

        // Filter only Physical Stock items (Type 0 or 1), ignore Services (Type 2)
        const stockProducts = this._products.filter(p => p.type !== 2 && p.type !== 'Service');

        stockProducts.forEach(p => {
            if (p.variants && p.variants.length > 0) {
                productOptions += `<optgroup label="${p.name}">`;
                p.variants.forEach(v => {
                    let desc = v.sku;
                    if (v.size || v.color) desc += ` (${v.size || ''} ${v.color || ''})`;
                    productOptions += `<option value="${v.id}" data-cost="${v.costPrice}">${desc}</option>`;
                });
                productOptions += `</optgroup>`;
            }
        });

        const html = `
            <tr id="row_${id}">
                <td>
                    <select class="form-select form-select-sm line-variant" onchange="grnApp.onVariantSelect(this)">
                        ${productOptions}
                    </select>
                </td>
                <td><input type="number" class="form-control form-control-sm text-center line-qty calc-trigger" value="1" min="0.01" step="0.01"></td>
                <td><input type="number" class="form-control form-control-sm text-end line-cost calc-trigger" value="0.00" step="0.01"></td>
                <td class="text-end fw-bold align-middle line-total">0.00</td>
                <td class="text-center align-middle">
                    <button type="button" class="btn btn-sm text-danger" onclick="$('#row_${id}').remove(); window.grnApp.calculateTotals();">
                        <i class="fa-solid fa-trash-can"></i>
                    </button>
                </td>
            </tr>`;
        $('#linesBody').append(html);
    },

    onVariantSelect: function (selectEl) {
        const cost = $(selectEl).find('option:selected').data('cost') || 0;
        $(selectEl).closest('tr').find('.line-cost').val(parseFloat(cost).toFixed(2));
        this.calculateTotals();
    },

    calculateTotals: function () {
        let grandTotal = 0;
        $('#linesBody tr').each(function () {
            const qty = parseFloat($(this).find('.line-qty').val()) || 0;
            const cost = parseFloat($(this).find('.line-cost').val()) || 0;
            const lineTotal = qty * cost;
            $(this).find('.line-total').text(lineTotal.toFixed(2));
            grandTotal += lineTotal;
        });

        $('#lblGrandTotal').text(grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2 }));
    },

    save: async function () {
        var form = document.getElementById('grnForm');
        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        const payload = {
            Grn: {
                ReceiptDate: $('#ReceiptDate').val(),
                SupplierId: parseInt($('#SupplierId').val()),
                WarehouseId: parseInt($('#WarehouseId').val()),
                ReferenceNo: $('#ReferenceNo').val(),
                Remarks: "",
                Items: []
            }
        };

        $('#linesBody tr').each(function () {
            const varId = $(this).find('.line-variant').val();
            if (varId) {
                payload.Grn.Items.push({
                    ProductVariantId: parseInt(varId),
                    Quantity: parseFloat($(this).find('.line-qty').val()) || 0,
                    UnitCost: parseFloat($(this).find('.line-cost').val()) || 0
                });
            }
        });

        if (payload.Grn.Items.length === 0) {
            toastr.warning("Add at least one item.");
            return;
        }

        const res = await api.post('/api/purchasing/grns', payload);

        if (res && res.succeeded) {
            toastr.success(res.messages ? res.messages[0] : "GRN Posted Successfully!");
            this._modal.hide();
            this._table.ajax.reload(null, false);
        }
    }
};

$(document).ready(function () { window.grnApp.init(); });