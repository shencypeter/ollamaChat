import {test,expect} from '@playwright/test'
import {readFileSync} from 'node:fs'
import {randomUUID} from 'node:crypto'
import {retryLogin} from '../helpers/auth'

test('personal red badges reflect published assignments and student questions and clear only after viewing',async({playwright,browser,baseURL})=>{
 test.setTimeout(90000)
 const state=JSON.parse(readFileSync(new URL('../../../.codex-build/portal-smoke.json',import.meta.url),'utf8')),contexts={}
 for(const role of ['Professor','TeachingAssistant','Student']){const ctx=await playwright.request.newContext({baseURL});expect((await retryLogin(ctx,{email:state.accounts.find(a=>a.role===role).email,password:state.password})).status()).toBe(200);contexts[role]=ctx}
 const teacher=contexts.Professor,student=contexts.Student,ta=contexts.TeachingAssistant
 async function write(ctx,path,data,method='POST'){const token=(await(await ctx.get('/api/auth/csrf')).json()).token;return ctx.fetch('/api'+path,{method,data,headers:{'X-CSRF-TOKEN':token}})}
 const course=await(await write(teacher,'/courses',{code:'BADGE-'+Date.now(),title:'合成通知課程',termCode:'115-1'})).json(),base=`/courses/${course.id}`
 for(const [role,courseRole] of [['Student','student'],['TeachingAssistant','ta']])expect((await write(teacher,base+'/members',{userId:state.accounts.find(a=>a.role===role).id,courseRole})).status()).toBe(204)
 const count=async(ctx)=>{const data=await(await ctx.get('/api/notifications')).json();return data.courses.find(c=>c.courseId===course.id)||{assignments:0,questions:0}}
 const report=await(await write(teacher,base+'/reports',{title:'合成新作業',maxScore:100,weight:1})).json()
 expect((await count(student)).assignments).toBe(0)
 const sc=await browser.newContext({storageState:await student.storageState()}),sp=await sc.newPage(),tc=await browser.newContext({storageState:await teacher.storageState()}),tp=await tc.newPage(),errors=[]
 for(const p of [sp,tp])p.on('pageerror',e=>errors.push(e.message))
 await sp.goto(baseURL+base);await tp.goto(baseURL+'/courses')
 expect((await write(teacher,`/reports/${report.id}/publish`,{})).status()).toBe(204)
 await sp.evaluate(()=>window.dispatchEvent(new Event('focus')))
 const assessmentBadge=sp.getByRole('navigation',{name:'課程功能'}).getByRole('link',{name:'測驗與作業',exact:true}).locator('.notification-badge')
 await expect(assessmentBadge).toHaveText('1');expect((await count(teacher)).assignments).toBe(0)
 await sp.setViewportSize({width:390,height:844});await expect(sp.getByRole('button',{name:'開啟選單',exact:true}).locator('.notification-badge')).toBeVisible()
 await sp.screenshot({path:'.codex-build/notifications-student-mobile.png',fullPage:true});await sp.getByRole('navigation',{name:'課程功能'}).getByRole('link',{name:'測驗與作業',exact:true}).click()
 await expect(sp.getByRole('heading',{name:'合成新作業',exact:true})).toBeVisible();await expect(assessmentBadge).toBeHidden();expect((await count(student)).assignments).toBe(0)
 await sp.reload();await expect(sp.getByRole('heading',{name:'合成新作業',exact:true})).toBeVisible();expect((await count(student)).assignments).toBe(0)
 const questionId=randomUUID();expect((await write(student,base+'/questions',{id:questionId,text:'合成新的學生問題'})).status()).toBe(204)
 await tp.evaluate(()=>window.dispatchEvent(new Event('focus')))
 await expect(tp.locator('.course-card').filter({hasText:'合成通知課程'}).getByText('學生新提問',{exact:false})).toBeVisible()
 await tp.screenshot({path:'.codex-build/notifications-teacher-desktop.png',fullPage:true});expect((await count(ta)).questions).toBe(1)
 await tp.locator('.course-card').filter({hasText:'合成通知課程'}).getByRole('link',{name:/學生新提問/}).click();await expect(tp.getByText('合成新的學生問題',{exact:true})).toBeVisible()
 await expect.poll(async()=>(await count(teacher)).questions).toBe(0);expect((await count(ta)).questions).toBe(1)
 await tp.reload();await expect(tp.getByText('合成新的學生問題',{exact:true})).toBeVisible();expect((await count(teacher)).questions).toBe(0)
 expect(await sp.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);expect(errors).toEqual([])
 await sc.close();await tc.close();for(const ctx of Object.values(contexts))await ctx.dispose()
})
