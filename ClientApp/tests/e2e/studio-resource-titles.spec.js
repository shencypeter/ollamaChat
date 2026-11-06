import {retryLogin} from '../helpers/auth'
import {test,expect} from '@playwright/test'
import {readFileSync} from 'node:fs'
test('resource titles can be set at upload and edited later without changing original files',async({playwright,browser,baseURL})=>{
 const state=JSON.parse(readFileSync(new URL('../../../.codex-build/portal-smoke.json',import.meta.url),'utf8')),teacher=await playwright.request.newContext({baseURL}),student=await playwright.request.newContext({baseURL}),outsider=await playwright.request.newContext({baseURL})
 async function write(ctx,path,data,method='POST',extra={}){const token=(await(await ctx.get('/api/auth/csrf')).json()).token;return ctx.fetch('/api'+path,{method,...(data?{data}:{}),...extra,headers:{'X-CSRF-TOKEN':token,...extra.headers}})}
 for(const [ctx,role] of [[teacher,'Professor'],[student,'Student'],[outsider,'TeachingAssistant']])expect((await retryLogin(ctx,{email:state.accounts.find(a=>a.role===role).email,password:state.password})).status()).toBe(200)
 const course=await(await write(teacher,'/courses',{code:'TITLES-'+Date.now(),title:'合成文件名稱課程',termCode:'115-1'})).json(),foreign=await(await write(teacher,'/courses',{code:'OTHER-TITLES-'+Date.now(),title:'合成隔離課程',termCode:'115-1'})).json(),base=`/courses/${course.id}`;await write(teacher,base+'/members',{userId:state.accounts.find(a=>a.role==='Student').id,courseRole:'student'})
 const pdf=Buffer.from('%PDF-1.4\nsynthetic\n%%EOF');async function upload(name,buffer,title){return (await write(teacher,base+'/resources',null,'POST',{multipart:{file:{name,mimeType:'application/octet-stream',buffer},...(title?{title}:{})}})).json()}
 const file=await upload('generic.pdf',pdf,'第一週講義'),legacy=await upload('legacy.txt',Buffer.from('synthetic'))
 let items=await(await student.get('/api'+base+'/resources')).json(),item=items.find(r=>r.id===file.id);expect(item.title).toBe('第一週講義');expect(item.fileName).toBe('generic.pdf');expect(item.titleVersion).toBe(1);expect(items.find(r=>r.id===legacy.id).title).toBe('')
 for(const ctx of [student,outsider])expect((await write(ctx,`${base}/resources/${file.id}/title`,{title:'禁止',expectedVersion:1},'PUT')).status()).toBe(403)
 expect((await write(teacher,`${base}/resources/${file.id}/title`,{title:'禁止',expectedVersion:1},'PUT',{headers:{'X-Portal-View':'student'}})).status()).toBe(403)
 expect((await write(teacher,`/courses/${foreign.id}/resources/${file.id}/title`,{title:'跨課禁止',expectedVersion:1},'PUT')).status()).toBe(404)
 expect((await write(teacher,`${base}/resources/${file.id}/title`,{title:'新版講義',expectedVersion:1},'PUT')).status()).toBe(200)
 expect((await write(teacher,`${base}/resources/${file.id}/title`,{title:'舊版本禁止覆蓋',expectedVersion:1},'PUT')).status()).toBe(409)
 expect((await write(teacher,`${base}/resources/${file.id}/title`,{title:'x'.repeat(201),expectedVersion:2},'PUT')).status()).toBe(400)
 expect((await write(teacher,`${base}/resources/${file.id}/title`,{title:'',expectedVersion:2},'PUT')).status()).toBe(200)
 const context=await browser.newContext({storageState:await teacher.storageState()}),page=await context.newPage();await page.goto(baseURL+`/courses/${course.id}?section=resources`)
 const pdfCard=page.locator('.resource-card').filter({hasText:'generic.pdf'}),textCard=page.locator('.resource-card').filter({hasText:'legacy.txt'})
 await pdfCard.getByRole('button',{name:'編輯文件名稱',exact:true}).click();await pdfCard.getByLabel('generic.pdf 文件名稱',{exact:true}).fill('未存PDF名稱')
 await textCard.getByRole('button',{name:'編輯文件名稱',exact:true}).click();await textCard.getByLabel('legacy.txt 文件名稱',{exact:true}).fill('案例閱讀筆記')
 expect((await write(teacher,`${base}/resources/${file.id}/title`,{title:'另一位教師的新名稱',expectedVersion:3},'PUT')).status()).toBe(200)
 await textCard.getByRole('button',{name:'儲存文件名稱',exact:true}).click();await expect(page.getByText('文件名稱已儲存，原始檔案保持不變。',{exact:true})).toBeVisible();await expect(textCard.getByText('案例閱讀筆記',{exact:true})).toBeVisible();await expect(pdfCard.getByLabel('generic.pdf 文件名稱',{exact:true})).toHaveValue('未存PDF名稱')
 await pdfCard.getByRole('button',{name:'儲存文件名稱',exact:true}).click();await expect(page.getByRole('alert')).toContainText('文件名稱已被其他操作更新')
 items=await(await student.get('/api'+base+'/resources')).json();expect(items.find(r=>r.id===file.id).title).toBe('另一位教師的新名稱');expect(items.find(r=>r.id===legacy.id).title).toBe('案例閱讀筆記');expect(items.find(r=>r.id===file.id).downloadCount).toBe(0)
 const download=await write(student,`${base}/resources/${file.id}/download`);expect(await download.body()).toEqual(pdf);expect(download.headers()['content-disposition']).toContain('generic.pdf')
 await write(student,'/studio/profile',{displayName:'合成文件學生',studentNumber:'QA-'+state.run,departmentYear:''},'PUT');const sc=await browser.newContext({storageState:await student.storageState()}),sp=await sc.newPage();await sp.goto(baseURL+`/courses/${course.id}?section=resources`);await expect(sp.getByText('案例閱讀筆記',{exact:true})).toBeVisible();expect(await sp.getByRole('button',{name:'編輯文件名稱',exact:true}).count()).toBe(0)
 await context.close();await sc.close();for(const ctx of [teacher,student,outsider])await ctx.dispose()
})
