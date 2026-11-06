import {retryLogin} from '../helpers/auth'
import {test,expect} from '@playwright/test'
import {readFileSync} from 'node:fs'
test('quiz composition keeps 60 automatic plus 40 essay points and private grading guidance',async({playwright,browser,baseURL})=>{
 const state=JSON.parse(readFileSync(new URL('../../../.codex-build/portal-smoke.json',import.meta.url),'utf8')),fixture=JSON.parse(readFileSync(new URL('../../../docs/examples/clinical-psychology-basics-quiz.json',import.meta.url),'utf8')),teacher=await playwright.request.newContext({baseURL}),student=await playwright.request.newContext({baseURL})
 async function write(ctx,path,data,method='POST'){const token=(await(await ctx.get('/api/auth/csrf')).json()).token;return ctx.fetch('/api'+path,{method,data,headers:{'X-CSRF-TOKEN':token}})}
 for(const [ctx,role] of [[teacher,'Professor'],[student,'Student']])expect((await retryLogin(ctx,{email:state.accounts.find(a=>a.role===role).email,password:state.password})).status()).toBe(200)
 const course=await(await write(teacher,'/courses',{code:'PLAN-'+Date.now(),title:'合成配分驗證課程',termCode:'115-1'})).json();await write(teacher,`/courses/${course.id}/members`,{userId:state.accounts.find(a=>a.role==='Student').id,courseRole:'student'})
 const created=await write(teacher,`/courses/${course.id}/quizzes`,{assignment:{...fixture.assignment,title:'合成配分試卷'},weight:1});expect(created.status()).toBe(200);const quiz=await created.json()
 const context=await browser.newContext({storageState:await teacher.storageState()}),page=await context.newPage();await page.goto(baseURL+'/quizzes/'+quiz.id)
 const plan=page.getByRole('region',{name:'測驗配分組成'});await expect(plan.getByText('合計 100 分',{exact:true})).toBeVisible();await expect(plan).toContainText('客觀題 60 分 ＋ 申論題 40 分')
 await page.getByRole('button',{name:'編輯草稿',exact:true}).click();await expect(page.getByLabel('教師評分參考（學生不可見）')).toHaveValue(/36–40 分/)
 await page.getByLabel('題目 13 配分',{exact:true}).fill('35');await expect(page.getByRole('button',{name:'儲存測驗草稿',exact:true})).toBeDisabled();await page.getByLabel('題目 13 配分',{exact:true}).fill('40');await page.getByRole('button',{name:'儲存測驗草稿',exact:true}).click();await expect(page.getByText('測驗草稿已儲存。',{exact:true})).toBeVisible()
 const detail=await(await teacher.get(`/api/assignments/${quiz.id}`)).json();expect(detail.questions).toHaveLength(13);expect(detail.questions.reduce((n,q)=>n+q.points,0)).toBe(100);expect(detail.questions[12].gradingGuidance).toContain('0–9 分')
 expect((await write(teacher,`/assignments/${quiz.id}/publish`,{})).status()).toBe(200)
 const safe=await(await student.get(`/api/assignments/${quiz.id}`)).json();expect(safe.questions[12].gradingGuidance).toBeNull();expect(safe.questions.every(q=>q.standardAnswer===null)).toBe(true)
 const submit=await write(student,`/assignments/${quiz.id}/my-submission`,{expectedVersion:0,submit:true,answers:detail.questions.map(q=>({questionId:q.id,text:q.questionType==='essay'?'合成申論練習':JSON.stringify(q.standardAnswer)}))});expect(submit.status()).toBe(200);const own=await submit.json(),snapshots=await(await teacher.get(`/api/assignments/${quiz.id}/submissions/${own.id}/revisions`)).json(),marks=await(await teacher.get(`/api/assignments/${quiz.id}/revisions/${snapshots[0].id}/quiz-grading`)).json();expect(marks.objectiveScore).toBe(60);expect(marks.pendingEssays).toBe(1);expect(marks.marks.find(m=>m.questionType==='essay').points).toBe(40)
 await context.close();await teacher.dispose();await student.dispose()
})

