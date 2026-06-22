window.productionOrderApp = {
    _table: null,
    _modal: null,
    _detailModal: null,
    _actionModal: null,
    _current: null,
    _boms: [],

    init: async function () {
        this._modal = new bootstrap.Modal(document.getElementById('productionOrderModal'));
        this._detailModal = new bootstrap.Modal(document.getElementById('productionDetailModal'));
        this._actionModal = new bootstrap.Modal(document.getElementById('productionActionModal'));
        this._initGrid();
        await this._loadLookups();
        this._loadLegacy();
        $('#poBom').on('change', () => this._selectBom());
        $('#btnSaveProductionOrder').on('click', e => this.save(e));
        $('#btnPostProductionAction').on('click', e => this.postAction(e));
        $('#receiptAccepted').on('input', () => this._proposeConsumption());
    },

    _initGrid: function () {
        this._table = $('#productionOrdersGrid').DataTable({
            ajax: async (data, callback) => {
                try { const res = await api.get('/api/inventory/production-orders'); callback({ data: res.data || res || [] }); }
                catch { callback({ data: [] }); }
            },
            columns: [
                { data: 'orderNumber', className: 'fw-bold font-monospace text-primary' },
                { data: 'orderDate', render: d => new Date(d).toLocaleDateString() },
                { data: 'contractorName' }, { data: 'finishedGood', className: 'fw-semibold' },
                { data: 'targetQuantity', className: 'text-end' }, { data: 'acceptedQuantity', className: 'text-end' },
                { data: 'status', render: s => this._statusBadge(s) },
                { data: null, className: 'text-end', orderable: false, render: row => `
                    <button class="btn btn-sm btn-outline-dark" onclick="productionOrderApp.view(${row.id})" title="View"><i class="fa-solid fa-eye"></i></button>
                    <button class="btn btn-sm btn-outline-secondary" onclick="NexusPrint.printDocument('ProductionOrder', ${row.id})" title="Print"><i class="fa-solid fa-print"></i></button>
                    <button class="btn btn-sm btn-outline-danger" onclick="NexusPrint.downloadDocument('ProductionOrder', ${row.id})" title="Download PDF"><i class="fa-solid fa-file-pdf"></i></button>` }
            ],
            order: [[0, 'desc']], pageLength: 20
        });
    },

    _loadLookups: async function () {
        const [supRes, whRes, bomRes] = await Promise.all([
            api.get('/api/supplier'), api.get('/api/masterdata/warehouses'), api.get('/api/bom')
        ]);
        const suppliers = supRes.data || supRes || [];
        const warehouses = whRes.data || whRes || [];
        this._boms = (bomRes.data || bomRes || []).filter(x => x.isActive && x.isApproved);
        $('#poContractor').html('<option value="">Select contractor</option>' + suppliers.map(x => `<option value="${x.id}">${this._escape(x.name)}</option>`).join(''));
        const whOptions = '<option value="">Select warehouse</option>' + warehouses.map(x => `<option value="${x.id}">${this._escape(x.name)}</option>`).join('');
        $('#poSourceWarehouse, #poDestinationWarehouse').html(whOptions);
        $('#poBom').html('<option value="">Select BOM revision</option>' + this._boms.map(x => `<option value="${x.id}">${this._escape(x.productVariantName)} | Rev ${x.revisionNumber} | Basis ${x.basisQuantity}</option>`).join(''));
        $('#poContractor, #poBom, #poSourceWarehouse, #poDestinationWarehouse').select2({ dropdownParent: $('#productionOrderModal'), width: '100%' });
    },

    _selectBom: function () {
        const bom = this._boms.find(x => x.id === parseInt($('#poBom').val()));
        $('#poFinishedGood').empty();
        if (bom) $('#poFinishedGood').append(new Option(bom.productVariantName, bom.productVariantId, true, true));
    },

    openCreate: function () {
        $('#productionOrderForm')[0].reset();
        $('#poId').val(0); $('#poRowVersion').val('');
        document.getElementById('poDate').valueAsDate = new Date();
        $('#poContractor, #poBom, #poSourceWarehouse, #poDestinationWarehouse').val(null).trigger('change');
        $('#poFinishedGood').empty();
        this._modal.show();
    },

    save: async function (e) {
        const form = document.getElementById('productionOrderForm');
        if (!form.checkValidity()) { form.reportValidity(); return; }
        const id = parseInt($('#poId').val()) || 0;
        const toleranceText = $('#poTolerance').val();
        const payload = {
            orderDate: $('#poDate').val(), contractorId: parseInt($('#poContractor').val()),
            finishedGoodVariantId: parseInt($('#poFinishedGood').val()), billOfMaterialId: parseInt($('#poBom').val()),
            sourceWarehouseId: parseInt($('#poSourceWarehouse').val()), destinationWarehouseId: parseInt($('#poDestinationWarehouse').val()),
            targetQuantity: parseInt($('#poTarget').val()), overproductionTolerancePercent: toleranceText === '' ? null : parseFloat(toleranceText),
            plannedStartDate: $('#poStart').val() || null, dueDate: $('#poDue').val() || null,
            notes: $('#poNotes').val(), rowVersion: $('#poRowVersion').val()
        };
        const $button = $(e.currentTarget).prop('disabled', true);
        try {
            const res = id ? await api.put(`/api/inventory/production-orders/${id}`, payload) : await api.post('/api/inventory/production-orders', payload);
            if (!res.succeeded) throw new Error(res.message || res.messages?.[0]);
            toastr.success(res.message); this._modal.hide(); this._table.ajax.reload(null, false);
        } catch (error) { toastr.error(error.responseJSON?.message || error.message || 'Unable to save production order.'); }
        finally { $button.prop('disabled', false); }
    },

    view: async function (id) {
        try {
            const res = await api.get(`/api/inventory/production-orders/${id}`);
            this._current = res.data || res;
            const x = this._current;
            $('#podNumber').text(x.orderNumber); $('#podStatus').html(this._statusBadge(x.status));
            $('#podContractor').text(x.contractorName); $('#podFinishedGood').text(`${x.finishedGood} | BOM Rev ${x.bomRevisionNumber}`);
            $('#podOutput').text(`${x.targetQuantity} / ${x.acceptedQuantity} accepted, ${x.rejectedQuantity} rejected`);
            $('#podSewing').text(`${this._money(x.sewingBilled)} / ${this._money(x.sewingAccrued)} billed`);
            $('#podClaims').text(this._money(x.openClaimAmount));
            $('#podComponents').html(x.components.map(c => `<tr>
                <td><span class="fw-semibold">${this._escape(c.material)}</span><div class="small text-muted font-monospace">${this._escape(c.sku)}</div></td>
                ${[c.planned,c.issued,c.returned,c.consumed,c.normalWaste,c.abnormalLoss,c.contractorRecoverable,c.contractorHeld].map(v => `<td class="text-end">${this._qty(v)}</td>`).join('')}
                <td class="text-end fw-semibold">${this._money(c.unallocatedWipCost)}</td></tr>`).join(''));
            const claimRows = (x.claims || []).map(c => `<div class="border-bottom py-2"><b>${c.claimNumber}</b> Contractor Claim <span class="float-end">${this._money(c.amount)}</span><br><span class="text-muted">${c.status}${c.settlementReference ? ` - ${this._escape(c.settlementReference)}` : ''}</span></div>`).join('');
            $('#podMovements').html((x.movements.length ? x.movements.map(m => `<div class="border-bottom py-2"><b>${m.referenceNumber}</b> ${m.type} <span class="float-end">${this._money(m.totalCost)}</span><br><span class="text-muted">${new Date(m.date).toLocaleDateString()} ${this._escape(m.notes)}</span></div>`).join('') : '<span class="text-muted">No movements.</span>') + claimRows);
            $('#podReceipts').html(x.receipts.length ? x.receipts.map(r => `<div class="border-bottom py-2"><b>${r.receiptNumber}</b> ${r.acceptedQuantity} accepted / ${r.rejectedQuantity} rejected <span class="float-end">${this._money(r.finishedGoodsCost)}</span><br><span class="text-muted">Sewing ${this._money(r.sewingCharge)} ${r.isSewingBilled ? 'billed' : 'unbilled'}</span></div>`).join('') : '<span class="text-muted">No receipts.</span>');
            this._renderActions();
            this._detailModal.show();
        } catch (error) { toastr.error(error.responseJSON?.message || 'Unable to load production order.'); }
    },

    _renderActions: function () {
        const x = this._current;
        let html = '';
        if (x.status === 'Draft') html += this._actionButton('Release', 'fa-paper-plane', 'release');
        if (['Released','InProgress'].includes(x.status)) html += this._actionButton('Issue Material', 'fa-truck-ramp-box', 'issue');
        if (x.status === 'InProgress') {
            html += this._actionButton('Return Material', 'fa-rotate-left', 'return', 'outline-secondary');
            html += this._actionButton('Production Receipt', 'fa-boxes-stacked', 'receipt', 'success');
            html += this._actionButton('Revise Order', 'fa-pen-ruler', 'revise', 'outline-warning');
        }
        if (x.status === 'ReadyToClose') html += this._actionButton('Close & Reconcile', 'fa-lock', 'close', x.canClose ? 'dark' : 'outline-dark');
        if ((x.claims || []).some(c => c.status === 'Open')) html += this._actionButton('Settle Supplier Claim', 'fa-file-circle-check', 'settleClaim', 'outline-danger');
        $('#podActions').html(html);
    },

    _actionButton: function (text, icon, action, style = 'primary') {
        return `<button class="btn btn-sm btn-${style}" onclick="productionOrderApp.action('${action}')"><i class="fa-solid ${icon} me-1"></i>${text}</button>`;
    },

    action: async function (type) {
        if (type === 'settleClaim') return this._settleClaim();
        if (['release','revise','close'].includes(type)) return this._simpleAction(type);
        $('#productionActionType').val(type);
        document.getElementById('productionActionDate').valueAsDate = new Date();
        $('#productionActionNotes').val('');
        $('#receiptHeader').toggleClass('d-none', type !== 'receipt');
        $('#productionActionTitle').text(type === 'issue' ? 'Issue Materials to Contractor' : type === 'return' ? 'Return Unused Materials' : 'Receive Production Output');
        if (type === 'receipt') {
            const remaining = Math.max(0, this._current.targetQuantity - this._current.acceptedQuantity);
            $('#receiptAccepted').val(remaining); $('#receiptRejected, #receiptSewing').val(0); $('#receiptBatch').val('');
            $('#productionActionHead').html('<tr><th>Material</th><th>Consumed</th><th>Normal Waste</th><th>Abnormal Loss</th><th>Contractor Recoverable</th><th class="text-end">Held</th></tr>');
            $('#productionActionBody').html(this._current.components.map(c => `<tr data-id="${c.id}" data-per-unit="${c.quantityPerUnit}" data-held="${c.contractorHeld}"><td>${this._escape(c.material)}</td><td><input class="form-control form-control-sm consumed" type="number" min="0" step="0.001"></td><td><input class="form-control form-control-sm normal" type="number" min="0" step="0.001" value="0"></td><td><input class="form-control form-control-sm abnormal" type="number" min="0" step="0.001" value="0"></td><td><input class="form-control form-control-sm recoverable" type="number" min="0" step="0.001" value="0"></td><td class="text-end">${this._qty(c.contractorHeld)}</td></tr>`).join(''));
            this._proposeConsumption();
        } else {
            $('#productionActionHead').html('<tr><th>Material</th><th class="text-end">Available</th><th style="width:220px">Quantity</th></tr>');
            $('#productionActionBody').html(this._current.components.map(c => {
                const available = type === 'issue' ? Math.max(0, c.planned - c.issued) : c.contractorHeld;
                return `<tr data-id="${c.id}"><td>${this._escape(c.material)} <span class="text-muted font-monospace">${this._escape(c.sku)}</span></td><td class="text-end">${this._qty(available)}</td><td><input class="form-control form-control-sm movement-qty" type="number" min="0" max="${available}" step="0.001" value="${available}"></td></tr>`;
            }).join(''));
        }
        this._actionModal.show();
    },

    _proposeConsumption: function () {
        const accepted = parseFloat($('#receiptAccepted').val()) || 0;
        $('#productionActionBody tr').each(function () {
            const proposed = Math.min(parseFloat($(this).data('held')) || 0, (parseFloat($(this).data('per-unit')) || 0) * accepted);
            $(this).find('.consumed').val(proposed.toFixed(3));
        });
    },

    postAction: async function (e) {
        const type = $('#productionActionType').val();
        const payload = { notes: $('#productionActionNotes').val() };
        let url;
        if (type === 'receipt') {
            url = `/api/inventory/production-orders/${this._current.id}/receipts`;
            Object.assign(payload, { receiptDate: $('#productionActionDate').val(), acceptedQuantity: parseInt($('#receiptAccepted').val()) || 0, rejectedQuantity: parseInt($('#receiptRejected').val()) || 0, sewingCharge: parseFloat($('#receiptSewing').val()) || 0, batchNumber: $('#receiptBatch').val(), consumptions: [] });
            $('#productionActionBody tr').each(function () { payload.consumptions.push({ productionOrderComponentId: parseInt($(this).data('id')), consumedQuantity: parseFloat($(this).find('.consumed').val()) || 0, normalWasteQuantity: parseFloat($(this).find('.normal').val()) || 0, abnormalLossQuantity: parseFloat($(this).find('.abnormal').val()) || 0, contractorRecoverableQuantity: parseFloat($(this).find('.recoverable').val()) || 0 }); });
        } else {
            url = `/api/inventory/production-orders/${this._current.id}/material-${type === 'issue' ? 'issues' : 'returns'}`;
            payload[type === 'issue' ? 'issueDate' : 'returnDate'] = $('#productionActionDate').val();
            payload.lines = [];
            $('#productionActionBody tr').each(function () { const quantity = parseFloat($(this).find('.movement-qty').val()) || 0; if (quantity > 0) payload.lines.push({ productionOrderComponentId: parseInt($(this).data('id')), quantity }); });
        }
        const $button = $(e.currentTarget).prop('disabled', true);
        try { const res = await api.post(url, payload); if (!res.succeeded) throw new Error(res.message); toastr.success(res.message); this._actionModal.hide(); await this.view(this._current.id); this._table.ajax.reload(null, false); }
        catch (error) { toastr.error(error.responseJSON?.message || error.message || 'Production posting failed.'); }
        finally { $button.prop('disabled', false); }
    },

    _simpleAction: async function (type) {
        const today = new Date().toISOString().slice(0, 10);
        let html = `<input type="date" id="swalDate" class="swal2-input" value="${today}">`;
        if (type === 'revise') html += `<input type="number" id="swalTarget" class="swal2-input" min="1" step="1" value="${this._current.targetQuantity}" placeholder="Target quantity"><input type="number" id="swalTolerance" class="swal2-input" min="0" step="0.01" value="${this._current.tolerancePercent}" placeholder="Tolerance %"><input id="swalReason" class="swal2-input" placeholder="Revision reason">`;
        const result = await Swal.fire({ title: type === 'release' ? 'Release Production Order' : type === 'close' ? 'Close and Reconcile' : 'Revise Production Order', html, showCancelButton: true, confirmButtonText: type === 'close' ? 'Close' : 'Confirm', preConfirm: () => ({ date: $('#swalDate').val(), targetQuantity: parseInt($('#swalTarget').val()), tolerancePercent: parseFloat($('#swalTolerance').val()), reason: $('#swalReason').val() }) });
        if (!result.isConfirmed) return;
        const url = `/api/inventory/production-orders/${this._current.id}/${type === 'revise' ? 'revision' : type}`;
        try { const res = await api.post(url, result.value); if (!res.succeeded) throw new Error(res.message); toastr.success(res.message); await this.view(this._current.id); this._table.ajax.reload(null, false); }
        catch (error) { toastr.error(error.responseJSON?.message || error.message || `Unable to ${type} order.`); }
    },

    _settleClaim: async function () {
        const claim = (this._current.claims || []).find(x => x.status === 'Open');
        if (!claim) return;
        const today = new Date().toISOString().slice(0, 10);
        const result = await Swal.fire({ title: `Settle ${claim.claimNumber}`, html: `<input type="date" id="claimDate" class="swal2-input" value="${today}"><input id="claimReference" class="swal2-input" placeholder="Supplier credit/debit-note reference">`, showCancelButton: true, confirmButtonText: 'Settle', preConfirm: () => ({ date: $('#claimDate').val(), reference: $('#claimReference').val() }) });
        if (!result.isConfirmed) return;
        try { const res = await api.post(`/api/inventory/production-orders/${this._current.id}/claims/${claim.id}/settle`, result.value); if (!res.succeeded) throw new Error(res.message); toastr.success(res.message); await this.view(this._current.id); }
        catch (error) { toastr.error(error.responseJSON?.message || error.message || 'Unable to settle claim.'); }
    },

    printOrder: function () { NexusPrint.printDocument('ProductionOrder', this._current.id); },
    pdfOrder: function () { NexusPrint.downloadDocument('ProductionOrder', this._current.id); },

    _loadLegacy: async function () {
        try {
            const [issuesRes, receiptsRes] = await Promise.all([api.get('/api/inventory/issues'), api.get('/api/inventory/production-receipts')]);
            const issues = issuesRes.data || issuesRes || []; const receipts = receiptsRes.data || receiptsRes || [];
            $('#legacyIssues tbody').html(issues.map(x => `<tr><td>${x.issueNo}</td><td>${new Date(x.issueDate).toLocaleDateString()}</td><td class="text-end">${this._money(x.totalCost)}</td></tr>`).join(''));
            $('#legacyReceipts tbody').html(receipts.map(x => `<tr><td>${x.receiptNo}</td><td>${new Date(x.date).toLocaleDateString()}</td><td class="text-end">${this._money(x.totalValue)}</td></tr>`).join(''));
        } catch { }
    },

    _statusBadge: function (status) { const map = { Draft:'secondary', Released:'primary', InProgress:'warning text-dark', ReadyToClose:'info text-dark', Closed:'success', Cancelled:'dark' }; return `<span class="badge bg-${map[status] || 'secondary'}">${status.replace(/([A-Z])/g, ' $1').trim()}</span>`; },
    _money: value => parseFloat(value || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }),
    _qty: value => parseFloat(value || 0).toLocaleString(undefined, { maximumFractionDigits: 3 }),
    _escape: value => $('<div>').text(value || '').html()
};

$(document).ready(() => productionOrderApp.init());
