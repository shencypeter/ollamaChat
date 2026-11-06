import {retryLogin} from '../helpers/auth'
import {test,expect} from '@playwright/test'
import {readFileSync} from 'node:fs'
test.skip(process.env.OLLAMA_INSPECTION_STUB!=='1','Only run with the loopback synthetic model fixture.')
test.use({actionTimeout:15000})
const state=JSON.parse(readFileSync(new URL('../../../.codex-build/portal-smoke.json',import.meta.url),'utf8'))
const stages=['premorbid','illnessCourse','functionalAssessment','psychosocial']
test('S1–S3a, T1–T4 and A1–A6: versioned text feedback, private drafts, formal history and administration',async({playwright,browser,baseURL})=>{
 test.setTimeout(180000)
 const teacher=await playwright.request.newContext({baseURL}),learner=await playwright.request.newContext({baseURL}),ta=await playwright.request.newContext({baseURL})
 async function write(ctx,path,data,method='POST',headers={}){const token=(await(await ctx.get('/api/auth/csrf')).json()).token;return ctx.fetch('/api'+path,{method,headers:{'X-CSRF-TOKEN':token,...headers},...(data?{data}:{})})}
 for(const [ctx,role] of [[teacher,'Professor'],[learner,'Student'],[ta,'TeachingAssistant']])expect((await retryLogin(ctx,{email:state.accounts.find(a=>a.role===role).email,password:state.password})).status()).toBe(200)
 const settings=await(await teacher.get('/api/studio/admin/settings')).json()
 async function configure(values){const current=await(await teacher.get('/api/studio/admin/settings')).json();const response=await write(teacher,'/studio/admin/settings',{...current,...values,expectedVersion:current.version},'PUT');expect(response.status()).toBe(204)}
 try{
  const course=await(await write(teacher,'/courses',{code:'STUDIO-'+state.run+'-'+Date.now(),title:'案例合成測試課程',termCode:'115-1'})).json()
  for(const role of ['Student','TeachingAssistant'])expect((await write(teacher,`/courses/${course.id}/members`,{userId:state.accounts.find(a=>a.role===role).id,courseRole:role==='Student'?'student':'ta'})).status()).toBe(204)
  const input={courseId:course.id,title:'合成四階段案例',material:'合成案例原文：個案在提示下完成任務，未提供其他資料。',prompts:Object.fromEntries(stages.map(s=>[s,'請區分案例事實與推論。'])),referencePoints:Object.fromEntries(stages.map(s=>[s,'ADMIN_REFERENCE_SECRET']))}
  let response=await write(teacher,'/studio/admin/cases',input);expect(response.status()).toBe(200);const created=await response.json()
  expect((await learner.get('/api/studio/cases/'+created.id)).status()).toBe(404)
  expect((await ta.get('/api/studio/admin/cases')).status()).toBe(403)
  expect((await learner.get('/api/studio/teaching/courses')).status()).toBe(403)
  expect((await write(teacher,`/studio/admin/cases/${created.id}/publish`,{expectedDraftVersionId:created.versionId})).status()).toBe(204)
  const published=await(await learner.get('/api/studio/cases/'+created.id)).json();expect(published.referencePoints).toBeNull();expect(JSON.stringify(published)).not.toContain('ADMIN_REFERENCE_SECRET')
  const profileBefore=await(await learner.get('/api/studio/profile')).json();expect((await learner.get(`/api/studio/cases/${created.id}/attempt`)).status()).toBe(profileBefore.completed?200:428)
  const studentContext=await browser.newContext({viewport:{width:1700,height:1050}}),page=await studentContext.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message))
  await page.goto(baseURL+'/login');await page.getByLabel('電子郵件',{exact:true}).fill(state.accounts.find(a=>a.role==='Student').email);await page.getByLabel('密碼',{exact:true}).fill(state.password);await page.getByRole('button',{name:'登入工作區',exact:true}).click();await expect(page).toHaveURL(profileBefore.completed?/student\/cases$/:/student\/profile/);if(profileBefore.completed)await page.goto(baseURL+'/student/profile');await expect(page).toHaveURL(/student\/profile/)
  await page.getByLabel('姓名',{exact:true}).fill('案例學生');await page.getByLabel('學號',{exact:true}).fill('QA-'+state.run);await page.getByLabel('系級（選填）',{exact:true}).fill('合成系級');await page.getByRole('button',{name:'儲存並開始案例學習'}).click();await expect(page).toHaveURL(/student\/cases$/)
  expect(await page.locator('a[href="/courses"]').count()).toBeGreaterThan(0);expect(await page.locator('a[href="/ai"]').count()).toBe(0)
  await page.locator(`a[href="/student/cases/${created.id}"]`).click();await expect(page.getByRole('heading',{name:'案例原文',exact:true})).toBeVisible();await expect(page.getByRole('heading',{name:'學生作答',exact:true})).toBeVisible();await expect(page.getByRole('heading',{name:'AI 回饋',exact:true})).toBeVisible()
  await page.getByRole('textbox',{name:'病前功能 作答',exact:true}).fill('SECRET_PRIVATE_DRAFT');await page.getByRole('button',{name:'儲存四階段草稿'}).click();await expect(page.getByText('四階段草稿已儲存。',{exact:true})).toBeVisible()
  const studentId=state.accounts.find(a=>a.role==='Student').id
  const tbase=`/studio/teaching/courses/${course.id}`
  expect((await(await teacher.get('/api'+tbase+'/students/'+studentId)).json()).records).toEqual([])
  const first=await(await learner.get(`/api/studio/cases/${created.id}/attempt`)).json();expect((await write(teacher,`/studio/attempts/${first.id}`,{expectedVersion:first.version,answers:Object.fromEntries(stages.map(s=>[s,'blocked']))},'PUT')).status()).toBe(404)
  await configure({timeoutSeconds:5,maxRequestsPerRevision:5});const timeoutMarker='STUDIO_TIMEOUT_ONCE-'+state.run+'-'+Date.now()
  await page.getByRole('textbox',{name:'病前功能 作答',exact:true}).fill(timeoutMarker)
  await page.getByRole('button',{name:'儲存四階段草稿'}).click();await expect(page.getByRole('button',{name:'取得 AI 文字回饋'})).toBeEnabled();await page.getByRole('button',{name:'取得 AI 文字回饋'}).click();await expect(page.getByRole('alert')).toContainText('S3a', {timeout:10000});await expect(page.getByRole('textbox',{name:'病前功能 作答',exact:true})).toHaveValue(timeoutMarker)
  await page.getByRole('button',{name:'重試 AI 回饋'}).click();await expect(page.getByText('合成案例文字回饋，不代表真實模型品質',{exact:true})).toBeVisible();expect(await page.locator('.score').count()).toBe(0)
  await page.evaluate(()=>{document.activeElement?.blur();window.scrollTo(0,0)});await page.screenshot({path:'test-results/studio-student-desktop.png',fullPage:true});await page.setViewportSize({width:390,height:844});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await page.screenshot({path:'test-results/studio-student-mobile.png',fullPage:true})
  await page.getByRole('textbox',{name:'病前功能 作答',exact:true}).fill('=1+1 合成正式分析');await expect(page.getByRole('button',{name:'取得 AI 文字回饋'})).toBeDisabled()
  for(const [label,text] of [['疾病病程','合成病程分析'],['功能評估結果','合成功能分析'],['心理社會條件','合成心理社會分析']]){await page.getByRole('button',{name:new RegExp(label)}).click();await page.getByRole('textbox',{name:label+' 作答',exact:true}).fill(text)}
  await page.getByRole('button',{name:'儲存四階段草稿'}).click();await page.getByRole('button',{name:'取得 AI 文字回饋'}).click();await expect(page.getByText('合成案例文字回饋，不代表真實模型品質',{exact:true})).toBeVisible();page.once('dialog',d=>d.accept());await page.getByRole('button',{name:'正式提交',exact:true}).click();await expect(page.getByText('案例作答已正式提交。',{exact:true})).toBeVisible()
  const history=await(await ta.get('/api'+tbase+'/students/'+studentId)).json();expect(history.records).toHaveLength(1);expect(history.records[0].attempt.answers).toHaveLength(4);expect(JSON.stringify(history)).not.toContain('SECRET_PRIVATE_DRAFT');expect(JSON.stringify(history)).not.toContain('ADMIN_REFERENCE_SECRET');expect(history.records[0].attempt.feedback).toHaveLength(1)
  const stats=await(await teacher.get('/api'+tbase+'/statistics')).json();expect(stats.submittedStudents).toBe(1);expect(stats.submittedAttempts).toBe(1)
  expect((await write(learner,tbase+'/export')).status()).toBe(403)
  const csv=await write(teacher,tbase+'/export');expect(csv.status()).toBe(200);const csvText=(await csv.body()).toString();expect(csvText).toContain("'=1+1");expect(csvText).not.toContain('SECRET_PRIVATE_DRAFT')
  const other=await(await write(teacher,'/courses',{code:'STUDIO-OTHER-'+Date.now(),title:'隔離課程',termCode:'115-1'})).json();expect((await ta.get(`/api/studio/teaching/courses/${other.id}/statistics`)).status()).toBe(403)
  const adminContext=await browser.newContext({storageState:await teacher.storageState()}),admin=await adminContext.newPage();await admin.goto(baseURL+'/');await expect(admin).toHaveURL(/\/teaching$/);await admin.goto(baseURL+'/teaching?course='+course.id);await admin.getByRole('link',{name:'查看學習歷程',exact:true}).first().click();await expect(admin.getByRole('heading',{name:/案例學生/})).toBeVisible()
  await admin.goto(baseURL+'/teaching/statistics?course='+course.id);await expect(admin.getByRole('heading',{name:'正式提交概況'})).toBeVisible();await admin.goto(baseURL+'/teaching/export?course='+course.id);await expect(admin.getByRole('button',{name:'下載 CSV'})).toBeVisible()
  for(const [path,title] of [['/admin','管理後台首頁'],['/admin/accounts','帳號管理'],['/admin/cases','案例維護'],['/admin/references','標準答案／重點清單'],['/admin/templates','AI 回饋模板'],['/admin/settings','系統參數＋稽核軌跡']]){await admin.goto(baseURL+path);await expect(admin.getByRole('heading',{name:title,exact:true}).first()).toBeVisible()}
  await admin.goto(baseURL+'/admin/cases');await admin.locator(`[data-case-id="${created.id}"]`).getByRole('button').click();await admin.getByRole('textbox',{name:'病前功能 結構提示',exact:true}).fill('合成新版結構提示');await expect(admin.getByRole('button',{name:'發布已儲存版本'})).toBeDisabled();await admin.getByRole('button',{name:'儲存新版本草稿'}).click();await expect(admin.getByText('新案例版本已儲存為草稿。發布前學生看不到新內容。',{exact:true})).toBeVisible()
  await admin.goto(baseURL+'/admin/references');await admin.locator(`[data-case-id="${created.id}"]`).getByRole('button').click();await admin.getByRole('textbox',{name:'病前功能 重點清單',exact:true}).fill('PRIVATE_REFERENCE_UPDATED');await admin.getByRole('button',{name:'儲存新版本草稿'}).click();await expect(admin.getByText('新案例版本已儲存為草稿。發布前學生看不到新內容。',{exact:true})).toBeVisible();expect(JSON.stringify(await(await learner.get('/api/studio/cases/'+created.id)).json())).not.toContain('PRIVATE_REFERENCE_UPDATED')
  await admin.goto(baseURL+'/admin/settings');await admin.getByLabel('每答案版本最多請求次數',{exact:true}).fill('4');await admin.getByRole('button',{name:'儲存系統參數'}).click();await expect(admin.getByText('案例回饋系統參數已更新。',{exact:true})).toBeVisible()
  await admin.goto(baseURL+'/admin/templates');await admin.getByLabel('回饋模板',{exact:true}).fill('合成 QA 模板，仍只提供文字回饋。');await admin.getByRole('button',{name:'儲存並啟用新模板版本'}).click();await expect(admin.getByText('回饋模板新版本已啟用；既有回饋保留原模板版本。',{exact:true})).toBeVisible()
  const latest=await(await teacher.get('/api/studio/admin/cases/'+created.id)).json();response=await write(teacher,'/studio/admin/cases/'+created.id,{...input,title:'合成四階段案例 v2',expectedDraftVersionId:latest.draftVersionId},'PUT');expect(response.status()).toBe(200);const next=await response.json();expect((await(await learner.get('/api/studio/cases/'+created.id)).json()).versionNo).toBe(1);await write(teacher,`/studio/admin/cases/${created.id}/publish`,{expectedDraftVersionId:next.versionId});const fresh=await(await learner.get(`/api/studio/cases/${created.id}/attempt`)).json();expect(fresh.id).not.toBe(first.id);expect(fresh.answers).toHaveLength(0);expect((await(await teacher.get('/api'+tbase+'/students/'+studentId)).json()).records[0].caseDetail.versionNo).toBe(1)
  await configure({feedbackEnabled:false});const saved=await(await write(learner,'/studio/attempts/'+fresh.id,{expectedVersion:0,answers:Object.fromEntries(stages.map(s=>[s,'合成新版本']))},'PUT')).json();expect((await write(learner,'/studio/attempts/'+fresh.id+'/feedback',{answerVersionId:saved.answers[0].id})).status()).toBe(409)
  expect(errors).toEqual([]);await studentContext.close();await adminContext.close()
 }finally{await configure({feedbackEnabled:settings.feedbackEnabled,timeoutSeconds:settings.timeoutSeconds,maxRequestsPerRevision:settings.maxRequestsPerRevision});for(const c of [teacher,learner,ta])await c.dispose()}
})

test('private feedback refresh preserves optimistic versions, staff practice and in-progress case versions',async({playwright,browser,baseURL})=>{
 test.setTimeout(90000)
 const teacher=await playwright.request.newContext({baseURL}),student=await playwright.request.newContext({baseURL})
 async function write(ctx,path,data,method='POST',practice=false){const token=(await(await ctx.get('/api/auth/csrf')).json()).token;return ctx.fetch('/api'+path,{method,headers:{'X-CSRF-TOKEN':token,...(practice?{'X-Portal-View':'student'}:{})},...(data?{data}:{})})}
 for(const [ctx,role] of [[teacher,'Professor'],[student,'Student']])expect((await retryLogin(ctx,{email:state.accounts.find(a=>a.role===role).email,password:state.password})).status()).toBe(200)
 const studentId=state.accounts.find(a=>a.role==='Student').id
 const course=await(await write(teacher,'/courses',{code:'STUDIO-RACE-'+Date.now(),title:'版本隔離合成課程',termCode:'115-1'})).json();await write(teacher,`/courses/${course.id}/members`,{userId:studentId,courseRole:'student'})
 const input={courseId:course.id,title:'版本隔離案例',material:'合成案例原文',prompts:Object.fromEntries(stages.map(s=>[s,'合成結構提示'])),referencePoints:Object.fromEntries(stages.map(s=>[s,'PRIVATE_REFERENCE']))}
 const created=await(await write(teacher,'/studio/admin/cases',input)).json();await write(teacher,`/studio/admin/cases/${created.id}/publish`,{expectedDraftVersionId:created.versionId})
 const own=await(await student.get(`/api/studio/cases/${created.id}/attempt`)).json()
 const context=await browser.newContext({storageState:await student.storageState()}),page=await context.newPage();await page.goto(baseURL+'/student/cases/'+created.id)
 const answer=page.getByRole('textbox',{name:'病前功能 作答',exact:true});await answer.fill('STUDIO_FAIL');await page.getByRole('button',{name:'儲存四階段草稿'}).click();await page.getByRole('button',{name:'取得 AI 文字回饋'}).click();await expect(page.getByRole('alert')).toContainText('S3a')
 let latest=await(await student.get('/api/studio/attempts/'+own.id)).json();expect((await write(student,'/studio/attempts/'+own.id,{expectedVersion:latest.attempt.version,answers:{...Object.fromEntries(stages.map(s=>[s,''])),premorbid:'其他分頁的已存答案'}},'PUT')).status()).toBe(200)
 await page.getByRole('button',{name:'重新載入回饋狀態'}).click();await answer.fill('本分頁未存答案');await page.getByRole('button',{name:'儲存四階段草稿'}).click();await expect(page.getByRole('alert').first()).toContainText('作答版本已更新');await expect(answer).toHaveValue('本分頁未存答案')
 page.once('dialog',d=>d.accept());await page.getByRole('button',{name:'重新載入已儲存版本'}).click();await expect(answer).toHaveValue('其他分頁的已存答案')
 const practice=await(await teacher.get(`/api/studio/cases/${created.id}/attempt`,{headers:{'X-Portal-View':'student'}})).json();expect(practice.id).not.toBe(own.id);expect(practice.isPractice).toBe(true)
 expect((await teacher.get('/api/studio/attempts/'+own.id,{headers:{'X-Portal-View':'student'}})).status()).toBe(404)
 expect((await write(teacher,'/studio/attempts/'+practice.id,{expectedVersion:0,submit:true,answers:Object.fromEntries(stages.map(s=>[s,'練習答案']))},'PUT',true)).status()).toBe(403)
 expect((await(await teacher.get(`/api/studio/teaching/courses/${course.id}/students/${studentId}`)).json()).records).toEqual([])
 const updated=await(await write(teacher,'/studio/admin/cases/'+created.id,{...input,title:'已更新的案例素材',expectedDraftVersionId:created.versionId},'PUT')).json();await write(teacher,`/studio/admin/cases/${created.id}/publish`,{expectedDraftVersionId:updated.versionId})
 const resumed=await(await student.get(`/api/studio/cases/${created.id}/attempt`)).json();expect(resumed.id).toBe(own.id);const resumedContext=await(await student.get('/api/studio/attempts/'+own.id)).json();expect(resumedContext.caseDetail.versionNo).toBe(1);expect(JSON.stringify(resumedContext.caseDetail)).not.toContain('PRIVATE_REFERENCE')
 await context.close();await teacher.dispose();await student.dispose()
})
