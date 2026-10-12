(() => {
    const root = document.getElementById('openingBankItems'); if (!root) return;
    const esc = x => String(x ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const msg = text => document.getElementById('obiMessage').textContent = text;
    async function load() {
        try { const response = await fetch('/api/treasury/opening-bank-items'); if (!response.ok) throw new Error('Opening bank items could not be loaded.'); const items = await response.json();
            document.getElementById('obiRows').innerHTML = items.map(x => `<tr><td>${esc(x.reference)}${x.chequeNumber ? `<div class="small">Cheque ${esc(x.chequeNumber)} · ${esc(x.chequeDate?.slice(0, 10))}</div>` : ""}</td><td>${esc(x.kind)}</td><td>${esc(x.date.slice(0, 10))}</td><td>${x.amount.toLocaleString('en-LK', { minimumFractionDigits: 2 })}</td><td><form data-item="${x.id}"><input name="date" class="form-control form-control-sm" type="date" required aria-label="Resolution date for ${esc(x.reference)}"><input name="reason" class="form-control form-control-sm mt-1" placeholder="Reason, required for cancellation" aria-label="Resolution reason"><button class="btn btn-sm btn-outline-success mt-1" name="action" value="clear" type="submit">Mark Cleared</button>${x.kind === 'IssuedCheque' ? '<button class="btn btn-sm btn-outline-danger mt-1 ms-1" name="action" value="cancel" type="submit">Cancel Issued Cheque</button>' : ''}</form></td></tr>`).join(''); if (!items.length) msg('No unresolved opening bank items.');
        } catch (e) { msg(e.message); }
    }
    document.getElementById('obiRefresh').onclick = load;
    root.addEventListener('submit', async e => { e.preventDefault(); const form = e.target; if (!form.dataset.item) return; try {
        const response = await fetch(`/api/treasury/opening-bank-items/${form.dataset.item}/resolve`, { method: 'POST', headers: { 'Content-Type': 'application/json', RequestVerificationToken: root.querySelector('[name="__RequestVerificationToken"]').value }, body: JSON.stringify({ date: form.elements.date.value, reason: form.elements.reason.value, cancel: e.submitter?.value === 'cancel' }) }); const data = await response.json(); if (!response.ok) throw new Error(data.message || 'Resolution failed.'); await load(); msg('Opening bank item resolved.');
    } catch (err) { msg(err.message); } }); load();
})();
