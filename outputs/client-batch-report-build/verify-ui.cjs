const {chromium} = require('C:/Users/Bagwa/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs=require('node:fs/promises');
const path=require('node:path');
const assert=require('node:assert/strict');
const app=path.resolve(__dirname,'../../APTS_WEB_APP');
(async()=>{
 const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 try {
  const page=await browser.newPage({viewport:{width:390,height:844},deviceScaleFactor:1});
  const errors=[]; page.on('pageerror',e=>errors.push(e.message));
  await page.route('http://apts.test/**',async route=>{
   const url=new URL(route.request().url());
   if(url.pathname==='/Report/GetMemberByClientBatchNoReport'){
    const batch=new URLSearchParams(route.request().postData()).get('ClientBatchNo');
    return route.fulfill({json:{success:true,rows:batch==='MISSING'?[]:[
     {RFID:'FKAPXP',LastEntryBy:'Subhodh Dhokale',LastUpdatedDate:'26/10/2026 10:30:00',LastTransferTo:'JSW Khed',LastStatus:'Pallet Disassemble From Client'},
     {RFID:'CB6919',LastEntryBy:'Operator',LastUpdatedDate:'26/10/2026 10:31:00',LastTransferTo:'JSW Khed',LastStatus:'Pre Dispatch'},
     {RFID:'CB1041',LastEntryBy:'Operator',LastUpdatedDate:'26/10/2026 10:32:00',LastTransferTo:'JSW Khed',LastStatus:'Pallet Assemble'}]}});
   }
   if(url.pathname==='/report'){
    let html=await fs.readFile(path.join(app,'Views/Report/MemberByClientBatchNo.cshtml'),'utf8');
    html=html.replace(/^@\{[\s\S]*?\}\s*/,'').replace(/@Url\.Content\("~\/([^"]+)"\)/g,'/$1')
     .replace(/@Url\.Action\("([^"]+)", "([^"]+)"\)/g,'/$2/$1').replace(/~\//g,'/')
     .replace(/<script[^>]+apts-language-translator[^>]*><\/script>/,'');
    return route.fulfill({contentType:'text/html',body:html});
   }
   try {
    const file=path.join(app,decodeURIComponent(url.pathname));
    const ext=path.extname(file);
    return route.fulfill({body:await fs.readFile(file),contentType:({'.css':'text/css','.js':'text/javascript','.png':'image/png'})[ext]||'application/octet-stream'});
   }catch{ return route.fulfill({status:404,body:''}); }
  });
  await page.goto('http://apts.test/report');
  await page.locator('#ReportClientBatchNo').fill('BATCH-20261022');
  await page.getByRole('button',{name:'Submit',exact:true}).click();
  await page.locator('[data-client-batch-count]').filter({hasText:'Records: 3'}).waitFor();
  assert.equal(await page.locator('[data-client-batch-results] > section').count(),3);
  assert.equal(await page.getByText('Last Status',{exact:true}).count(),3);
  assert.equal(await page.getByText('Pallet Disassemble From Client',{exact:true}).count(),1);
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),true,'mobile overflow');
  async function verifyScrollRegion() {
   const before=await page.evaluate(()=>{
    const region=document.querySelector('.apts-client-batch-report-data');
    const submit=document.querySelector('[type="submit"]').getBoundingClientRect();
    const clear=document.querySelector('[type="reset"]').getBoundingClientRect();
    const box=region.getBoundingClientRect();
    return {submitTop:submit.top,submitBottom:submit.bottom,clearTop:clear.top,clearBottom:clear.bottom,
     regionTop:box.top,regionBottom:box.bottom,canScroll:region.scrollHeight>region.clientHeight,viewport:innerHeight};
   });
   assert.ok(before.submitTop>=0 && before.clearBottom<=before.viewport,'buttons must stay visible');
   assert.ok(before.regionTop>=before.submitBottom && before.regionBottom<=before.clearTop,'results must fit between buttons');
   assert.ok(before.canScroll,'results should scroll internally');
   await page.locator('.apts-client-batch-report-data').evaluate(el=>{el.scrollTop=el.scrollHeight;});
   const after=await page.evaluate(()=>({
    clear:document.querySelector('[type="reset"]').getBoundingClientRect().top,
    submit:document.querySelector('[type="submit"]').getBoundingClientRect().top,
    position:document.querySelector('.apts-client-batch-report-data').scrollTop,windowScroll:window.scrollY
   }));
   assert.ok(after.position>0,'data must actually scroll');
   assert.equal(after.clear,before.clearTop); assert.equal(after.submit,before.submitTop); assert.equal(after.windowScroll,0);
  }
  await verifyScrollRegion();
  await page.screenshot({path:path.join(__dirname,'member-by-batch-mobile-scrolled.png'),fullPage:true});
  await page.locator('.apts-client-batch-report-data').evaluate(el=>{el.scrollTop=0;});
  await page.screenshot({path:path.join(__dirname,'member-by-batch-mobile.png'),fullPage:true});
  for(const viewport of [{width:375,height:667},{width:320,height:568}]) {
   await page.setViewportSize(viewport);
   await verifyScrollRegion();
  }
  await page.setViewportSize({width:1280,height:900});
  await verifyScrollRegion();
  await page.screenshot({path:path.join(__dirname,'member-by-batch-desktop.png'),fullPage:true});
  await page.getByRole('button',{name:'Clear',exact:true}).click();
  assert.equal(await page.locator('[data-client-batch-results] > section').count(),0);
  await page.locator('#ReportClientBatchNo').fill('MISSING');
  await page.getByRole('button',{name:'Submit',exact:true}).click();
  await page.locator('[data-client-batch-status]').filter({hasText:'No members found'}).waitFor();
  assert.deepEqual(errors,[]);
  console.log('PASS: internal results scrolling between stationary Submit/Clear buttons at 390x844, 375x667, 320x568 and 1280x900; Clear, empty results, no JavaScript errors. Mocked data; no live database accessed.');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
