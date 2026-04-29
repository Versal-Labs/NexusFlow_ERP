window.stockTakeApp = {
    _table: null,
    _initModal: null,
    _countModal: null,
    _approveModal: null,

    init: function () {
        var im = document.getElementById('initiateModal');
        var cm = document.getElementById('countModal');
        var am = document.getElementById('approveModal');
        
        if (im) this._initModal = new bootstrap.Modal(im);
        if (cm) this._countModal = new bootstrap.Modal(cm);
        if (am) this._approveModal = new bootstrap.Modal(am);

        this._initGrid();
        this._loadLookups();

        $('#searchCountItems').on('input', function () {
            let term = $(this).val().toLowerCase();
            $('.count-row').each(function () {
                let desc = $(this).find('.product-desc').text().toLowerCase();
                if (desc.includes(term)) {
                    $(this).show();
                } else {
                    $(this).hide();
                }
            });
        });
    },

    _initGrid: function () {
        this._table = $('#stockTakeGrid').DataTable({
            ajax: {
                url: '/api/inventory/stocktakes',
                dataSrc: function (json) { return json.data || json || []; }
            },
            columns: [
                { data: 'stockTakeNumber', className: 'fw-bold font-monospace text-primary ps-4' },
                { data: 'date' },
                { data: 'warehouseName', className: 'fw-bold' },
                {
                    data: 'statusText',
                    className: 'text-center',
                    render: function(d) {
                        if(d === 'Initiated') return '<span class="badge bg-secondary"><i class="fa-solid fa-pause me-1"></i>Waiting for Count</span>';
                        if(d === 'Counted') return '<span class="badge bg-warning text-dark"><i class="fa-solid fa-hourglass-half me-1"></i>Pending Review</span>';
                        if(d === 'Approved') return '<span class="badge bg-success"><i class="fa-solid fa-check-double me-1"></i>Approved & Adjusted</span>';
                        return `<span class="badge bg-dark">${d}</span>`;
                    }
                },
                {
                    data: 'totalVarianceValue',
                    className: 'text-end fw-bold',
                    render: function(d) {
                        let val = parseFloat(d);
                        if (val === 0) return '-';
                        if (val > 0) return `<span class="text-success">+${val.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>`;
                        return `<span class="text-danger">${val.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>`;
                    }
                },
                {
                    data: null,
                    className: 'text-end pe-4',
                    orderable: false,
                    render: function (data, type, row) {
                        let btns = '';
                        if (row.statusText === 'Initiated') {
                            btns += `<button class="btn btn-sm btn-warning shadow-sm fw-bold me-1" onclick="stockTakeApp.openCountModal(${row.id})"><i class="fa-solid fa-keyboard me-1"></i>Enter Count</button>`;
                        } else if (row.statusText === 'Counted') {
                            btns += `<button class="btn btn-sm btn-info shadow-sm fw-bold" onclick="stockTakeApp.openApproveModal(${row.id})"><i class="fa-solid fa-magnifying-glass me-1"></i>Review</button>`;
                        } else {
                            btns += `<button class="btn btn-sm btn-light border" onclick="stockTakeApp.openApproveModal(${row.id})"><i class="fa-solid fa-eye text-secondary"></i></button>`;
                        }
                        return btns;
                    }
                }
            ],
            order: [[0, 'desc']],
            dom: '<"d-flex justify-content-between align-items-center mb-3"f>rt<"d-flex justify-content-between align-items-center mt-3"ip>'
        });
    },

    _loadLookups: async function () {
        try {
            const whRes = await api.get('/api/masterdata/warehouses');
            const warehouses = Array.isArray(whRes) ? whRes : (whRes?.data || []);
            let $wh = $('#WarehouseId').empty().append('<option value="">-- Select Target Warehouse --</option>');
            warehouses.forEach(w => $wh.append($('<option></option>').val(w.id).text(w.name)));
        } catch (e) { console.error("Lookup Error:", e); }
    },

    // --- PHASE 1: INITIATE ---
    openInitiateModal: function () {
        $('#initiateForm')[0].reset();
        this._initModal.show();
    },

    submitInitiate: async function () {
        if (!$('#WarehouseId').val()) { toastr.warning("Select a warehouse."); return; }
        
        var $btn = $(event.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm"></i>');

        try {
            const res = await api.post('/api/inventory/stocktakes/initiate', {
                WarehouseId: parseInt($('#WarehouseId').val()),
                Notes: $('#Notes').val()
            });
            if (res && res.succeeded) {
                toastr.success(res.message);
                this._initModal.hide();
                this._table.ajax.reload(null, false);
            } else toastr.error(res.message);
        } catch(e) { console.error(e); } 
        finally { $btn.prop('disabled', false).html(ogText); }
    },

    // --- PHASE 2: BLIND COUNT ---
    openCountModal: async function (id) {
        $('#CountStockTakeId').val(id);
        $('#searchCountItems').val(''); // Reset search bar
        $('#countBody').html('<tr><td colspan="4" class="text-center py-4"><i class="spinner-border text-primary"></i> Loading Inventory Snapshot...</td></tr>');
        this._countModal.show();

        try {
            const res = await api.get(`/api/inventory/stocktakes/${id}`);
            const data = res.data || res;
            let html = '';

            data.items.forEach(i => {
                // TIER-1 FIX: Pre-fill with SystemQty instead of leaving blank!
                let val = i.countedQty !== null ? i.countedQty : i.systemQty;
                let sysQty = parseFloat(i.systemQty) || 0;

                // Pre-calculate live variance
                let variance = val - sysQty;
                let varHtml = window.stockTakeApp._getVarianceBadge(variance);

                html += `
                    <tr class="count-row" data-vid="${i.productVariantId}" data-sys="${sysQty}">
                        <td class="ps-3 fw-bold text-dark product-desc">${i.description}</td>
                        <td class="text-center font-monospace text-muted align-middle">${sysQty}</td>
                        <td class="px-3">
                            <input type="number" class="form-control form-control-sm text-center fw-bold border-primary count-input" value="${val}" min="0" step="0.01" onclick="this.select()">
                        </td>
                        <td class="text-center align-middle variance-cell">${varHtml}</td>
                    </tr>`;
            });

            $('#countBody').html(html);
            $('#lblCountProgress').text(`${data.items.length} Items Total`);

            // TIER-1 FEATURE: Live Variance Calculation on Input
            $('.count-input').on('input', function () {
                let row = $(this).closest('tr');
                let sys = parseFloat(row.data('sys')) || 0;
                let count = parseFloat($(this).val());
                if (isNaN(count)) count = 0; // Fallback to 0 if they delete the number

                let variance = count - sys;
                row.find('.variance-cell').html(window.stockTakeApp._getVarianceBadge(variance));
            });

        } catch (e) {
            $('#countBody').html('<tr><td colspan="4" class="text-danger text-center fw-bold py-4">Failed to load snapshot data.</td></tr>');
        }
    },

    _getVarianceBadge: function (variance) {
        if (variance === 0) return `<span class="badge bg-light text-muted border shadow-sm"><i class="fa-solid fa-check me-1"></i> Match</span>`;
        if (variance > 0) return `<span class="badge bg-success bg-opacity-10 text-success border border-success shadow-sm">+${variance} (Surplus)</span>`;
        return `<span class="badge bg-danger bg-opacity-10 text-danger border border-danger shadow-sm">${variance} (Shrink)</span>`;
    },

    submitCount: async function () {
        let items = {};
        let hasErrors = false;

        $('#countBody tr.count-row').each(function () {
            let vid = parseInt($(this).data('vid'));
            let val = $(this).find('.count-input').val();

            if (val === '') {
                hasErrors = true;
                $(this).find('.count-input').addClass('is-invalid');
            } else {
                $(this).find('.count-input').removeClass('is-invalid');
                items[vid] = parseFloat(val);
            }
        });

        if (hasErrors) {
            toastr.warning("Please ensure a valid quantity is entered for all items. Enter 0 if the shelf is empty.");
            return;
        }

        var $btn = $(event.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm me-1"></i> Saving...');

        try {
            // TIER-1 FIX: Wrapped the data inside a 'Payload' object to match the C# Command!
            const res = await api.post('/api/inventory/stocktakes/count', {
                Payload: {
                    StockTakeId: parseInt($('#CountStockTakeId').val()),
                    CountedItems: items
                }
            });

            if (res && res.succeeded) {
                toastr.success(res.message || "Count submitted successfully.");
                this._countModal.hide();
                this._table.ajax.reload(null, false);
            } else {
                toastr.error(res?.messages?.[0] || "Failed to submit count.");
            }
        } catch (e) {
            console.error(e);
            toastr.error("Network error during submission.");
        }
        finally { $btn.prop('disabled', false).html(ogText); }
    },

    // --- PHASE 3: APPROVAL ---
    openApproveModal: async function (id) {
        $('#ApproveStockTakeId').val(id);
        $('#approveBody').html('<tr><td colspan="5" class="text-center py-4"><i class="spinner-border text-primary"></i></td></tr>');
        this._approveModal.show();

        try {
            const res = await api.get(`/api/inventory/stocktakes/${id}`);
            const data = res.data || res;
            let html = '';
            let netImpact = 0;

            data.items.forEach(i => {
                let counted = i.countedQty !== null ? i.countedQty : '-';
                let varianceCls = i.varianceQty === 0 ? 'text-muted' : (i.varianceQty > 0 ? 'text-success fw-bold' : 'text-danger fw-bold');
                let valCls = i.varianceValue === 0 ? 'text-muted' : (i.varianceValue > 0 ? 'text-success fw-bold' : 'text-danger fw-bold');
                let varText = i.varianceQty > 0 ? `+${i.varianceQty}` : i.varianceQty;
                
                netImpact += i.varianceValue;

                html += `
                    <tr>
                        <td class="ps-3 fw-bold text-dark">${i.description}</td>
                        <td class="text-center font-monospace">${i.systemQty}</td>
                        <td class="text-center font-monospace bg-warning bg-opacity-10">${counted}</td>
                        <td class="text-center font-monospace ${varianceCls}">${varText}</td>
                        <td class="text-end pe-3 ${valCls}">${i.varianceValue.toLocaleString(undefined, { minimumFractionDigits: 2 })}</td>
                    </tr>`;
            });
            $('#approveBody').html(html);
            
            let netCls = netImpact === 0 ? 'text-dark' : (netImpact > 0 ? 'text-success' : 'text-danger');
            $('#lblNetImpact').text(netImpact.toLocaleString(undefined, { minimumFractionDigits: 2 })).removeClass().addClass(`fw-bold font-monospace ${netCls}`);

            // Hide approve button if already approved
            if (data.status === 3) { // 3 = Approved
                $('#approveModal .btn-danger').hide();
            } else {
                $('#approveModal .btn-danger').show();
            }

        } catch(e) { $('#approveBody').html('<tr><td colspan="5" class="text-danger text-center">Load failed.</td></tr>'); }
    },

    approveCount: async function () {
        if (!confirm("Are you sure? This will adjust strict FIFO layers and post double-entry journals. This cannot be undone.")) return;

        var $btn = $(event.currentTarget);
        var ogText = $btn.html();
        $btn.prop('disabled', true).html('<i class="spinner-border spinner-border-sm"></i> Executing...');

        try {
            const id = parseInt($('#ApproveStockTakeId').val());
            const res = await api.post(`/api/inventory/stocktakes/${id}/approve`, {});
            if (res && res.succeeded) {
                toastr.success(res.message);
                this._approveModal.hide();
                this._table.ajax.reload(null, false);
            } else {
                toastr.error(res.message || "Approval failed.");
            }
        } catch(e) { console.error(e); toastr.error("Server error during reconciliation."); } 
        finally { $btn.prop('disabled', false).html(ogText); }
    }
};

$(document).ready(() => stockTakeApp.init());