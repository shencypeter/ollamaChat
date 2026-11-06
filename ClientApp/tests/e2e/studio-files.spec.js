import {retryLogin} from '../helpers/auth'
import {test,expect} from '@playwright/test'
import {readFileSync} from 'node:fs'
test('image and PDF previews preserve access and download counts',async({playwright,browser,baseURL})=>{
 const state=JSON.parse(readFileSync(new URL('../../../.codex-build/portal-smoke.json',import.meta.url),'utf8')),teacher=await playwright.request.newContext({baseURL}),student=await playwright.request.newContext({baseURL}),outsider=await playwright.request.newContext({baseURL})
 async function write(ctx,path,data,extra={}){const token=(await(await ctx.get('/api/auth/csrf')).json()).token;return ctx.post('/api'+path,{...(data?{data}:{}),...extra,headers:{'X-CSRF-TOKEN':token}})}
 for(const [ctx,role] of [[teacher,'Professor'],[student,'Student'],[outsider,'TeachingAssistant']])expect((await retryLogin(ctx,{email:state.accounts.find(a=>a.role===role).email,password:state.password})).status()).toBe(200)
 const course=await(await write(teacher,'/courses',{code:'PREVIEW-'+Date.now(),title:'合成檔案預覽課程',termCode:'115-1'})).json(),base=`/courses/${course.id}`
 await write(teacher,base+'/members',{userId:state.accounts.find(a=>a.role==='Student').id,courseRole:'student'})
 const other=await(await write(teacher,'/courses',{code:'PREVIEW-OTHER-'+Date.now(),title:'合成其他課程',termCode:'115-1'})).json()
 const png=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aC1sAAAAASUVORK5CYII=','base64')
 const stream='BT /F1 24 Tf 50 100 Td (Synthetic PDF preview) Tj ET',objects=['<< /Type /Catalog /Pages 2 0 R >>','<< /Type /Pages /Kids [3 0 R] /Count 1 >>','<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 200] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>','<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',`<< /Length ${stream.length} >>\nstream\n${stream}\nendstream`];let text='%PDF-1.4\n',offsets=[];for(let i=0;i<objects.length;i++){offsets.push(Buffer.byteLength(text));text+=`${i+1} 0 obj\n${objects[i]}\nendobj\n`}const start=Buffer.byteLength(text);text+=`xref\n0 6\n0000000000 65535 f \n${offsets.map(o=>String(o).padStart(10,'0')+' 00000 n \n').join('')}trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${start}\n%%EOF`;const pdf=Buffer.from(text)
 async function upload(name,buffer,sectionId){return (await write(teacher,(sectionId?`/courses/${sectionId}`:base)+'/resources',null,{multipart:{file:{name,mimeType:'application/octet-stream',buffer},}})).json()}
 const image=await upload('synthetic.png',png),doc=await upload('synthetic.pdf',pdf),restricted=await upload('restricted.png',png,other.id),fake=await upload('fake.png',Buffer.from('<html>unsafe</html>'))
 const path=id=>'/api'+base+`/resources/${id}/preview`
 const ir=await student.get(path(image.id));expect(ir.status()).toBe(200);expect(ir.headers()['content-type']).toBe('image/png');expect(ir.headers()['cache-control']).toBe('no-store');expect(await ir.body()).toEqual(png)
 expect((await student.get(path(restricted.id))).status()).toBe(404);expect((await outsider.get(path(image.id))).status()).toBe(403);expect((await student.get(path(fake.id))).status()).toBe(415)
 expect((await teacher.get(path(restricted.id),{headers:{'X-Portal-View':'student'}})).status()).toBe(404)
 const pr=await student.get(path(doc.id));expect(pr.headers()['content-type']).toBe('application/pdf');expect(pr.headers()['content-disposition']).toBeUndefined();expect(await pr.body()).toEqual(pdf)
 expect((await(await student.get('/api'+base+'/resources')).json()).every(r=>r.downloadCount===0)).toBe(true)
 const context=await browser.newContext({storageState:await teacher.storageState()}),page=await context.newPage();await page.goto(baseURL+`/courses/${course.id}?section=resources`)
 await expect(page.getByAltText('synthetic.png')).toBeVisible();expect(await page.locator('.resource-grid').evaluate(el=>getComputedStyle(el).gridTemplateColumns.split(/\s+/).length)).toBeGreaterThan(1);expect(await page.getByAltText('synthetic.png').evaluate(img=>img.complete && img.naturalWidth>0)).toBe(true)
 await page.getByRole('button',{name:'預覽 PDF',exact:true}).click();await expect(page.locator('object')).toHaveAttribute('type','application/pdf');expect(await page.locator('.resource-expanded').evaluate(el=>Math.abs(el.getBoundingClientRect().width-el.parentElement.getBoundingClientRect().width)<2)).toBe(true);await page.waitForTimeout(1500);await page.screenshot({path:'test-results/file-preview-desktop.png',fullPage:true})
 await page.setViewportSize({width:390,height:844});expect(await page.locator('.resource-grid').evaluate(el=>getComputedStyle(el).gridTemplateColumns.split(/\s+/).length)).toBe(1);expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await page.screenshot({path:'test-results/file-preview-mobile.png',fullPage:true})
 const report=await(await write(teacher,base+'/reports',{title:'預覽合成報告',instructions:'synthetic',maxScore:100,weight:1})).json();await write(teacher,`/reports/${report.id}/publish`)
 expect((await write(student,`/reports/${report.id}/submit`,null,{multipart:{file:{name:'report.pdf',mimeType:'application/pdf',buffer:pdf}}})).ok()).toBe(true)
 const submission=(await(await teacher.get(`/api/reports/${report.id}/submissions`)).json())[0],rp=`/api/reports/${report.id}/submissions/${submission.id}/preview`
 expect((await teacher.get(rp)).status()).toBe(200);expect((await student.get(rp)).status()).toBe(200);expect((await outsider.get(rp)).status()).toBe(403);expect((await teacher.get(rp,{headers:{'X-Portal-View':'student'}})).status()).toBe(404)
 await page.goto(baseURL+`/reports/${report.id}`);await page.getByRole('button',{name:'預覽 PDF',exact:true}).click();await expect(page.locator('object')).toHaveAttribute('data',/^blob:/)
 await context.close();for(const ctx of [teacher,student,outsider])await ctx.dispose()
})
