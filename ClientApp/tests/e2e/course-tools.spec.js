import {test,expect} from '@playwright/test'
import {readFileSync} from 'node:fs'
const state=JSON.parse(readFileSync(new URL('../../../.codex-build/portal-smoke.json',import.meta.url),'utf8'))
test('course resources, groups, secret answer keys, private questions and independent exams',async({playwright,browser})=>{
  test.setTimeout(120000)
  const baseURL=process.env.PORTAL_TEST_URL || 'http://127.0.0.1:5107'
  const teacher=await playwright.request.newContext({baseURL}),student=await playwright.request.newContext({baseURL}),other=await playwright.request.newContext({baseURL}),ta=await playwright.request.newContext({baseURL})
  async function write(ctx,path,data,method='POST',extra={}){const token=(await(await ctx.get('/api/auth/csrf')).json()).token;return ctx.fetch('/api'+path,{method,headers:{'X-CSRF-TOKEN':token},...(data?{data}:{}),...extra})}
  for(const [ctx,role] of [[teacher,'Professor'],[student,'Student'],[ta,'TeachingAssistant']]){let response;for(let retry=0;retry<7;retry++){response=await write(ctx,'/auth/login',{email:state.accounts.find(a=>a.role===role).email,password:state.password});if(response.status()!==429)break;await new Promise(resolve=>setTimeout(resolve,10000))}expect(response.status()).toBe(200)}
  const studentId=state.accounts.find(a=>a.role==='Student').id,taId=state.accounts.find(a=>a.role==='TeachingAssistant').id
  let r=await write(teacher,'/courses',{code:'TOOLS-'+state.run+'-'+Date.now(),title:'合成課程功能驗證',termCode:'115-1'});expect(r.status()).toBe(200);const course=(await r.json()).id,base=`/courses/${course}`
  for(const [id,role] of [[studentId,'student'],[taId,'ta']])expect((await write(teacher,`${base}/members`,{userId:id,courseRole:role})).status()).toBe(204)
  expect((await write(teacher,`/accounts/${studentId}/student-number`,{studentNumber:'QA-'+state.run},'PUT')).status()).toBe(204)
  r=await write(teacher,`${base}/resources`,null,'POST',{multipart:{file:{name:'合成教材.txt',mimeType:'text/plain',buffer:Buffer.from('synthetic teaching resource')}}});expect(r.status()).toBe(200);const resource=(await r.json()).id
  expect((await write(other,`${base}/resources/${resource}/download`)).status()).toBe(401)
  r=await write(student,`${base}/resources/${resource}/download`);expect(r.status()).toBe(200);expect((await r.body()).toString()).toBe('synthetic teaching resource');expect((await(await teacher.get('/api'+base+'/resources')).json())[0].downloadCount).toBe(1)
  expect((await write(student,`${base}/groups`,{name:'forbidden',members:[studentId]})).status()).toBe(403)
  expect((await write(ta,`${base}/groups`,{name:'第一組',members:[studentId]})).status()).toBe(200)
  const groups=await(await student.get('/api'+base+'/groups')).json();expect(groups[0].members[0].id).toBe(studentId)
  const questions=[{promptText:'申論',questionType:'essay',options:[],standardAnswer:[]},{promptText:'是非',questionType:'true_false',options:['是','非'],standardAnswer:[0]},{promptText:'單選',questionType:'single',options:['A','B'],standardAnswer:[1]},{promptText:'多選',questionType:'multiple',options:['A','B','C'],standardAnswer:[0,2]}]
  r=await write(teacher,base+'/assignments',{title:'四題型作業',questions});expect(r.status()).toBe(200);const a=await r.json();expect(a.questions[2].standardAnswer).toEqual([1]);expect((await write(teacher,`/assignments/${a.id}/publish`)).status()).toBe(200)
  const visible=await(await student.get(`/api/assignments/${a.id}`)).json();expect(visible.questions.every(q=>q.standardAnswer==null)).toBe(true)
  expect((await write(student,`/assignments/${a.id}/questions/${a.questions[0].id}/ai-feedback`,{expectedVersion:0,answerRevisionId:a.id})).status()).toBe(403)
  r=await write(student,`/assignments/${a.id}/my-submission`,{expectedVersion:0,submit:true,answers:a.questions.map((q,i)=>({questionId:q.id,text:['申論作答','[0]','[1]','[0,2]'][i]}))});expect(r.status()).toBe(200)
  r=await write(teacher,base+'/exams',{title:'獨立考試',questions});expect(r.status()).toBe(200);const exam=(await r.json()).id,eb=base+'/exams/'+exam
  const draft=await(await teacher.get('/api'+eb)).json();expect((await write(teacher,eb,{title:'獨立考試',questions,expectedEditVersion:draft.editVersion},'PUT')).status()).toBe(200);expect((await write(teacher,eb,{title:'過期修改',questions,expectedEditVersion:'stale'},'PUT')).status()).toBe(409);expect((await student.get('/api'+eb)).status()).toBe(404);expect((await write(teacher,eb+'/publish')).status()).toBe(204)
  expect((await(await student.get('/api'+eb)).json()).questions.every(q=>q.standardAnswer==null)).toBe(true)
  const answers={'0':'考試申論','1':'[1]','2':'[0]','3':'[0,2]'}
  expect((await write(student,eb+'/attempt',{answers,expectedVersion:0,submit:false},'PUT')).status()).toBe(204)
  expect((await write(student,eb+'/attempt',{answers,expectedVersion:0,submit:true},'PUT')).status()).toBe(409)
  expect((await write(student,eb+'/attempt',{answers,expectedVersion:1,submit:true},'PUT')).status()).toBe(204)
  expect((await write(student,eb+'/attempt',{answers,expectedVersion:2,submit:false},'PUT')).status()).toBe(409)
  expect((await student.get('/api'+eb+'/results')).status()).toBe(403)
  const result=(await(await ta.get('/api'+eb+'/results?sort=student')).json())[0];expect(result.studentNumber).toBe('QA-'+state.run)
  expect((await write(ta,eb+'/results/'+result.id,{score:85,feedback:'合成評語',expectedVersion:result.version},'PUT')).status()).toBe(204)
  const ranking=(await(await teacher.get('/api'+eb+'/results?sort=score')).json());expect(ranking[0].rank).toBe(1);expect((await(await student.get('/api'+eb+'/attempt')).json()).score).toBe(85)
  const future=await(await write(teacher,base+'/exams',{title:'尚未開放考試',questions,opensAt:new Date(Date.now()+3600000).toISOString()})).json();await write(teacher,base+'/exams/'+future.id+'/publish');expect((await(await student.get('/api'+base+'/exams/'+future.id)).json()).questions).toHaveLength(0);expect((await write(student,base+'/exams/'+future.id+'/attempt',{answers,expectedVersion:0,submit:true},'PUT')).status()).toBe(409)
  const context=await browser.newContext({storageState:await teacher.storageState()}),page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto(baseURL+base);await expect(page.getByRole('heading',{name:'教學資源／講義'})).toBeVisible();await page.getByRole('link',{name:'同學',exact:true}).click();await expect(page.getByRole('heading',{name:'同學',exact:true})).toBeVisible();await page.getByRole('link',{name:'考試管理',exact:true}).click();await expect(page.getByRole('heading',{name:'考試管理',exact:true})).toBeVisible();await page.getByRole('button',{name:'獨立考試',exact:true}).click();await page.getByRole('button',{name:'排行榜',exact:true}).click();await expect(page.getByRole('heading',{name:'排行榜',exact:true})).toBeVisible();await page.getByRole('combobox',{name:'排行榜排序'}).selectOption('student');await expect(page.getByRole('columnheader',{name:'名次',exact:true})).toBeVisible();await page.screenshot({path:'test-results/course-tools.png',fullPage:true});await page.getByRole('link',{name:'作業管理',exact:true}).click();await page.getByRole('button',{name:'建立個人作業',exact:true}).click();const editor=page.locator('.assignment-editor');await editor.getByLabel('題型',{exact:true}).selectOption('single');await editor.getByLabel('選項（每行一個）').fill('A\nB');await editor.getByRole('checkbox',{name:'B',exact:true}).check();await expect(editor.getByRole('checkbox',{name:'B',exact:true})).toBeChecked();await page.setViewportSize({width:390,height:844});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await page.screenshot({path:'test-results/course-tools-mobile.png',fullPage:true});await page.goto(baseURL+'/account');await page.getByRole('button',{name:'登出並切換帳號'}).click();await expect(page).toHaveURL(/login/);expect(errors).toEqual([]);await context.close()
  for(const ctx of [teacher,student,other,ta])await ctx.dispose()
})
