(() => {
    'use strict';
    const root = '/api/finance/customer-migration', $id = id => document.getElementById(id);
    let setup, review, master, reconciled = false;
    const date = value => value ? value.slice(0, 10) : '', money = value => Number(value).toLocaleString('en-LK', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    async function request(path, body) {
        const headers = { 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value };
        if (body && !(body instanceof FormData)) headers['Content-Type'] = 'application/json';
        const response = await fetch(root + path, { method: body ? 'POST' : 'GET', headers, body: body instanceof FormData ? body : body ? JSON.stringify(body) : undefined });
        const result = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(result.message || `Request failed (${response.status}).`);
        return result;
    }
    function message(text, error = false) { $id('migrationMessage').textContent = text; $id('migrationMessage').className = `alert alert-${error ? 'danger' : 'success'}`; }
    function list(id, entries) { const el = $id(id); el.replaceChildren(); entries.forEach(text => { const li = document.createElement('li'); li.textContent = text; el.append(li); }); }
    function rows(id, values) { const body = $id(id); body.replaceChildren(); values.forEach(values => { const tr = document.createElement('tr'); values.forEach(value => { const td = document.createElement('td'); td.textContent = value; tr.append(td); }); body.append(tr); }); }
    function button(id, action) { $id(id).addEventListener('click', async () => { const el = $id(id); el.disabled = true; try { await action(); } catch (e) { message(e.message, true); } finally { el.disabled = false; updateButtons(); } }); }
    function updateButtons() {
        $id('postMigration').disabled = !review?.canPost || !$id('physicalConfirmed').checked;
        $id('reviewMappings').disabled = review?.status !== 'Draft';
        $id('signOffBatch').disabled = !reconciled || review?.status !== 'Posted' || review?.isSignedOff;
        $id('importMasters').disabled = !master || master.errors.length > 0 || !$id('masterConfirmed').checked;
    }
    async function refresh() {
        setup = await request('/setup');
        $id('masterLookup').textContent = JSON.stringify({ lookups: setup.lookups, accounts: setup.accounts, salesReps: setup.salesReps }, null, 2);
        const selectedBatch = $id('savedBatch').value;
        const batches = await request('/batches'); $id('savedBatch').replaceChildren();
        batches.forEach(batch => { const option = document.createElement('option'); option.value = batch.id; option.textContent = `#${batch.id} ${batch.status} ${date(batch.cutoverDate)}${batch.isSignedOff ? ' (signed off)' : ''}`; $id('savedBatch').append(option); });
        if (batches.some(x => String(x.id) === selectedBatch)) $id('savedBatch').value = selectedBatch;
    }
    function render(data) {
        review = data; reconciled = false; $id('physicalConfirmed').checked = false; $id('migrationReview').classList.remove('d-none');
        $id('batchSummary').textContent = `Batch #${data.batchId}: ${data.status}${data.isSignedOff ? '; signed off' : ''}. Cutover ${date(data.cutoverDate)}, closing ${date(data.sourceAsOfDate)}.`;
        $id('postingStatus').textContent = (setup.signedOff || data.isSignedOff) ? 'Migration is signed off and locked.' : setup.postingEnabled ? 'Migration posting enabled. Resolve every error and revalidate before posting.' : 'Migration posting is disabled. Enable after UAT approval, then revalidate.';
        list('migrationErrors', data.errors); list('migrationWarnings', data.warnings);
        const bankBody = $id('bankMappings'); bankBody.replaceChildren();
        [...new Set(data.source.cheques.map(x => x.bankCode))].forEach(code => {
            const tr = document.createElement('tr'), label = document.createElement('td'), cell = document.createElement('td'), select = document.createElement('select');
            label.textContent = code; select.className = 'form-select form-select-sm'; select.dataset.code = code; select.disabled = data.status !== 'Draft';
            select.append(new Option('-- Verify branch --', '')); setup.branches.forEach(branch => select.append(new Option(branch.label, branch.id)));
            select.value = data.banks.find(x => x.code === code)?.bankBranchId || ''; select.addEventListener('change', () => { review.canPost = false; updateButtons(); });
            cell.append(select); tr.append(label, cell); bankBody.append(tr);
        });
        const sum = field => data.totals.reduce((total, row) => total + row[field], 0);
        $id('openingTotals').textContent = `${data.source.outstanding.length} opening items; ${data.customers.length} mapped customers; ${data.source.cheques.length} cheques. Debts ${money(sum('debit'))}; return credits ${money(sum('credit'))}; net AR ${money(sum('netAr'))}; cheques ${money(sum('cheques'))} LKR.`;
        rows('customerTotals', data.totals.map(x => [x.code, x.name, money(x.debit), money(x.credit), money(x.netAr), money(x.cheques)]));
        rows('openingJournal', data.journal.map(x => [`${x.accountId}: ${x.accountName}`, money(x.debit), money(x.credit)]));
        rows('openingRows', data.source.outstanding.map(x => [x.row, x.code, x.reference, x.kind, date(x.date), money(x.amount)]));
        rows('chequeRows', data.source.cheques.map(x => [x.row, x.code, x.reference, x.chequeNumber, date(x.chequeDate), date(x.receivedDate), x.documentReferences, money(x.amount)]));
        $id('reviewMappings').disabled = data.status !== 'Draft'; $id('batchActions').classList.toggle('d-none', data.status === 'Draft');
        $id('signOffBatch').disabled = true; $id('reversePanel').classList.toggle('d-none', data.status !== 'Posted' || data.isSignedOff);
        $id('reverseDate').value = date(data.cutoverDate); $id('reconciliationResult').textContent = '';
        $id('downloadAudit').href = `${root}/${data.batchId}/audit.csv`;
        $id('downloadOutstanding').href = `${root}/${data.batchId}/source/outstanding`; $id('downloadCheques').href = `${root}/${data.batchId}/source/cheques`;
        updateButtons();
    }
    $id('migrationUpload').addEventListener('submit', async e => {
        e.preventDefault(); const button = e.submitter; button.disabled = true;
        try { const body = new FormData(); body.append('Outstanding', $id('outstandingFile').files[0]); body.append('Cheques', $id('chequeFile').files[0]); body.append('CutoverDate', $id('cutoverDate').value); body.append('SourceAsOfDate', $id('sourceDate').value); await refresh(); render(await request('/preview', body)); message('Preview saved. Review errors, mappings and totals.'); await refresh(); }
        catch (error) { message(error.message, true); } finally { button.disabled = false; }
    });
    ['outstandingFile', 'chequeFile', 'sourceDate', 'cutoverDate'].forEach(id => $id(id).addEventListener('change', () => { if (review) { review.canPost = false; updateButtons(); message('Upload settings changed. Validate and Preview again.'); } }));
    $id('physicalConfirmed').addEventListener('change', updateButtons); $id('masterConfirmed').addEventListener('change', updateButtons);
    $id('masterFile').addEventListener('change', () => { master = null; updateButtons(); });
    button('loadBatch', async () => { await refresh(); if ($id('savedBatch').value) render(await request('/' + $id('savedBatch').value)); });
    button('reviewMappings', async () => { const mappings = {}; $id('bankMappings').querySelectorAll('select').forEach(x => { if (x.value) mappings[x.dataset.code] = Number(x.value); }); await refresh(); render(await request(`/${review.batchId}/review`, mappings)); message('Mappings revalidated.'); });
    button('postMigration', async () => { if (!review?.canPost || !$id('physicalConfirmed').checked) return; const result = await request(`/${review.batchId}/post`, { reviewHash: review.reviewHash, physicallyVerified: true }); await refresh(); render(result); message('Customer opening posted. Reconcile the batch before sign-off.'); });
    button('reconcileBatch', async () => { const result = await request(`/${review.batchId}/reconciliation`); const container = $id('reconciliationResult'); container.replaceChildren();
        const summary = document.createElement('p'); summary.className = result.matches ? 'alert alert-success' : 'alert alert-danger';
        summary.textContent = `Opening position ${result.matches ? 'matches' : 'does not match'} the source. ${result.openingItemCount} items and ${result.chequeCount} cheques. Journal ${result.journalBalances ? 'balances' : 'does not balance'}.`; container.append(summary);
        const table = document.createElement('table'); table.className = 'table table-sm';
        const header = document.createElement('thead'), headRow = document.createElement('tr'); ['Legacy code','Customer','Source AR','Imported AR','Source cheques','Imported cheques'].forEach(label => { const th = document.createElement('th'); th.textContent = label; headRow.append(th); }); header.append(headRow);
        const body = document.createElement('tbody'); body.id = 'reconciledCustomers'; table.append(header, body); container.append(table);
        rows('reconciledCustomers', result.customers.map(x => [x.code,x.name,money(x.sourceAr),money(x.importedAr),money(x.sourceCheques),money(x.importedCheques)])); reconciled = result.matches; updateButtons(); });
    button('signOffBatch', async () => { if (!window.confirm('Sign off this reconciled migration and lock further posting and batch reversal?')) return; render(await request(`/${review.batchId}/sign-off`, {})); await refresh(); message('Migration signed off and locked.'); });
    button('reverseBatch', async () => { if (!$id('reverseReason').value.trim() || !window.confirm('Reverse this unused migration batch?')) return; render(await request(`/${review.batchId}/reverse`, { date: $id('reverseDate').value, reason: $id('reverseReason').value })); await refresh(); message('Batch reversed. Source records remain available for audit.'); });
    async function setting(enabled) { await request('/posting-setting', { enabled, uatApproved: $id('uatApproved').checked }); await refresh(); if (review) render(await request(`/${review.batchId}/review`, review.bankOverrides)); message(enabled ? 'Posting enabled. Revalidate and review before posting.' : 'Posting disabled.'); }
    button('enablePosting', () => setting(true)); button('disablePosting', () => setting(false));
    $id('masterUpload').addEventListener('submit', async e => { e.preventDefault(); const button = e.submitter; button.disabled = true; try { const body = new FormData(); body.append('File', $id('masterFile').files[0]); master = await request('/masters/preview', body); $id('masterConfirmed').checked = false; $id('masterPreview').classList.remove('d-none'); $id('masterSummary').textContent = `${master.customers.length} customers in preview.`; list('masterErrors', master.errors); rows('masterRows', master.customers.map(x => [x.id, x.legacyCustomerCode, x.name, x.contactPerson, x.phone, x.email, [x.addressLine1,x.city,x.district,x.province,x.country].filter(Boolean).join(", "), x.customerGroupId, x.priceLevelId, x.salesRepId, x.paymentTermId, x.creditLimit, x.creditPeriodDays, x.defaultReceivableAccountId, x.defaultRevenueAccountId, x.bankId, x.bankBranchId, x.bankAccountNumber])); updateButtons(); } catch (error) { message(error.message, true); } finally { button.disabled = false; } });
    button('importMasters', async () => { if (!master || master.errors.length || !$id('masterConfirmed').checked) return; const body = new FormData(); body.append('File', $id('masterFile').files[0]); body.append('PreviewHash', master.previewHash); body.append('Confirmed', 'true'); await request('/masters/import', body); master = null; message('Customer masters imported. Revalidate the financial batch.'); await refresh(); });
    refresh().catch(error => message(error.message, true));
})();
