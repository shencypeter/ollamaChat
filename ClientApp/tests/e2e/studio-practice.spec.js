import {retryLogin} from '../helpers/auth'
import {test,expect} from '@playwright/test'
import {readFileSync} from 'node:fs'
test('staff can submit private practice quizzes and reports, grade in teaching mode and read own results',async({playwright,browser,baseURL})=>{
 const state=JSON.parse(readFileSync(new URL('../../../.codex-build/portal-smoke.json',import.meta.url),'utf8')),teacher=await playwright.request.newContext({baseURL}),student=await playwright.request.newContext({baseURL}),ta=await playwright.request.newContext({baseURL})
 async function write(ctx,path,data,method='POST',practice=false,extra={}){const token=(await(await ctx.get('/api/auth/csrf')).json()).token;return ctx.fetch('/api'+path,{method,...(data?{data}:{}),...extra,headers:{'X-CSRF-TOKEN':token,...(practice?{'X-Portal-View':'student'}:{})}})}
 async function read(ctx,path,practice=false){return ctx.get('/api'+path,{headers:practice?{'X-Portal-View':'student'}:{}})}
 for(const [ctx,role] of [[teacher,'Professor'],[student,'Student'],[ta,'TeachingAssistant']])expect((await retryLogin(ctx,{email:state.accounts.find(a=>a.role===role).email,password:state.password})).status()).toBe(200)
 const course=await(await write(teacher,'/courses',{code:'PRACTICE-'+Date.now(),title:'合成練習提交課程',termCode:'115-1'})).json(),base=`/courses/${course.id}`
 for(const [role,courseRole] of [['Student','student'],['TeachingAssistant','ta']])await write(teacher,base+'/members',{userId:state.accounts.find(a=>a.role===role).id,courseRole})
 const created=await write(teacher,base+'/quizzes',{assignment:{title:'練習測驗',maxScore:20,questions:[{promptText:'請說明合成推論。'}]},weight:30});expect(created.ok()).toBe(true);const quiz=await created.json();await write(teacher,`/assignments/${quiz.id}/publish`)
 const question=(await(await read(teacher,`/assignments/${quiz.id}`,true)).json()).questions[0]
 const submitted=await write(teacher,`/assignments/${quiz.id}/my-submission`,{expectedVersion:0,submit:true,answers:[{questionId:question.id,text:'本人的合成練習答案'}]},'POST',true);expect(submitted.status()).toBe(200);const own=await submitted.json();expect(own.status).toBe('submitted')
 expect((await(await read(teacher,`/assignments/${quiz.id}/submissions`)).json())).toHaveLength(0)
 expect((await write(teacher,`/assignments/${quiz.id}/my-submission`,{expectedVersion:own.version,submit:true,answers:[{questionId:question.id,text:'禁止覆寫'}]},'POST',true)).status()).toBe(409)
 const report=await(await write(teacher,base+'/reports',{title:'練習 PDF 作業',maxScore:100,weight:70})).json();await write(teacher,`/reports/${report.id}/publish`)
 const pdf=Buffer.from('%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF')
 expect((await write(teacher,`/reports/${report.id}/submit`,null,'POST',true,{multipart:{file:{name:'practice.pdf',mimeType:'application/pdf',buffer:pdf}}})).status()).toBe(204)
 expect((await(await read(teacher,`/reports/${report.id}/submissions`)).json())).toHaveLength(0)
 const practicePdf=(await(await read(teacher,`/reports/${report.id}/submissions`,true)).json())[0]
 expect((await read(teacher,`/reports/${report.id}/submissions/${practicePdf.id}/preview`,true)).status()).toBe(200)
 expect((await read(ta,`/reports/${report.id}/submissions/${practicePdf.id}/preview`)).status()).toBe(404)
 expect((await read(student,base+'/practice')).status()).toBe(403)
 let records=await(await read(teacher,base+'/practice')).json();expect(records.records).toHaveLength(2);expect(records.average).toBeNull()
 expect((await(await read(ta,base+'/practice')).json()).records).toHaveLength(0)
 const qr=records.records.find(r=>r.kind==='quiz'),rr=records.records.find(r=>r.kind==='report')
 expect((await write(ta,`${base}/practice/quiz/${qr.id}/grade`,{score:10},'PUT')).status()).toBe(404)
 expect((await write(teacher,`${base}/practice/quiz/${qr.id}/grade`,{score:10},'PUT',true)).status()).toBe(403)
 expect((await write(teacher,`${base}/practice/quiz/${qr.id}/grade`,{score:21},'PUT')).status()).toBe(400)
 expect((await write(teacher,`${base}/practice/quiz/${qr.id}/grade`,{score:10,feedback:'練習人工回饋',expectedGradeId:null},'PUT')).status()).toBe(204)
 expect((await write(teacher,`${base}/practice/quiz/${qr.id}/grade`,{score:11,expectedGradeId:null},'PUT')).status()).toBe(409)
 expect((await write(teacher,`${base}/practice/report/${rr.id}/grade`,{score:80,feedback:'PDF 練習回饋',expectedVersion:rr.version},'PUT')).status()).toBe(204)
 expect((await write(teacher,`${base}/practice/report/${rr.id}/grade`,{score:90,expectedVersion:rr.version},'PUT')).status()).toBe(409)
 records=await(await read(teacher,base+'/practice',true)).json();expect(records.average).toBe(71);expect(records.canGrade).toBe(false)
 const officialBook=await(await read(teacher,base+'/gradebook')).json();expect(officialBook.scores).toHaveLength(0);expect(officialBook.classAverage).toBeNull()
 const context=await browser.newContext({storageState:await teacher.storageState()}),page=await context.newPage()
 await page.goto(baseURL+`/courses/${course.id}?section=grades`);await expect(page.getByRole('heading',{name:'我的練習提交與人工評分',exact:true})).toBeVisible();await page.getByLabel('練習得分',{exact:true}).first().fill('10');await page.getByRole('button',{name:'儲存練習成績',exact:true}).first().click();await expect(page.getByText('練習成績已儲存，切換學生視角即可查看；正式成績不受影響。',{exact:true})).toBeVisible()
 await page.getByRole('button',{name:'切換學生視角',exact:true}).click();await page.goto(baseURL+`/courses/${course.id}?section=grades`)
 await expect(page.getByRole('heading',{name:'我的練習成績',exact:true})).toBeVisible();await expect(page.getByText('我的練習加權平均：71.00 分',{exact:true})).toBeVisible();expect(await page.getByRole('button',{name:'儲存練習成績',exact:true}).count()).toBe(0)
 // Submit through the actual quiz UI as the assistant, in a separate practice namespace.
 const tcontext=await browser.newContext({storageState:await ta.storageState()}),tp=await tcontext.newPage();await tp.goto(baseURL+'/courses');await tp.getByRole('button',{name:'切換學生視角',exact:true}).click();await tp.goto(baseURL+'/quizzes/'+quiz.id)
 await tp.getByRole('textbox').fill('助教自己的練習答案');tp.once('dialog',d=>d.accept());await tp.getByRole('button',{name:'提交給教師／助教',exact:true}).click();await expect(tp.getByText('練習作答已提交，不計入正式學生紀錄。',{exact:true})).toBeVisible()
 expect((await(await read(teacher,`/assignments/${quiz.id}/submissions`)).json())).toHaveLength(0)
 const reportUI=await(await write(teacher,base+'/reports',{title:'UI 練習報告',maxScore:100,weight:1})).json();await write(teacher,`/reports/${reportUI.id}/publish`)
 await tp.goto(baseURL+'/reports/'+reportUI.id);await tp.getByLabel('PDF 報告（最多 20 MB）').setInputFiles({name:'ui-practice.pdf',mimeType:'application/pdf',buffer:pdf});tp.once('dialog',d=>d.accept());await tp.getByRole('button',{name:'繳交 PDF 報告',exact:true}).click();await expect(tp.getByText('PDF 報告已繳交。',{exact:true})).toBeVisible()
 expect((await(await read(teacher,`/reports/${reportUI.id}/submissions`)).json())).toHaveLength(0)

 const stages=['premorbid','illnessCourse','functionalAssessment','psychosocial'],caseInput={courseId:course.id,title:'合成練習案例',material:'合成資料，未含真實病例。',prompts:Object.fromEntries(stages.map(s=>[s,'依據合成案例分析。'])),referencePoints:Object.fromEntries(stages.map(s=>[s,'']))}
 const createdCase=await(await write(teacher,'/studio/admin/cases',caseInput)).json();expect((await write(teacher,`/studio/admin/cases/${createdCase.id}/publish`,{expectedDraftVersionId:createdCase.versionId})).ok()).toBe(true)
 const attempt=await(await read(teacher,`/studio/cases/${createdCase.id}/attempt`,true)).json()
 expect((await write(teacher,`/studio/attempts/${attempt.id}`,{expectedVersion:attempt.version,submit:true,answers:Object.fromEntries(stages.map(s=>[s,'合成案例練習答案']))},'PUT',true)).ok()).toBe(true)
 const stats=await(await read(teacher,`/studio/teaching/courses/${course.id}/statistics`)).json()
 // Teaching statistics must still exclude all practice case attempts.
 expect(stats.submittedAttempts).toBe(0)
 await context.close();await tcontext.close();for(const ctx of [teacher,student,ta])await ctx.dispose()
})
