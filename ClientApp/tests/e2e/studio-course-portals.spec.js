import {test,expect} from '@playwright/test'
import {retryLogin} from '../helpers/auth'

// Run against a disposable database/schema initialized with --course-demo.
test.skip(!process.env.PORTAL_DEMO_PASSWORD || process.env.PORTAL_TEST_DISPOSABLE!=='1','Requires the disposable multi-course scenario')
test('course roles, responsive navigation, submissions and released results',async({browser,baseURL})=>{
  test.setTimeout(150000)
  async function login(name){
    const context=await browser.newContext({baseURL})
    const result=await retryLogin(context.request,{email:name+'@portal-demo.example.invalid',password:process.env.PORTAL_DEMO_PASSWORD})
    expect(result.status()).toBe(200)
    return context
  }
  async function write(ctx,path,data,method='POST',headers={}){
    const {token}=await (await ctx.request.get('/api/auth/csrf')).json()
    return ctx.request.fetch('/api'+path,{method,data,headers:{'X-CSRF-TOKEN':token,...headers}})
  }
  const teacher=await login('faculty1'),student=await login('student10'),peer=await login('student01'),ta=await login('ta')
  const ownId=(await (await student.request.get('/api/auth/session')).json()).user.id
  const list=await (await teacher.request.get('/api/courses')).json()
  const seeded=list.find(c=>c.code==='DEMO-1'),base='/courses/'+seeded.id
  const page=await teacher.newPage(),errors=[]
  page.on('pageerror',e=>errors.push(e.message))
  await page.goto(base)
  const nav=page.getByRole('navigation',{name:'工作區導覽'})
  await expect(nav).toBeVisible()
  await expect(page.getByRole('button',{name:'開啟選單'})).toBeHidden()
  await nav.getByRole('link',{name:'教材管理'}).click()
  await expect(page.getByRole('heading',{name:'課程教材'})).toBeVisible()
  await expect(page.getByText('本週閱讀重點與討論題目。')).toBeVisible()
  if(process.env.PORTAL_SCREENSHOTS)await page.screenshot({path:process.env.PORTAL_SCREENSHOTS+'/materials.png'})
  await page.getByRole('button',{name:'預覽 PDF',exact:true}).click()
  await expect(page.locator('object[type="application/pdf"]')).toBeVisible()
  await page.getByRole('button',{name:'收合 PDF 預覽'}).click()
  const mats=await (await student.request.get('/api'+base+'/resources')).json()
  const pdf=mats.find(m=>m.previewType==='application/pdf')
  expect((await write(student,base+'/resources/'+pdf.id+'/download')).status()).toBe(200)
  await page.getByRole('button',{name:'學生視角（預覽）'}).click()
  await expect(page.getByRole('status').filter({hasText:'學生視角預覽'})).toBeVisible()
  await expect(nav.getByRole('link',{name:'學員管理'})).toHaveCount(0)
  await page.reload()
  await page.getByRole('button',{name:'返回教師視角'}).click()
  await page.setViewportSize({width:390,height:844})
  const menu=page.getByRole('button',{name:'開啟選單'})
  await expect(nav).toBeHidden()
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBeTruthy()
  await menu.click()
  await expect(nav.getByRole('link',{name:'返回我的授課'})).toBeFocused()
  if(process.env.PORTAL_SCREENSHOTS)await page.screenshot({path:process.env.PORTAL_SCREENSHOTS+'/drawer.png'})
  await page.keyboard.press('Escape')
  await expect(menu).toBeFocused()
  await menu.click()
  await page.locator('.sidebar-backdrop').click({position:{x:350,y:90}})
  await expect(nav).toBeHidden()
  await menu.click()
  await nav.getByRole('link',{name:'課程分組'}).click()
  await expect(nav).toBeHidden()
  await expect(page.getByRole('heading',{name:'課程小組設定'})).toBeVisible()
  const groups=await (await teacher.request.get('/api'+base+'/groups')).json()
  if(!groups.some(g=>g.members.some(m=>m.id===ownId))){
    expect((await write(teacher,base+'/groups',{name:'示範分組',members:[ownId],leaderId:ownId})).ok()).toBeTruthy()
  }
  await page.reload()
  await expect(page.getByRole('checkbox',{name:/D11510/})).toBeDisabled()
  expect((await write(teacher,base+'/groups',{name:'重複選擇',members:[ownId]})).status()).toBe(400)
  const course=(await (await write(teacher,'/courses',{code:'QA-'+Date.now(),title:'隔離驗收課程',termCode:'115-1'})).json()).id
  const root='/courses/'+course
  expect((await write(teacher,root+'/members',{userId:ownId,courseRole:'student'})).status()).toBe(204)
  expect((await student.request.get('/api'+root)).status()).toBe(404)
  const myCourses=async ctx=>(await (await ctx.request.get('/api/courses?studentPortal=true')).json())
  expect((await myCourses(teacher)).some(c=>c.id===course)).toBeTruthy()
  expect((await myCourses(student)).some(c=>c.id===course)).toBeFalsy()
  const draftPreview=await teacher.request.get('/api'+root,{headers:{'X-Portal-View':'student'}})
  expect(await draftPreview.json()).toMatchObject({status:'draft',courseRole:'student'})
  expect((await write(teacher,root+'/activate')).status()).toBe(204)
  expect((await peer.request.get('/api'+root+'/gradebook')).status()).toBe(403)
  const assessment={title:'申論與客觀題',maxScore:60,timeLimitMinutes:30,questions:[
    {promptText:'請選出全部正確項目',questionType:'multiple',options:['甲','乙','丙'],standardAnswer:[0,2],points:20},
    {promptText:'說明判斷依據',questionType:'essay',points:40}]}
  const {id}=await (await write(teacher,root+'/quizzes',{assignment:assessment,weight:1})).json()
  const exam='/assignments/'+id
  expect((await student.request.get('/api'+exam)).status()).toBe(404)
  expect((await write(teacher,exam+'/publish')).ok()).toBeTruthy()
  const preview={'X-Portal-View':'student'}
  expect((await write(teacher,exam+'/start',undefined,'POST',preview)).status()).toBe(403)
  expect((await write(teacher,exam+'/my-submission',{submit:true,answers:[]},'POST',preview)).status()).toBe(403)
  expect((await student.request.get('/api'+exam+'/class-roster')).status()).toBe(403)
  const studentPage=await student.newPage()
  await studentPage.goto(root+'/quizzes/'+id)
  await studentPage.getByRole('button',{name:'開始測驗',exact:true}).click()
  await studentPage.getByRole('checkbox',{name:'甲'}).check()
  await studentPage.getByRole('checkbox',{name:'丙'}).check()
  await studentPage.getByRole('textbox',{name:'題目 2 作答'}).fill('合成說明')
  studentPage.once('dialog',d=>d.accept())
  await studentPage.getByRole('button',{name:'提交給教師／助教'}).click()
  await expect(studentPage.getByText('作答已成功提交，教師與助教現在可以查看。')).toBeVisible()
  await page.setViewportSize({width:1366,height:900})
  await page.goto(root+'/quizzes/'+id)
  await expect(page.getByRole('heading',{name:/全班作答清單/})).toBeVisible()
  await page.getByRole('button',{name:'查看作答'}).click()
  await expect(page.getByText('合成說明',{exact:true}).first()).toBeVisible()
  await page.getByLabel('人工分數').fill('30')
  await page.getByRole('button',{name:'儲存評分草稿'}).click()
  await expect(page.getByText('人工評分草稿已儲存，尚未發布。')).toBeVisible()
  const book=async ctx=>(await ctx.request.get('/api'+root+'/gradebook')).json()
  expect((await book(student)).scores).toHaveLength(0)
  await page.getByLabel('人工分數').fill('35')
  page.once('dialog',d=>d.accept())
  await page.getByRole('button',{name:'確認成績並寫入正式成績簿'}).click()
  await expect(page.getByText('教師已確認，正式成績簿已更新。')).toBeVisible()
  expect((await book(student)).scores[0].score).toBe(55)
  expect((await book(student)).finals).toHaveLength(0)
  expect((await write(teacher,root+'/gradebook/adjustment',{adjustment:20},'PUT')).ok()).toBeTruthy()
  expect((await write(teacher,root+'/gradebook/publish-finals')).ok()).toBeTruthy()
  expect((await book(student)).finals[0].publishedTotal).toBe(111.67)
  expect((await write(ta,base+'/gradebook/publish-finals')).status()).toBe(403)
  expect(errors).toEqual([])
  for(const ctx of [teacher,student,peer,ta])await ctx.close()
})

test('PDF publication, course-specific TA limits and public discussions',async({playwright,baseURL})=>{
  test.setTimeout(150000)
  async function session(name){
    const ctx=await playwright.request.newContext({baseURL})
    expect((await retryLogin(ctx,{email:name+'@portal-demo.example.invalid',password:process.env.PORTAL_DEMO_PASSWORD})).ok()).toBeTruthy()
    return ctx
  }
  async function write(ctx,path,data,method='POST',multipart){
    const {token}=await (await ctx.get('/api/auth/csrf')).json()
    return ctx.fetch('/api'+path,{method,...(multipart?{multipart}:{data:typeof data==='boolean'?JSON.stringify(data):data}),headers:{'X-CSRF-TOKEN':token,...(typeof data==='boolean'?{'Content-Type':'application/json'}:{})}})
  }
  const teacher=await session('faculty1'),student=await session('student02'),assistant=await session('ta'),other=await session('faculty2')
  const root='/courses/'+(await (await write(teacher,'/courses',{code:'PDF-'+Date.now(),title:'報告與討論驗收',termCode:'115-S'})).json()).id
  const me=async ctx=>(await (await ctx.get('/api/auth/session')).json()).user.id
  const taId=await me(assistant)
  for(const [userId,courseRole] of [[taId,'ta'],[await me(student),'student']])
    expect((await write(teacher,root+'/members',{userId,courseRole})).status()).toBe(204)
  await write(teacher,root+'/activate')
  expect((await write(teacher,root+'/members/'+taId+'/ta-restrictions',['assessments'],'PUT')).ok()).toBeTruthy()
  const report={title:'觀察報告',maxScore:80,weight:1,notifyStudents:false}
  expect((await write(assistant,root+'/reports',report)).status()).toBe(403)
  const teaching=(await (await assistant.get('/api/courses')).json()).find(c=>c.code==='DEMO-3')
  expect((await write(assistant,'/courses/'+teaching.id+'/reports',report)).ok()).toBeTruthy()
  const reportPath='/reports/'+(await (await write(teacher,root+'/reports',report)).json()).id
  await write(teacher,reportPath+'/publish')
  const file={name:'sample.pdf',mimeType:'application/pdf',buffer:Buffer.from('%PDF-1.4 synthetic')}
  expect((await write(student,reportPath+'/submit',null,'POST',{file})).status()).toBe(204)
  const submissions=async ctx=>(await (await ctx.get('/api'+reportPath+'/submissions')).json())
  const [one]=await submissions(teacher),grade=reportPath+'/submissions/'+one.id+'/grade'
  expect((await write(teacher,grade,{score:70,feedback:'評分草稿',expectedVersion:one.version},'PUT')).status()).toBe(204)
  expect((await submissions(student))[0]).toMatchObject({score:null,feedback:''})
  const history=async()=>(await (await student.get('/api/learning-history?course='+root.split('/').pop())).json()).items
  expect((await history()).find(i=>i.kind==='report')).toMatchObject({state:'pending',score:null,feedback:''})
  await write(teacher,grade,{score:74,feedback:'已公布評語',expectedVersion:one.version+1,publish:true},'PUT')
  await write(teacher,grade,{score:77,feedback:'後續草稿',expectedVersion:one.version+2},'PUT')
  expect((await submissions(student))[0]).toMatchObject({score:74,feedback:'已公布評語'})
  expect((await history()).find(i=>i.kind==='report')).toMatchObject({state:'graded',score:74,feedback:'已公布評語'})
  const id=crypto.randomUUID(),q=root+'/questions/'+id
  expect((await write(teacher,root+'/questions/with-pdf',null,'POST',{id,text:'課程公開問題',file})).status()).toBe(204)
  expect((await student.get('/api'+q+'/attachment')).ok()).toBeTruthy()
  expect((await other.get('/api'+q+'/attachment')).status()).toBe(403)
  expect((await write(student,q+'/replies',{id:crypto.randomUUID(),text:'同課回覆'})).status()).toBe(204)
  expect((await write(teacher,q+'/lock',true,'PUT')).status()).toBe(204)
  expect((await write(student,q+'/replies',{id:crypto.randomUUID(),text:'鎖定後回覆'})).status()).toBe(409)
  expect((await write(teacher,q+'/lock',false,'PUT')).status()).toBe(204)
  expect((await write(student,q+'/vote',true,'PUT')).status()).toBe(204)
  for(const ctx of [teacher,student,assistant,other])await ctx.dispose()
})
