/* Migration Centre: staging and review are explicit; no upload automatically posts. */
(() => {
    'use strict';
    const root = document.getElementById('migrationCentre'); if (!root) return;
    const $ = id => document.getElementById(id);
    const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const pretty = name => name.replace(/([a-z])([A-Z])/g, '$1 $2');
    let project, settings, lookup, columns = {}, preview, approved = false, timer;
    const defaults = [
        ['OpeningEquity', 'Opening equity account', 'Account'], ['ReceivableAccount', 'Default customer control', 'Account'], ['RevenueAccount', 'Default customer revenue', 'Account'], ['PayableAccount', 'Default supplier control', 'Account'], ['ExpenseAccount', 'Default supplier expense', 'Account'], ['SafeAccount', 'Undeposited cheque account', 'Account'],
        ['CustomerGroup', 'Default customer group', 'CustomerGroup'], ['SupplierGroup', 'Default supplier group', 'SupplierGroup'], ['PriceLevel', 'Default price level', 'PriceLevel'], ['CustomerPaymentTerm', 'Default customer payment term', 'PaymentTerm'], ['SupplierPaymentTerm', 'Default supplier payment term', 'PaymentTerm'], ['StockUnit', 'Approved base unit when source unit is missing', 'Unit'], ['StockWarehouse', 'Approved warehouse when source location is missing', 'Warehouse']
    ];
    function choices(type) { return lookup[type[0].toLowerCase() + type.slice(1)] || lookup.lookups.filter(x => x.type === type); }
    function options(type, selected = '') { return '<option value="">Choose a master</option>' + choices(type).map(x => `<option value="${x.id}" ${String(selected) === String(x.id) ? 'selected' : ''}>${esc(x.code)} — ${esc(x.label)}</option>`).join(''); }
    function message(text, error = false) { $('mcMessage').textContent = text; $('mcMessage').className = `alert alert-${error ? 'danger' : 'success'}`; $('mcMessage').scrollIntoView({ behavior: 'smooth', block: 'nearest' }); }
    async function api(path = '', method = 'GET', body) {
        const headers = {}; const args = { method, headers, credentials: 'same-origin' };
        if (method !== 'GET') headers.RequestVerificationToken = root.querySelector('[name="__RequestVerificationToken"]').value;
        if (body instanceof FormData) args.body = body;
        else if (body !== undefined) { headers['Content-Type'] = 'application/json'; args.body = JSON.stringify(body); }
        const response = await fetch('/api/migration/projects' + path, args);
        const data = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(data.message || data.title || `Request failed (${response.status}).`);
        return data;
    }
    async function perform(action) { try { await action(); } catch (e) { message(e.message, true); } }
    function invalidate() { preview = null; approved = false; $('mcApprove').disabled = true; $('mcExecute').disabled = true; $('mcPreview').innerHTML = '<p class="text-muted">Selection or project changed. Validate again to review the current import.</p>'; }
    function enable() {
        const readonly = project?.project.status === 'Finalized';
        ['mcNew', 'mcCreate', 'mcUpload', 'mcPackage', 'mcSaveMaps', 'mcAddMap', 'mcAddColumn', 'mcValidate', 'mcNewScope'].forEach(id => { const el = $(id); if (el.tagName === 'FORM') el.querySelectorAll('input,select,textarea,button').forEach(x => x.disabled = readonly || root.dataset.prepare !== 'true'); else el.disabled = readonly || root.dataset.prepare !== 'true'; });
        $('mcApprove').disabled = readonly || !preview?.canApprove || root.dataset.approve !== 'true';
        $('mcExecute').disabled = readonly || !approved || root.dataset.execute !== 'true';
        $('mcFinalize').disabled = readonly || root.dataset.finalize !== 'true';
        $('mcBulkRelease').disabled = readonly || root.dataset.approve !== 'true';
        $('mcNewScope').querySelectorAll('input,select,textarea,button').forEach(x => x.disabled = readonly || root.dataset.prepare !== 'true' || root.dataset.approve !== 'true');
        if (readonly) $('mcWorkspace').querySelectorAll('input,select,textarea').forEach(x => x.disabled = true);
    }
    async function projects(selected) {
        const list = await api(); $('mcProject').innerHTML = '<option value="">Choose a project</option>' + list.map(p => `<option value="${p.id}">${esc(p.name)} — ${esc(p.status)}</option>`).join('');
        if (selected) { $('mcProject').value = selected; await load(); }
    }
    async function load(progressOnly = false) {
        const id = $('mcProject').value; if (!id) { $('mcWorkspace').classList.add('d-none'); return; }
        project = await api('/' + id); settings = project.settings;
        $('mcWorkspace').classList.remove('d-none'); $('mcProjectSummary').textContent = `${project.project.sourceSystem} · LKR · Opening ${project.project.openingDate.slice(0, 10)} · Legacy closing ${project.project.closingDate.slice(0, 10)} · Asia/Colombo · ${project.project.status}`;
        $('mcFiles').innerHTML = project.datasets.map(d => `<tr><td>${esc(pretty(d.module))}</td><td>${d.revision}</td><td>${esc(d.closingDate.slice(0, 10))}</td><td><a href="/api/migration/projects/${id}/source/${d.id}">${esc(d.fileName)}</a></td><td>${d.isCurrent ? 'Current' : 'Retained revision'}</td></tr>`).join('');
        $('mcScopeRows').innerHTML = project.scopes.map(s => `<tr><td>${esc(s.kind)}</td><td>${esc(s.code)}</td><td><span class="badge ${s.status === 'Pending' ? 'bg-warning text-dark' : 'bg-success'}">${esc(pretty(s.status))}</span></td><td>${esc(JSON.parse(s.requiredModulesJson).map(pretty).join(', '))}</td><td><button class="btn btn-sm btn-outline-primary" type="button" data-scope="${s.id}">Reconcile</button></td></tr>`).join('');
        $('mcRuns').innerHTML = project.runs.length ? project.runs.map(r => `<div class="border rounded p-2 mb-2"><strong>Run ${r.id}: ${esc(r.status)}</strong> · ${r.committedUnits} committed units${r.error ? `<p class="text-danger mb-1">${esc(r.error)}</p>` : ''}${['Failed', 'Running'].includes(r.status) && root.dataset.execute === 'true' ? `<button class="btn btn-sm btn-outline-primary" data-resume="${r.id}" type="button">Resume Run</button>` : ''}</div>`).join('') : '<p class="text-muted">No posting runs yet.</p>';
        $('mcUnits').innerHTML = project.units.map(u => `<div class="border-bottom py-2">Unit ${u.id} · ${esc(pretty(u.module))}/${esc(u.scopeKey)} · ${esc(u.status)} · Journal ${esc(u.journalEntryId ?? 'none')}${u.status === 'Posted' && root.dataset.execute === 'true' && project.project.status !== 'Finalized' ? `<button class="btn btn-sm btn-outline-danger ms-2" type="button" data-reverse="${u.id}">Review Reversal</button>` : ''}</div>`).join('');
        $('mcAudit').innerHTML = project.audit.map(a => `<p>${esc(a.createdAt)} · ${esc(a.createdBy)} · <strong>${esc(a.action)}</strong><br>${esc(a.detail)}</p>`).join('');
        if (!progressOnly) {
            columns = await api('/' + id + '/columns'); invalidate(); renderSettings(); renderColumns();
            $('mcModules').innerHTML = lookup.modules.map(m => `<label class="form-check"><input class="form-check-input" type="checkbox" value="${esc(m)}" ${settings.datasetStates[m] === 'Provided' && (m !== 'TrialBalance' || project.scopes.every(s => ['Released', 'NoLegacyOpening'].includes(s.status))) ? 'checked' : ''}> ${esc(pretty(m))}</label>`).join('');
        }
        clearTimeout(timer); if (project.runs.some(x => ['Running', 'Queued'].includes(x.status))) timer = setTimeout(() => perform(() => load(true)), 5000);
        enable();
    }
    function renderSettings() {
        $('mcDefaults').innerHTML = defaults.map(([key, label, type]) => `<div class="col-md-4"><label for="mcDefault${key}" class="form-label">${esc(label)}</label><select id="mcDefault${key}" class="form-select" data-default="${key}">${options(type, key === 'OpeningEquity' ? settings.openingEquityAccountId : settings.defaults[key])}</select></div>`).join('');
        $('mcDataStates').innerHTML = lookup.modules.map(m => `<div class="col-md-4"><label for="mcState${m}" class="form-label">${esc(pretty(m))}</label><select id="mcState${m}" class="form-select" data-state="${m}">${['AwaitingData', 'Provided', 'NotApplicable'].map(state => `<option value="${state}" ${state === settings.datasetStates[m] ? 'selected' : ''}>${esc(pretty(state))}</option>`).join('')}</select><input class="form-control mt-1" aria-label="${esc(pretty(m))} zero declaration evidence" data-zero="${m}" value="${esc(settings.zeroEvidence[m] || '')}" placeholder="Signed zero / not applicable evidence"></div>`).join('');
        $('mcSummaryStock').value = settings.summarizedStockEvidence; $('mcFgValuation').value = settings.finishedGoodsValuation; $('mcFgEvidence').value = settings.finishedGoodsValuationEvidence; $('mcSupplierDays').value = settings.supplierTermDays ?? '';
        renderMaps();
    }
    function renderMaps() { $('mcMapList').innerHTML = Object.entries(settings.mappings).flatMap(([type, pairs]) => Object.entries(pairs).map(([code, id]) => `<div>${esc(type)}: <strong>${esc(code)}</strong> → ${esc(choices(type).find(x => x.id === id)?.label || id)}</div>`)).join('') || 'No explicit mappings saved.'; }
    function renderColumns() {
        const m = $('mcColumnModule').value; $('mcCanonical').innerHTML = (lookup.fields[m] || []).map(x => `<option>${esc(x)}</option>`).join(''); $('mcSourceColumn').innerHTML = (columns[m] || []).map(x => `<option>${esc(x)}</option>`).join('');
        $('mcColumnList').innerHTML = Object.entries(settings?.columns || {}).flatMap(([module, map]) => Object.entries(map).map(([canonical, source]) => `<div>${esc(module)}: ${esc(source)} → ${esc(canonical)}</div>`)).join('');
    }
    const codes = id => [...new Set($(id).value.split(/[,\n\r]+/).map(x => x.trim().toUpperCase()).filter(Boolean))];
    function selection() { return { modules: [...$('mcModules').querySelectorAll('input:checked')].map(x => x.value), customers: codes('mcCustomers'), suppliers: codes('mcSuppliers'), skus: codes('mcSkus'), banks: codes('mcBanks') }; }
    function errors(items) { return items.length ? `<ul class="text-danger">${items.map(x => `<li>${esc(x.module)} / ${esc(x.scope)} row ${x.row}: ${esc(x.message)}</li>`).join('')}</ul>` : ''; }
    function previewMessage() { return preview.errors.length || preview.units.some(u => u.errors.length) ? 'Resolve the validation errors.' : preview.canApprove ? 'Ready for approval.' : preview.units.length ? 'All selected rows are already imported; no new posting is needed.' : 'Selected datasets are declared not applicable; there is nothing to post.'; }
    function renderPreview() {
        $('mcPreview').innerHTML = `<p>${preview.units.length} scope units. ${previewMessage()} Review hash: <code>${esc(preview.hash)}</code></p>` + errors(preview.errors) + preview.units.map(u => {
            const fields = [...new Set(u.rows.flatMap(r => Object.keys(r.data)))].filter(x => !x.startsWith('_'));
            return `<details class="border rounded p-2 mb-2"><summary><strong>${esc(pretty(u.module))}/${esc(u.scope)}</strong> · ${u.rows.length} rows · ${esc(u.status)} · ${u.errors.length} errors</summary>${errors(u.errors)}${u.warnings.length ? `<ul class="text-warning-emphasis">${u.warnings.map(w => `<li>${esc(w)}</li>`).join('')}</ul>` : ''}<h6 class="mt-3">Proposed opening journal (LKR)</h6><table class="table table-sm"><thead><tr><th>Account</th><th>Debit</th><th>Credit</th></tr></thead><tbody>${u.journal.map(l => `<tr><td>${esc(choices('Account').find(x => x.id === l.accountId)?.label || l.accountId)}</td><td>${l.debit.toLocaleString('en-LK', { minimumFractionDigits: 2 })}</td><td>${l.credit.toLocaleString('en-LK', { minimumFractionDigits: 2 })}</td></tr>`).join('')}</tbody></table><h6>Normalized rows</h6><div class="table-responsive" style="max-height:420px"><table class="table table-sm"><thead><tr><th>Source row</th>${fields.map(f => `<th>${esc(pretty(f))}</th>`).join('')}</tr></thead><tbody>${u.rows.map(r => `<tr><td>${r.row}</td>${fields.map(f => `<td>${esc(r.data[f])}</td>`).join('')}</tr>`).join('')}</tbody></table></div></details>`;
        }).join(''); enable();
    }
    async function reconcile(scope) {
        const data = await api(`/${project.project.id}/reconciliation/${encodeURIComponent(scope.kind)}/${encodeURIComponent(scope.code)}`);
        $('mcReconcile').innerHTML = `<h6>${esc(scope.kind)}/${esc(scope.code)} reconciliation (LKR or base-unit quantity)</h6><div class="table-responsive"><table class="table table-sm"><thead><tr><th>Component</th><th>Source</th><th>Posted</th><th>Difference</th><th>Independent legacy control</th><th>Legacy row count</th></tr></thead><tbody>${data.map((c, i) => `<tr><td>${esc(c.key)}</td><td>${c.source}</td><td>${c.posted}</td><td>${c.difference}</td><td><input id="mcControl${i}" class="form-control" type="number" step="0.0001" aria-label="${esc(c.key)} independent legacy total" required></td><td><input id="mcCount${i}" class="form-control" type="number" min="0" step="1" aria-label="${esc(c.key)} legacy row count" required></td></tr>`).join('')}</tbody></table></div><label for="mcReleaseEvidence" class="form-label">Complete scope and control evidence</label><textarea id="mcReleaseEvidence" class="form-control" rows="2"></textarea><button id="mcRelease" class="btn btn-success mt-2" type="button" ${scope.status !== 'Pending' || root.dataset.approve !== 'true' || project.project.status === 'Finalized' ? 'disabled' : ''}>Release Reconciled Scope</button>`;
        $('mcRelease').onclick = () => perform(async () => {
            const controls = {}, rowCounts = {}; data.forEach((c, i) => { if ($('mcControl' + i).value === '' || $('mcCount' + i).value === '') throw new Error('Enter every independent control and row count.'); controls[c.key] = Number($('mcControl' + i).value); rowCounts[c.key] = Number($('mcCount' + i).value); });
            await api(`/${project.project.id}/release/${encodeURIComponent(scope.kind)}/${encodeURIComponent(scope.code)}`, 'POST', { evidence: $('mcReleaseEvidence').value, controls, rowCounts }); $('mcReconcile').innerHTML = ''; await load(); message('Scope reconciled and released for trading.');
        }); $('mcReconcile').scrollIntoView({ behavior: 'smooth' });
    }
    $('mcNew').onclick = () => $('mcCreate').classList.toggle('d-none');
    $('mcProject').onchange = () => perform(() => load()); $('mcRefresh').onclick = () => perform(() => load(true));
    $('mcCreate').onsubmit = e => { e.preventDefault(); perform(async () => { const p = await api('', 'POST', { name: $('mcName').value, sourceSystem: $('mcSource').value, openingDate: $('mcOpening').value, adoptionEvidence: $('mcAdoption').value }); $('mcCreate').classList.add('d-none'); await projects(p.id); message('Project created. Opening scopes remain locked until reconciled and released.'); }); };
    $('mcUpload').onsubmit = e => { e.preventDefault(); perform(async () => { const body = new FormData(); body.append('module', $('mcModule').value); body.append('closingDate', project.project.closingDate); body.append('file', $('mcFile').files[0]); await api('/' + project.project.id + '/upload', 'POST', body); $('mcFile').value = ''; await load(); message('Dataset staged. Review mappings and validate before posting.'); }); };
    $('mcPackage').onsubmit = e => { e.preventDefault(); perform(async () => { const body = new FormData(); body.append('file', $('mcZip').files[0]); await api('/' + project.project.id + '/package', 'POST', body); $('mcZip').value = ''; await load(); message('Package datasets staged. No business balances were posted.'); }); };
    $('mcMapType').onchange = () => { $('mcTarget').innerHTML = options($('mcMapType').value); };
    $('mcAddMap').onclick = () => perform(async () => { const type = $('mcMapType').value, code = $('mcLegacy').value.trim().toUpperCase(), id = Number($('mcTarget').value); if (!code || !id) throw new Error('Enter the exact legacy code and choose a master.'); settings.mappings[type] ||= {}; settings.mappings[type][code] = id; renderMaps(); invalidate(); message('Mapping added locally. Save Mappings and Evidence to persist it.'); });
    $('mcColumnModule').onchange = renderColumns;
    $('mcAddColumn').onclick = () => perform(async () => { const m = $('mcColumnModule').value, field = $('mcCanonical').value, source = $('mcSourceColumn').value; if (!source) throw new Error('Upload a dataset and choose a source column.'); settings.columns[m] ||= {}; settings.columns[m][field] = source; renderColumns(); invalidate(); message('Column profile changed locally. Save mappings to persist it.'); });
    $('mcSaveMaps').onclick = () => perform(async () => {
        $('mcDefaults').querySelectorAll('[data-default]').forEach(el => { if (el.dataset.default === 'OpeningEquity') settings.openingEquityAccountId = Number(el.value); else settings.defaults[el.dataset.default] = Number(el.value); });
        $('mcDataStates').querySelectorAll('[data-state]').forEach(el => settings.datasetStates[el.dataset.state] = el.value); $('mcDataStates').querySelectorAll('[data-zero]').forEach(el => settings.zeroEvidence[el.dataset.zero] = el.value);
        settings.summarizedStockEvidence = $('mcSummaryStock').value; settings.finishedGoodsValuation = $('mcFgValuation').value; settings.finishedGoodsValuationEvidence = $('mcFgEvidence').value; settings.supplierTermDays = $('mcSupplierDays').value === '' ? null : Number($('mcSupplierDays').value);
        await api('/' + project.project.id + '/mappings', 'PUT', settings); await load(); message('Mappings and evidence saved. Previous approval is invalidated.');
    });
    $('mcSelection').oninput = invalidate;
    $('mcValidate').onclick = () => perform(async () => { preview = await api('/' + project.project.id + '/validate', 'POST', selection()); approved = false; renderPreview(); message(preview.canApprove ? 'Selected scopes validated. Review the normalized rows and journals.' : previewMessage(), preview.errors.length > 0 || preview.units.some(u => u.errors.length > 0)); });
    $('mcApprove').onclick = () => perform(async () => { await api('/' + project.project.id + '/approve', 'POST', { selection: selection(), hash: preview.hash, evidence: $('mcApprovalEvidence').value }); approved = true; enable(); message('Selected import approved. Ready to execute.'); });
    $('mcExecute').onclick = () => perform(async () => { const run = await api('/' + project.project.id + '/execute', 'POST', selection()); approved = false; await load(true); message(`Run ${run.runId} queued. Follow progress below.`); });
    $('mcRuns').onclick = e => { const id = e.target.dataset.resume; if (id) perform(async () => { await api(`/${project.project.id}/runs/${id}/resume`, 'POST', {}); await load(true); message('Run queued to resume. Committed units will be recognized.'); }); };
    $('mcScopeRows').onclick = e => { const id = Number(e.target.dataset.scope); if (id) perform(() => reconcile(project.scopes.find(x => x.id === id))); };
    $('mcNewScope').onsubmit = e => { e.preventDefault(); perform(async () => { const kind = $('mcScopeKind').value; const requirements = { Customer: ['CustomerOpenings', 'ReceivedCheques'], Supplier: ['SupplierOpenings', 'IssuedCheques'], Stock: ['Stock'], Bank: ['BankOpening', 'BankRecon'] }; await api('/' + project.project.id + '/scope', 'PUT', { kind, code: $('mcScopeCode').value, requiredModules: requirements[kind], evidence: $('mcScopeEvidence').value, noLegacyOpening: true }); await load(); message('New-system entity classified and released with no legacy opening.'); }); };
    $('mcBulkRelease').onclick = () => perform(async () => { const result = await api('/' + project.project.id + '/release-selected', 'POST', { selection: selection(), evidence: $('mcBulkEvidence').value }); await load(); $('mcBulkResults').textContent = `${result.released} scopes released. ${result.pending.join(' ')}`; message(`${result.released} scopes released; ${result.pending.length} remain pending.`); });
    $('mcLedger').onclick = () => perform(async () => { const controls = await api('/' + project.project.id + '/reconciliation'); $('mcLedgerResults').innerHTML = `<table class="table table-sm"><thead><tr><th>Account</th><th>Legacy closing</th><th>Migration opening</th><th>Difference</th></tr></thead><tbody>${controls.map(c => `<tr><td>${esc(c.key)}</td><td>${c.source}</td><td>${c.posted}</td><td class="${c.difference ? 'text-danger' : 'text-success'}">${c.difference}</td></tr>`).join('')}</tbody></table>`; });
    $('mcFinalize').onclick = () => perform(async () => { await api('/' + project.project.id + '/finalize', 'POST', { evidence: $('mcFinalEvidence').value, controls: {}, rowCounts: {} }); await load(); message('Migration finalized. The project is read-only and financial reports are no longer provisional.'); });
    $('mcUnits').onclick = e => { const id = e.target.dataset.reverse; if (!id) return; const panel = document.createElement('form'); panel.className = 'border rounded p-3 my-2'; panel.innerHTML = `<strong>Review reversal of unit ${esc(id)}</strong><p>Only unused, unreleased units can be reversed. Original evidence is retained. Downstream activity requires a dated correction.</p><label class="form-label">Reversal date<input class="form-control" name="date" type="date" required></label><label class="form-label ms-2">Reason<input class="form-control" name="reason" required></label><button class="btn btn-danger ms-2" type="submit">Reverse Unused Unit</button>`; panel.onsubmit = event => { event.preventDefault(); perform(async () => { await api(`/${project.project.id}/units/${id}/reverse`, 'POST', { date: panel.elements.date.value, reason: panel.elements.reason.value }); await load(); message('Unused unit reversed. Its original records and opposite journal remain in the audit.'); }); }; e.target.parentElement.append(panel); };
    perform(async () => { lookup = await api('/lookups'); ['mcModule', 'mcColumnModule'].forEach(id => $(id).innerHTML = lookup.modules.map(m => `<option value="${m}">${esc(pretty(m))}</option>`).join('')); $('mcMapType').innerHTML = ['Account', 'Customer', 'Supplier', 'Stock', 'Warehouse', 'Unit', 'Brand', 'Category', 'BankBranch', 'CustomerGroup', 'SupplierGroup', 'PriceLevel', 'PaymentTerm'].map(type => `<option>${type}</option>`).join(''); $('mcMapType').onchange(); await projects(); enable(); });
})();
