const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../NexusFlow.Web/wwwroot/js/treasury/supplier-payments.js'), 'utf8');

function harness() {
    const elements = new Map(), errors = [], selections = [];
    const panel = { replaceChildren() {}, classList: { add() {}, remove() {} } };
    const document = { getElementById: id => id === 'supplierOpeningCredits' ? panel : {}, title: 'Payments' };
    function $(selector) {
        if (selector === document) return { ready() {} };
        selections.push(selector);
        if (!elements.has(selector)) elements.set(selector, {
            0: { reset() {} }, content: '', value: '',
            html(v) { if (v === undefined) return this.content; this.content = v; return this; },
            val(v) { if (v === undefined) return this.value; this.value = v; return this; }, trigger() { return this; }
        });
        return elements.get(selector);
    }
    const context = { window: { location: { search: '?billId=42', pathname: '/Treasury/Payments' } }, document, $, Date, URLSearchParams,
        history: { replaceState() {} }, api: { get: async () => [] }, console,
        toastr: { error: message => errors.push(message), success() {} } };
    vm.runInNewContext(source, context);
    const app = context.window.paymentApp; context.paymentApp = app;
    app._calculateTotals = () => {}; app.onMethodChange = () => {};
    let shown = false; app._modal = { show() { shown = true; } };
    return { app, context, elements, errors, selections, shown: () => shown };
}

test('opening reference renders as text and supplied due date remains distinct from document date', async () => {
    const h = harness();
    h.context.api.get = async url => url.includes('unpaid-bills') ? [{ id: 42, targetType: 'OpeningItem', billNumber: '<img src=x onerror=alert(1)>', date: '2026-09-01', dueDate: '2026-10-01', total: 100, balance: 100 }] : [];
    await h.app._loadUnpaidBills(1);
    const html = h.elements.get('#allocationBody').content;
    assert.ok(html.includes('&lt;img src=x onerror=alert(1)&gt;'));
    assert.ok(!html.includes('<img'));
    assert.ok(html.includes(`<td>${new Date('2026-10-01').toLocaleDateString()}</td>`));
    assert.ok(html.includes('data-type="OpeningItem"'));
});

test('bill deep link allocates only the bill when an opening item shares its numeric ID', async () => {
    const h = harness();
    h.context.api.get = async () => ({ id: 42, supplierId: 1, isPosted: true, grandTotal: 50, amountPaid: 0 });
    h.app._loadUnpaidBills = async () => [{ id: 42, targetType: 'OpeningItem' }, { id: 42, targetType: 'Bill' }];
    await h.app._initializeBillDeepLink();
    assert.equal(h.shown(), true);
    assert.deepEqual(h.errors, []);
    assert.equal(h.elements.get('#allocationBody tr[data-id="42"][data-type="Bill"] .alloc-input').value, '50.00');
    assert.ok(!h.selections.includes('#allocationBody tr[data-id="42"] .alloc-input'));
});

test('an opening item cannot stand in for an unavailable bill in a payment deep link', async () => {
    const h = harness();
    h.context.api.get = async () => ({ id: 42, supplierId: 1, isPosted: true, grandTotal: 50, amountPaid: 0 });
    h.app._loadUnpaidBills = async () => [{ id: 42, targetType: 'OpeningItem' }];
    await h.app._initializeBillDeepLink();
    assert.equal(h.shown(), false);
    assert.ok(h.errors.some(x => x.includes('not available for payment')));
});
