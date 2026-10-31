const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');
const source = fs.readFileSync(path.join(__dirname, '../APTS_WEB_APP/Scripts/apts-member-by-client-batch-no-report.js'), 'utf8');

function node() {
    return { children: [], events: {}, value: '', disabled: false, classList: {toggle() {}},
        set textContent(value) { this.text = value; this.children = []; }, get textContent() { return this.text || ''; },
        appendChild(child) { this.children.push(child); }, setAttribute() {},
        addEventListener(name, handler) { this.events[name] = handler; } };
}
function setup() {
    const form = node(), input = node(), status = node(), count = node(), results = node(), submit = node();
    const fields = {'[data-client-batch-input]':input, '[data-client-batch-status]':status,
        '[data-client-batch-count]':count, '[data-client-batch-results]':results, "[type='submit']":submit};
    form.querySelector = key => fields[key];
    form.getAttribute = () => '/Report/GetMemberByClientBatchNoReport';
    const calls = [], popups = [];
    const context = { Array, String, URLSearchParams, window: {AptsProcessPopup: {show: m => popups.push(m)}},
        document: {querySelector: () => form, createElement: node, addEventListener: (_, f) => f()},
        fetch: (url, options) => new Promise((resolve, reject) => calls.push({url, options, resolve, reject})) };
    vm.runInNewContext(source, context);
    return {form, input, status, count, results, submit, calls, popups};
}
async function flush() { for(let i=0;i<12;i++) await Promise.resolve(); }
function reply(call, payload, ok=true) { call.resolve({ok, json: () => Promise.resolve(payload)}); }
const row = (rfid='FKAPXP') => ({RFID:rfid, LastEntryBy:'Operator', LastUpdatedDate:'26/10/2026 10:00:00', LastTransferTo:'JSW KHED', LastStatus:'Pre Dispatch'});
const search = (f, batch) => { f.input.value=batch; f.input.events.change(); };

test('blank and overlength batch inputs do not issue requests', () => {
    const f=setup(); search(f,'   '); assert.equal(f.calls.length,0); assert.match(f.status.textContent,/Please scan or enter/);
    search(f,'x'.repeat(51)); assert.equal(f.calls.length,0); assert.match(f.status.textContent,/50 characters/);
});
test('batch is trimmed, parameter-encoded and all returned member rows are shown', async () => {
    const f=setup(); search(f,' BATCH & 01 ');
    assert.equal(new URLSearchParams(f.calls[0].options.body).get('ClientBatchNo'),'BATCH & 01');
    assert.equal(f.submit.disabled,true);
    reply(f.calls[0],{success:true,rows:[row(),row('SECOND')]}); await flush();
    assert.equal(f.results.children.length,2); assert.equal(f.count.textContent,'Records: 2');
    assert.equal(f.results.children[0].children[0].children[1].textContent,'FKAPXP');
    assert.equal(f.results.children[1].children[3].children[1].textContent,'JSW KHED');
    assert.equal(f.results.children[0].children[4].children[0].textContent,'Last Status');
    assert.equal(f.results.children[0].children[4].children[1].textContent,'Pre Dispatch');
    assert.equal(f.submit.disabled,false);
});
test('QR change plus scan events issue only one request while pending', () => {
    const f=setup(); search(f,'BATCH1'); f.input.events['apts:qr-scan'](); f.form.events.submit({preventDefault(){}});
    assert.equal(f.calls.length,1);
});
test('no matching members clears old details and reports no records', async () => {
    const f=setup(); search(f,'BATCH1'); reply(f.calls[0],{success:true,rows:[row()]}); await flush();
    search(f,'MISSING'); reply(f.calls[1],{success:true,rows:[]}); await flush();
    assert.equal(f.results.children.length,0); assert.equal(f.count.textContent,'Records: 0');
    assert.match(f.status.textContent,/No members found/);
});
test('a late response cannot overwrite the newer batch results', async () => {
    const f=setup(); search(f,'OLD'); search(f,'NEW');
    reply(f.calls[1],{success:true,rows:[row('NEW')]}); await flush();
    reply(f.calls[0],{success:true,rows:[row('OLD')]}); await flush();
    assert.equal(f.results.children[0].children[0].children[1].textContent,'NEW');
});
test('Clear invalidates a pending response and resets busy state', async () => {
    const f=setup(); search(f,'BATCH'); f.form.events.reset();
    reply(f.calls[0],{success:true,rows:[row()]}); await flush();
    assert.equal(f.results.children.length,0); assert.equal(f.status.textContent,''); assert.equal(f.submit.disabled,false);
});
test('database/server errors remain distinct from no matching members', async () => {
    const f=setup(); search(f,'BATCH'); reply(f.calls[0],{success:false,message:'Unable to load report.'}); await flush();
    assert.equal(f.status.textContent,'Unable to load report.'); assert.equal(f.popups.length,1); assert.equal(f.results.children.length,0);
    search(f,'BATCH'); f.calls[1].reject(new Error('Network failed')); await flush();
    assert.match(f.status.textContent,/Unable to load Members Details By ClientBatchNo/); assert.equal(f.submit.disabled,false);
});
test('member values are rendered as literal text and missing fields as hyphens', async () => {
    const f=setup(); search(f,'BATCH'); reply(f.calls[0],{success:true,rows:[{RFID:'<img src=x onerror=alert(1)>'}]}); await flush();
    const card=f.results.children[0]; assert.equal(card.children[0].children[1].textContent,'<img src=x onerror=alert(1)>');
    assert.equal(card.children[1].children[1].textContent,'-');
    assert.equal(card.children[4].children[1].textContent,'-');
});
