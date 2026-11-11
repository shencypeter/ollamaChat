import {test,expect} from '@playwright/test'
import {readFileSync} from 'node:fs'
import {randomUUID} from 'node:crypto'
import {retryLogin} from '../helpers/auth'

test('resource sharing, likes, comments, profiles, public course questions and responsive navigation',async({playwright,browser,baseURL})=>{
 test.setTimeout(180000)
 const state=JSON.parse(readFileSync(new URL('../../../.codex-build/portal-smoke.json',import.meta.url),'utf8'))
 const contexts={}
 for(const role of ['Professor','TeachingAssistant','Student']){
  const ctx=await playwright.request.newContext({baseURL});expect((await retryLogin(ctx,{email:state.accounts.find(a=>a.role===role).email,password:state.password})).status()).toBe(200);contexts[role]=ctx
 }
 const teacher=contexts.Professor,ta=contexts.TeachingAssistant,student=contexts.Student
 async function write(ctx,path,data,method='POST',extra={}){const token=(await(await ctx.get('/api/auth/csrf')).json()).token;return ctx.fetch('/api'+path,{method,...(data?{data}:{}),...extra,headers:{'X-CSRF-TOKEN':token,...extra.headers}})}
 const course=await(await write(teacher,'/courses',{code:'INTERACT-'+Date.now(),title:'合成互動課程',termCode:'115-1'})).json(),base=`/courses/${course.id}`
 for(const [role,courseRole] of [['Student','student'],['TeachingAssistant','ta']])expect((await write(teacher,base+'/members',{userId:state.accounts.find(a=>a.role===role).id,courseRole})).status()).toBe(204)
 expect((await write(teacher,base+'/resources',null,'POST',{multipart:{file:{name:'synthetic.txt',mimeType:'text/plain',buffer:Buffer.from('synthetic only')},title:'合成一般格式教材'}})).status()).toBe(200)
 const resource=(await(await student.get('/api'+base+'/resources')).json())[0],rb=base+`/resources/${resource.id}`
 const stateStudent=await student.storageState(),sc=await browser.newContext({storageState:stateStudent,permissions:['clipboard-read','clipboard-write']}),sp=await sc.newPage(),errors=[]
 sp.on('pageerror',e=>errors.push(e.message))
 await sp.goto(baseURL+base+`/materials/${resource.id}?shared=1`)
 await expect(sp.getByRole('heading',{name:'合成一般格式教材'})).toBeVisible();await expect(sp.getByRole('button',{name:'開啟選單'})).toBeHidden()
 await expect(sp.getByText('此格式提供預覽或下載，不累積學習時間。')).toBeVisible()
 const copy=sp.getByRole('button',{name:'複製連結',exact:true});if(await copy.isVisible())await copy.click();else await sp.getByRole('button',{name:'分享連結',exact:true}).click();await expect(sp.getByRole('status').filter({hasText:'連結已複製'})).toBeVisible()
 expect(await sp.evaluate(()=>navigator.clipboard.readText())).toBe(baseURL+base+`/materials/${resource.id}`)
 await sp.screenshot({path:'.codex-build/resource-copy-button.png',fullPage:true})
 const downloaded=sp.waitForEvent('download');await sp.getByRole('button',{name:'下載檔案',exact:true}).click();expect((await downloaded).suggestedFilename()).toBe('synthetic.txt')
 await sp.getByRole('button',{name:'讚 0',exact:true}).click();await expect(sp.getByRole('button',{name:'已按讚 1',exact:true})).toBeVisible()
 await Promise.all([write(student,rb+'/like',{liked:true},'PUT'),write(student,rb+'/like',{liked:true},'PUT')]);expect((await(await teacher.get('/api'+rb+'/interactions')).json()).likes).toBe(1)
 await sp.getByLabel('留言',{exact:true}).fill('<b>合成留言純文字</b>');await sp.getByRole('button',{name:'送出留言'}).click();await expect(sp.locator('.interaction-text')).toHaveText('<b>合成留言純文字</b>');expect(await sp.locator('.interaction-text b').count()).toBe(0)
 expect((await write(student,rb+'/comments',{id:randomUUID(),text:'冒用',authorId:state.accounts[0].id})).status()).toBe(400)
 expect((await write(teacher,rb+'/like',{liked:true},'PUT',{headers:{'X-Portal-View':'student'}})).status()).toBe(403)
 await sp.goto(baseURL+'/learners');await expect(sp.getByRole('heading',{name:'學員檔案',exact:true})).toBeVisible();await sp.getByRole('combobox',{name:'互動課程',exact:true}).selectOption(course.id);await expect(sp.getByText('2 項資源互動；紀錄不代表理解或完課。')).toBeVisible()
 const studentId=state.accounts.find(a=>a.role==='Student').id
 expect((await teacher.get(`/api/interaction-profile?student=${studentId}`)).status()).toBe(403)
 expect((await teacher.get(`/api/interaction-profile?student=${studentId}&course=${course.id}`)).status()).toBe(200)
 const tc=await browser.newContext({storageState:await teacher.storageState()}),tp=await tc.newPage();tp.on('pageerror',e=>errors.push(e.message))
 await tp.goto(baseURL+base+'?section=students');await tp.getByRole('link',{name:'學員檔案',exact:true}).click();await expect(tp.getByText('合成一般格式教材',{exact:true}).first()).toBeVisible()
 await sp.goto(baseURL+base+'?section=questions');await sp.getByLabel('提出課程問題').fill('合成未回覆即公開問題');await sp.getByRole('button',{name:'送出問題',exact:true}).click();await expect(sp.getByText('待回覆',{exact:false})).toBeVisible()
 await tp.goto(baseURL+base+'?section=questions');await expect(tp.getByText('合成未回覆即公開問題',{exact:true})).toBeVisible()
 const question=(await(await ta.get('/api'+base+'/questions')).json()).items[0]
 expect((await write(student,base+`/questions/${question.id}/response`,{response:'學生不得回覆',expectedVersion:1},'PUT')).status()).toBe(403)
 expect((await write(ta,base+`/questions/${question.id}/response`,{response:'合成助教回覆',expectedVersion:1},'PUT')).status()).toBe(204)
 await tp.getByLabel('教師／助教答覆').fill('保留的衝突答覆');await tp.getByRole('button',{name:'儲存答覆',exact:true}).click();await expect(tp.getByRole('alert')).toContainText('答覆已更新');await expect(tp.getByLabel('教師／助教答覆')).toHaveValue('保留的衝突答覆')
 tp.once('dialog',d=>d.accept());await tp.getByRole('button',{name:'重新載入此題'}).click();await expect(tp.getByLabel('教師／助教答覆')).toHaveValue('合成助教回覆');await tp.getByLabel('教師／助教答覆').fill('合成教師最終回覆');await tp.getByRole('button',{name:'儲存答覆',exact:true}).click();await expect(tp.getByText('答覆已儲存。')).toBeVisible()
 await sp.reload();await expect(sp.getByText('合成教師最終回覆',{exact:true})).toBeVisible();await expect(sp.getByText('已回覆',{exact:false})).toBeVisible()
 await tp.getByRole('link',{name:'課程設定',exact:true}).click();await expect(tp.getByRole('heading',{name:'課程設定',exact:true})).toBeVisible()
 await sp.setViewportSize({width:390,height:844});await sp.getByRole('button',{name:'開啟選單',exact:true}).click();await expect(sp.getByRole('button',{name:'關閉選單',exact:true})).toHaveAttribute('aria-expanded','true');await sp.getByRole('button',{name:'關閉選單',exact:true}).click()
 expect(await sp.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true)
 await sp.screenshot({path:'.codex-build/interactions-mobile.png',fullPage:true});await tp.screenshot({path:'.codex-build/interactions-desktop.png',fullPage:true});expect(errors).toEqual([])
 for(const ctx of [sc,tc])await ctx.close();for(const ctx of Object.values(contexts))await ctx.dispose()
})
