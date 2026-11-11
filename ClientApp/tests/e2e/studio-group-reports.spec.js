import {test,expect} from '@playwright/test'
import {retryLogin} from '../helpers/auth'

test.skip(process.env.PORTAL_TEST_DISPOSABLE!=='1' || !process.env.PORTAL_DEMO_PASSWORD,'Requires disposable course demo')
test('group submission, private evaluation, revision, individual release and snapshot access',async({browser,baseURL})=>{
  test.setTimeout(120000)
  const contexts=[]
  async function login(name){
    const c=await browser.newContext({baseURL});contexts.push(c)
    expect((await retryLogin(c.request,{email:name+'@portal-demo.example.invalid',password:process.env.PORTAL_DEMO_PASSWORD})).ok()).toBeTruthy()
    const {token}=await (await c.request.get('/api/auth/csrf')).json()
    return {c,get:path=>c.request.get('/api'+path),write:(path,data,method='POST',multipart)=>c.request.fetch('/api'+path,{method,data,multipart,headers:{'X-CSRF-TOKEN':token}})}
  }
  try{
    const t=await login('faculty1'),leader=await login('student01'),member=await login('student02'),other=await login('student10'),ta=await login('ta2')
    const courses=await (await t.get('/courses')).json(),course=courses.find(c=>c.code==='DEMO-1'),base='/courses/'+course.id
    const groups=await (await leader.get(base+'/groups')).json();expect(groups).toHaveLength(1)
    const group=groups[0],subject=group.members.find(m=>m.isLeader).id,me=(await (await member.get('/auth/session')).json()).user.id
    const input={title:'小組流程驗收',submissionMode:'group',evaluationMode:'consensus',groupIds:(await (await t.get(base+'/groups')).json()).map(g=>g.id)}
    const foreign=await (await t.get('/courses/'+courses.find(c=>c.code==='DEMO-2').id+'/groups')).json()
    expect((await t.write(base+'/reports',{...input,groupIds:[foreign[0].id]})).status()).toBe(400)
    const create=await t.write(base+'/reports',input);expect(create.ok()).toBeTruthy()
    const {id}=await create.json(),report='/reports/'+id
    expect((await t.write(report+'/publish')).ok()).toBeTruthy()
    const upload=who=>who.write(report+'/submit',undefined,'POST',{file:{name:'test.pdf',mimeType:'application/pdf',buffer:Buffer.from('%PDF-1.4 synthetic')}})
    expect((await upload(member)).status()).toBe(403)
    const attempts=await Promise.all([upload(leader),upload(leader)]);expect(attempts.map(x=>x.status()).sort()).toEqual([204,409])
    const rows=async who=>(await (await who.get(report+'/submissions')).json())
    const [s]=await rows(member),sub=report+'/submissions/'+s.id
    expect(s.members).toHaveLength(group.members.length);expect(await rows(other)).toHaveLength(0)
    expect((await other.get(sub+'/preview')).status()).toBe(404)
    expect((await t.write(sub+'/return?version='+s.version)).status()).toBe(204)
    expect((await upload(leader)).status()).toBe(204)
    expect((await rows(member))[0].history).toHaveLength(1)
    const review={ratings:[5,3,4,4],comment:'僅評分人員可見'}
    expect((await member.write(sub+'/reviews/'+subject,review,'PUT')).ok()).toBeTruthy()
    expect((await member.write(sub+'/reviews/'+subject,review,'PUT')).ok()).toBeTruthy()
    expect((await member.write(sub+'/vote',{choice:'review'},'PUT')).ok()).toBeTruthy()
    expect((await (await leader.get(sub+'/evaluation')).json()).reviews).toHaveLength(0)
    expect((await ta.get(sub+'/evaluation')).status()).toBe(403)
    const result=await (await t.get(sub+'/evaluation')).json()
    expect(result.reviews).toHaveLength(1);expect(result.votes[0].choice).toBe('review')
    async function grade(publish,reason){const [r]=await rows(t);return t.write(sub+'/grade',{score:80,feedback:'已閱讀',adjustments:[{studentId:me,score:87,reason}],publish,expectedVersion:r.version},'PUT')}
    expect((await grade(false,'')).status()).toBe(400)
    expect((await grade(false,'完成資料整理')).ok()).toBeTruthy()
    expect((await rows(member))[0].score).toBeNull();expect((await rows(member))[0].feedback).toBe('')
    async function myGrade(){
      const book=await (await member.get(base+'/gradebook')).json()
      const item=book.items.find(i=>i.sourceReportId===id)
      expect(book.scores.every(s=>s.studentId===me)).toBeTruthy()
      return book.scores.find(s=>s.itemId===item.id)?.score??null
    }
    async function historyGrade(){
      const history=await (await member.get('/learning-history?course='+course.id+'&kind=report')).json()
      return history.items.find(i=>i.id===id).score
    }
    expect(await myGrade()).toBeNull();expect(await historyGrade()).toBeNull()
    const page=await t.c.newPage();await page.goto(base+'/reports/'+id)
    await expect(page.getByRole('heading',{name:'需要教師確認'})).toBeVisible()
    expect((await grade(true,'完成資料整理')).ok()).toBeTruthy()
    expect((await rows(member))[0].score).toBe(87);expect((await rows(leader))[0].score).toBe(80)
    expect(await myGrade()).toBe(87);expect(await historyGrade()).toBe(87)
    const delegated=await t.write(base+'/reports',{...input,title:'代交驗收',groupIds:[group.id],allowDelegate:true,evaluationMode:'simple'})
    const delegatedId=(await delegated.json()).id,delegatedPath='/reports/'+delegatedId
    expect((await t.write(delegatedPath+'/publish')).ok()).toBeTruthy()
    expect((await other.get(delegatedPath)).status()).toBe(404)
    const otherBook=await (await other.get(base+'/gradebook')).json()
    expect(otherBook.items.some(i=>i.sourceReportId===delegatedId)).toBeFalsy()
    const delegatePage=await member.c.newPage()
    await delegatePage.goto(base+delegatedPath)
    await delegatePage.locator('input[type=file]').setInputFiles({name:'team.pdf',mimeType:'application/pdf',buffer:Buffer.from('%PDF-1.4 synthetic')})
    delegatePage.once('dialog',dialog=>dialog.accept())
    await delegatePage.getByRole('button',{name:'繳交 PDF 報告',exact:true}).click()
    await expect(delegatePage.getByRole('status').filter({hasText:'PDF 報告已繳交'})).toBeVisible()
    const [delegatedSubmission]=await (await leader.get(delegatedPath+'/submissions')).json()
    expect(delegatedSubmission.submittedBy).toBe(group.members.find(m=>m.id===me).displayName)
    expect((await member.write(delegatedPath+'/submissions/'+delegatedSubmission.id+'/vote',{choice:'agree'},'PUT')).status()).toBe(403)
    expect((await t.write(base+'/groups/'+group.id,undefined,'DELETE')).ok()).toBeTruthy()
    expect((await rows(member))[0].members).toHaveLength(group.members.length)
    const studentPage=await member.c.newPage();await studentPage.goto(base+'/reports/'+id)
    await expect(studentPage.getByText(/得分：87/)).toBeVisible()
  }finally{for(const c of contexts)await c.close()}
})
