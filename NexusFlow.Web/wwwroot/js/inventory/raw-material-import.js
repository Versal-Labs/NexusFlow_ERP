(() => {
    'use strict';
    const el = id => document.getElementById(id);
    const form = el('rawMaterialImportForm');
    let preview = null;
    let busy = false;
    let completed = false;
    const number = value => Number(value).toLocaleString(undefined, { maximumFractionDigits: 2 });
    const money = value => Number(value).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    function message(text, kind = 'danger') {
        el('importMessage').className = `alert alert-${kind}`;
        el('importMessage').textContent = text;
    }
    function syncButtons() {
        el('postImport').disabled = busy || completed || !preview?.canImport || !el('confirmImport').checked;
        el('confirmImport').disabled = busy || completed || !preview?.canImport;
    }
    function invalidate() {
        preview = null;
        el('confirmImport').checked = false;
        el('importPreview').classList.add('d-none');
        syncButtons();
    }
    function list(id, items) {
        el(id).replaceChildren(...items.map(text => {
            const li = document.createElement('li');
            li.textContent = text;
            return li;
        }));
    }
    function render(data) {
        preview = data;
        el('importPreview').classList.remove('d-none');
        el('importTotals').textContent = `${data.rows.length} valid rows · ${number(data.totalQuantity)} PCS · Opening value ${money(data.totalValue)} (base currency)`;
        el('importMappings').replaceChildren();
        const mappings = {
            'Opening date': data.openingDate.slice(0, 10), Warehouse: data.warehouse, Brand: data.brand, Unit: data.unit,
            Category: `${data.category}${data.createsCategory ? ' (will be created)' : ' (existing)'}`,
            'Inventory debit': data.inventoryAccount, 'Opening equity credit': data.equityAccount,
            'Category cost account (no posting now)': data.costAccount,
            'Required category revenue account (no sale posted)': data.salesAccount
        };
        for (const [label, value] of Object.entries(mappings)) {
            const dt = document.createElement('dt'); dt.className = 'col-sm-5'; dt.textContent = label;
            const dd = document.createElement('dd'); dd.className = 'col-sm-7'; dd.textContent = value;
            el('importMappings').append(dt, dd);
        }
        list('importWarnings', data.warnings);
        list('importErrorList', data.errors);
        el('importErrors').classList.toggle('d-none', data.errors.length === 0);
        el('importRows').replaceChildren(...data.rows.map(row => {
            const tr = document.createElement('tr');
            [row.row, row.sku, row.productName, number(row.quantity), money(row.unitCost), money(row.value)].forEach((value, index) => {
                const td = document.createElement('td'); td.textContent = value;
                if (index > 2) td.className = 'text-end';
                tr.append(td);
            });
            return tr;
        }));
        el('confirmImport').checked = false;
    }
    async function submit(execute) {
        if (busy || completed || !form.reportValidity()) return;
        if (execute && (!preview?.canImport || !el('confirmImport').checked)) return;
        const file = el('importFile').files[0];
        if (!file || !/\.csv$/i.test(file.name) || !file.size || file.size > 2 * 1024 * 1024) {
            invalidate(); message('Choose a non-empty CSV file up to 2 MB.'); return;
        }
        // Capture the file/settings before disabling the fieldset. Disabled inputs are excluded from FormData.
        const body = new FormData(form);
        if (execute) {
            body.set('PreviewHash', preview.previewHash);
            body.set('Confirmed', 'true');
        } else invalidate();
        busy = true; el('importFields').disabled = true; syncButtons();
        message(execute ? 'Importing and posting opening stock. Please keep this page open.' : 'Validating CSV and account mappings…', 'info');
        try {
            const response = await fetch(`/api/inventory/raw-material-import/${execute ? 'execute' : 'preview'}`, {
                method: 'POST', body, credentials: 'same-origin',
                headers: { RequestVerificationToken: form.querySelector('[name="__RequestVerificationToken"]').value }
            });
            if (!response.headers.get('content-type')?.includes('application/json'))
                throw new Error('Unexpected response. Your session may have expired.');
            const result = await response.json();
            if (!response.ok || !result.succeeded)
                throw new Error(result.message || result.title || 'The request failed. Check your permissions and input.');
            render(result.data);
            if (execute) {
                completed = true;
                message(`${result.message} Total quantity: ${number(result.data.totalQuantity)} PCS. Total value: ${money(result.data.totalValue)}.`, 'success');
            } else message(result.data.canImport ? 'Preview passed. Review all rows and mappings before posting.' : 'Validation found errors. Nothing has been saved.', result.data.canImport ? 'info' : 'danger');
        } catch (error) {
            invalidate();
            message(`${error.message}${execute ? ' If the connection was interrupted, check Opening Stock before retrying. Preview again; existing SKUs will block duplicate posting.' : ''}`);
        } finally {
            busy = false; el('importFields').disabled = completed; syncButtons();
        }
    }
    form.addEventListener('submit', event => { event.preventDefault(); submit(false); });
    form.addEventListener('input', invalidate);
    form.addEventListener('change', invalidate);
    el('confirmImport').addEventListener('change', syncButtons);
    el('postImport').addEventListener('click', () => submit(true));
    const today = new Date();
    el('importDate').value = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
    async function init() {
        try {
            const response = await fetch('/api/inventory/raw-material-import/setup', { credentials: 'same-origin' });
            if (!response.ok) throw new Error('Unable to load setup. Check your login and permissions.');
            const data = await response.json();
            function options(id, values, preferred) {
                el(id).replaceChildren(new Option('-- Select --', ''));
                values.forEach(([value, label]) => el(id).add(new Option(label, value)));
                const match = values.find(([value]) => value.toUpperCase() === preferred);
                if (match) el(id).value = match[0];
            }
            options('importWarehouse', data.warehouses.map(x => [x.code, `${x.code} — ${x.name}`]), 'WH-MAIN');
            options('importBrand', data.brands.map(x => [x, x]), 'UNBRANDED');
            el('previewImport').disabled = false;
        } catch (error) { message(error.message); }
    }
    syncButtons();
    init();
})();
