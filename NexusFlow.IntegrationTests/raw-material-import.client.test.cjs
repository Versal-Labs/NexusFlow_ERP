const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../NexusFlow.Web/wwwroot/js/inventory/raw-material-import.js'), 'utf8');

// Minimal DOM doubles for client state tests, not a substitute for visual/browser verification.
function harness() {
    class Element {
        constructor() {
            this.children = []; this.events = {}; this.value = ''; this.checked = false; this.disabled = false;
            this.files = [{ name: 'stock.csv', size: 100 }];
            this.classes = new Set();
            this.classList = { add: x => this.classes.add(x), remove: x => this.classes.delete(x),
                toggle: (x, on) => on ? this.classes.add(x) : this.classes.delete(x) };
        }
        set innerHTML(_) { throw new Error('Untrusted CSV content must never use innerHTML'); }
        append(...children) { this.children.push(...children); }
        add(child) { this.append(child); }
        replaceChildren(...children) { this.children = children; }
        addEventListener(event, handler) { this.events[event] = handler; }
        emit(event) { this.events[event]?.({ preventDefault() {} }); }
        reportValidity() { return true; }
        querySelector() { return { value: 'test-csrf-token' }; }
    }
    const elements = new Map();
    const el = id => { if (!elements.has(id)) elements.set(id, new Element()); return elements.get(id); };
    const requests = [];
    const responses = [];
    const data = {
        canImport: true, previewHash: 'hash', rows: [{ row: 2, sku: 'RM-1', productName: '<img src=x onerror=alert(1)>', quantity: 2, unitCost: 3, value: 6 }],
        warnings: ['Check values'], errors: [], totalQuantity: 2, totalValue: 6, openingDate: '2026-09-15T00:00:00',
        warehouse: 'Main Warehouse', brand: 'UNBRANDED', category: 'Accessories', unit: 'PCS', createsCategory: true,
        inventoryAccount: '1131', equityAccount: '3200', costAccount: '5110', salesAccount: '4110'
    };
    const response = (payload, ok = true) => ({ ok, headers: { get: () => 'application/json' }, json: async () => payload });
    class FormDataDouble {
        constructor() {
            assert.equal(el('importFields').disabled, false, 'capture settings before disabling inputs');
            this.values = new Map();
        }
        set(key, value) { this.values.set(key, value); }
    }
    vm.runInNewContext(source, {
        document: { getElementById: el, createElement: () => new Element() },
        Date, Number, String, Object, Option: function(label, value) { this.label = label; this.value = value; },
        FormData: FormDataDouble,
        fetch: async (url, options) => {
            requests.push({ url, options });
            if (url.endsWith('/setup')) return response({ warehouses: [{ code: 'WH-MAIN', name: 'Main Warehouse' }], brands: ['UNBRANDED'] });
            return responses.length ? await responses.shift() : response({ succeeded: true, data, message: 'Done' });
        }
    });
    return { el, requests, responses, data, response };
}
const settle = () => new Promise(resolve => setImmediate(resolve));

test('setup defaults, safe text rendering, confirmation and edit invalidation', async () => {
    const h = harness(); await settle();
    assert.equal(h.el('importWarehouse').value, 'WH-MAIN');
    assert.equal(h.el('importBrand').value, 'UNBRANDED');
    assert.equal(h.el('previewImport').disabled, false);
    assert.equal(h.el('postImport').disabled, true);
    h.el('rawMaterialImportForm').emit('submit'); await settle();
    assert.equal(h.el('importRows').children[0].children[2].textContent, h.data.rows[0].productName);
    assert.equal(h.el('postImport').disabled, true);
    h.el('confirmImport').checked = true; h.el('confirmImport').emit('change');
    assert.equal(h.el('postImport').disabled, false);
    h.el('rawMaterialImportForm').emit('input');
    assert.equal(h.el('postImport').disabled, true);
    assert.equal(h.el('confirmImport').checked, false);
    assert.equal(h.el('importPreview').classes.has('d-none'), true);
});

test('posting captures hash, locks controls, blocks double submits and locks after success', async () => {
    const h = harness(); await settle(); h.el('rawMaterialImportForm').emit('submit'); await settle();
    h.el('confirmImport').checked = true; h.el('confirmImport').emit('change');
    let finish;
    h.responses.push(new Promise(resolve => { finish = resolve; }));
    h.el('postImport').emit('click'); h.el('postImport').emit('click');
    assert.equal(h.requests.filter(x => x.url.endsWith('/execute')).length, 1);
    assert.equal(h.el('importFields').disabled, true);
    const req = h.requests.at(-1);
    assert.equal(req.options.body.values.get('PreviewHash'), 'hash');
    assert.equal(req.options.body.values.get('Confirmed'), 'true');
    assert.equal(req.options.headers.RequestVerificationToken, 'test-csrf-token');
    finish(h.response({ succeeded: true, data: h.data, message: 'Posted OBSTK-1' })); await settle();
    assert.equal(h.el('importFields').disabled, true);
    assert.equal(h.el('postImport').disabled, true);
    assert.match(h.el('importMessage').textContent, /Posted OBSTK-1/);
});

test('invalid preview and failed posting cannot be submitted again without another preview', async () => {
    const h = harness(); await settle();
    h.responses.push(h.response({ succeeded: true, data: { ...h.data, canImport: false, errors: ['Duplicate SKU'] } }));
    h.el('rawMaterialImportForm').emit('submit'); await settle();
    assert.equal(h.el('postImport').disabled, true);
    assert.equal(h.el('confirmImport').disabled, true);
    h.el('rawMaterialImportForm').emit('submit'); await settle();
    h.el('confirmImport').checked = true; h.el('confirmImport').emit('change');
    h.responses.push(h.response({ succeeded: false, message: 'Conflict' }, false));
    h.el('postImport').emit('click'); await settle();
    assert.equal(h.el('postImport').disabled, true);
    assert.equal(h.el('importFields').disabled, false);
    assert.match(h.el('importMessage').textContent, /check Opening Stock before retrying/);
});

test('oversize and non-CSV input is rejected without upload', async () => {
    const h = harness(); await settle();
    h.el('importFile').files = [{ name: 'stock.xlsx', size: 100 }];
    h.el('rawMaterialImportForm').emit('submit'); await settle();
    assert.equal(h.requests.length, 1);
    h.el('importFile').files = [{ name: 'stock.csv', size: 3 * 1024 * 1024 }];
    h.el('rawMaterialImportForm').emit('submit'); await settle();
    assert.equal(h.requests.length, 1);
});
