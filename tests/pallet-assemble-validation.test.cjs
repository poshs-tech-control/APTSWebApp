const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');

// Exercise the production closure with a minimal DOM and controlled HTTP responses.
const script = fs.readFileSync(path.join(__dirname, '../APTS_WEB_APP/Scripts/apts-pallet-assemble.js'), 'utf8');
function fixture() {
    const calls = [], messages = [];
    const input = { value: '', focus() {} };
    const form = { getAttribute: name => ({
        'data-assemble-validate-url': '/validate',
        'data-assemble-bypass-url': '/bypass',
        'data-assemble-save-url': '/save'
    })[name] || '' };
    const context = { console, encodeURIComponent, alert: message => messages.push(message), window: {},
        document: { querySelector: selector => selector === '[data-assemble-orders-url]' ? form
            : selector === '[data-assemble-member-input]' ? input : null, addEventListener() {} },
        fetch: (url, options) => new Promise((resolve, reject) => calls.push({url, options, resolve, reject})) };
    vm.runInNewContext(script.replace(/\}\(\)\);\s*$/, `
        globalThis.api = {
            addMember, addValidatedMember, continueBypass, submitAssemble,
            select(order, members) { selectedOrder = order; scannedMembers = members || []; },
            pending(member) { pendingBypassMember = member; },
            members() { return scannedMembers; }
        };
    }());`), context);
    const order = { Customer: 'JSW KHED', PalletOrderdtlCode: 'ORDER1', TotalMemQty: '4',
        CMSize: '486', CMSizeQty: '2', LM1Size: '1100', LM1SizeQty: '2', LM2Size: '0', LM2SizeQty: '0' };
    context.api.select(order);
    return { api: context.api, input, calls, messages, order };
}
const member = (id, type = 'Cross', size = '486', label = 'CM') => ({
    id, rfid: id, memberCode: 'CODE' + id, memberType: type, memberSize: size, sizeRuleLabel: label
});
const respond = (call, payload) => call.resolve({ ok: true, json: () => Promise.resolve(payload) });
async function flush() { for (let i = 0; i < 12; i++) await Promise.resolve(); }

test('Cross 1100 cannot consume the LM1 slot shown in the screenshot', async () => {
    const f = fixture();
    f.input.value = 'FCXLIX';
    const work = f.api.addMember();
    respond(f.calls[0], { success: true, ...member('FCXLIX', 'Cross', '1100'), requiresBypass: true });
    await work;
    assert.equal(f.calls.length, 1, 'No bypass request may run for the wrong type');
    assert.equal(f.api.members().length, 0);
    assert.match(f.messages.at(-1), /Cross with size 1100 is not required/);
});

test('full CM allocation rejects another bypass before a database request', () => {
    const f = fixture();
    f.api.select(f.order, [member('A'), member('B')]);
    f.api.pending(member('C'));
    f.api.continueBypass();
    assert.equal(f.calls.length, 0);
    assert.equal(f.api.members().length, 2);
    assert.match(f.messages.at(-1), /CM size 486 required quantity is already complete/);
});

test('bypass completion rechecks capacity instead of trusting an earlier slot', async () => {
    const f = fixture();
    f.api.select(f.order, [member('A')]);
    f.api.pending(member('B'));
    const work = f.api.continueBypass();
    f.api.addValidatedMember(member('C'));
    respond(f.calls[0], {success: true});
    await work;
    assert.equal(f.api.members().length, 2);
    assert.equal(f.api.members().some(m => m.rfid === 'B'), false);
});

test('duplicate bypass cannot update or append an already scanned member', () => {
    const f = fixture();
    f.api.addValidatedMember(member('A'));
    f.api.pending(member('a'));
    f.api.continueBypass();
    assert.equal(f.calls.length, 0);
    assert.equal(f.api.members().length, 1);
    assert.match(f.messages.at(-1), /already added/);
});

test('scan and submit remain blocked until validation plus bypass finishes', async () => {
    const f = fixture();
    f.input.value = 'A';
    const work = f.api.addMember();
    f.input.value = 'B';
    f.api.addMember();
    assert.equal(f.calls.length, 1);
    respond(f.calls[0], { success: true, ...member('A'), requiresBypass: true });
    await flush();
    assert.equal(f.calls.length, 2);
    f.api.addMember();
    f.api.submitAssemble();
    assert.equal(f.calls.length, 2);
    respond(f.calls[1], {success: true});
    await work;
    assert.equal(f.api.members().length, 1);
    f.input.value = 'B';
    const next = f.api.addMember();
    assert.equal(f.calls.length, 3);
    respond(f.calls[2], { success: true, ...member('B') });
    await next;
    assert.equal(f.api.members().length, 2);
});

test('same-size CM and LM slots stay separate and cannot borrow capacity', () => {
    const f = fixture();
    f.order.LM1Size = '486';
    f.api.addValidatedMember(member('A'));
    f.api.addValidatedMember(member('B'));
    f.api.addValidatedMember(member('C'));
    assert.equal(f.api.members().length, 2);
    f.api.addValidatedMember(member('D', 'Long', '486', 'LM1'));
    assert.equal(f.api.members().length, 3);
    assert.equal(f.api.members()[2].sizeRuleLabel, 'LM1');
});

test('unknown member types and zero-quantity slots are rejected', () => {
    const f = fixture();
    f.api.addValidatedMember(member('A', 'Unknown'));
    f.api.addValidatedMember(member('B', 'Long', '0', 'LM2'));
    assert.equal(f.api.members().length, 0);
});

test('a completed pallet rejects additional bypass members', () => {
    const f = fixture();
    f.api.select(f.order, [member('A'), member('B'), member('C', 'Long', '1100', 'LM1'), member('D', 'Long', '1100', 'LM1')]);
    f.api.pending(member('E'));
    f.api.continueBypass();
    assert.equal(f.calls.length, 0);
    assert.match(f.messages.at(-1), /Required member quantity is already complete/);
});

test('submit rejects the right total with the wrong type/size composition', () => {
    const f = fixture();
    f.api.select(f.order, [member('A'), member('B'), member('C'), member('D')]);
    f.api.submitAssemble();
    assert.equal(f.calls.length, 0);
    assert.match(f.messages.at(-1), /required quantity for each member type and size/);
});

test('correct CM and LM composition can be submitted', async () => {
    const f = fixture();
    [member('A'), member('B'), member('C', 'Long', '1100', 'LM1'), member('D', 'Long', '1100', 'LM1')]
        .forEach(m => f.api.addValidatedMember(m));
    f.api.submitAssemble();
    assert.equal(f.calls.length, 1);
    assert.equal(f.calls[0].url, '/save');
    respond(f.calls[0], {success: false, message: 'Test response'});
    await flush();
});

test('validation results from a previous order cannot populate the new order', async () => {
    const f = fixture();
    f.input.value = 'A';
    const work = f.api.addMember();
    f.api.select({...f.order, PalletOrderdtlCode: 'ORDER2'});
    respond(f.calls[0], {success: true, ...member('A'), requiresBypass: true});
    await work;
    assert.equal(f.calls.length, 1);
    assert.equal(f.api.members().length, 0);
});

test('bypass results from a previous order cannot populate the new order', async () => {
    const f = fixture();
    f.api.pending(member('A'));
    const work = f.api.continueBypass();
    f.api.select({...f.order, PalletOrderdtlCode: 'ORDER2'});
    respond(f.calls[0], {success: true});
    await work;
    assert.equal(f.api.members().length, 0);
});

test('failed bypass releases scan lock and does not append the member', async () => {
    const f = fixture();
    f.input.value = 'A';
    const work = f.api.addMember();
    respond(f.calls[0], {success: true, ...member('A'), requiresBypass: true});
    await flush();
    f.calls[1].reject(new Error('Network failure'));
    await work;
    assert.equal(f.api.members().length, 0);
    const retry = f.api.addMember();
    assert.equal(f.calls.length, 3);
    respond(f.calls[2], {success: false, message: 'Test validation failure'});
    await retry;
});
