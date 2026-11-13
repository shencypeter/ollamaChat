import {test,expect} from '@playwright/test'

// Production SPA with synthetic authorized responses; never writes to the running database.
test('group PDF review selects one real submission and keeps missing groups read only',async({browser,baseURL})=>{
 const context=await browser.newContext({baseURL,ignoreHTTPSErrors:true})
 const page=await context.newPage(),errors=[],writes=[],evaluations=[]
 page.on('pageerror',error=>errors.push(error.message))
 const course={id:'review-course',title:'小組觀察課程',code:'GROUP-UX',termCode:'115-1',status:'active',courseRole:'teacher',canPractice:true,isOwner:true,taRestrictions:[]}
 const report={id:'report-review',courseId:course.id,unitId:'review-unit',title:'小組 PDF 觀察報告',instructions:'每組整理一份觀察報告，以資料支持判斷。',submissionMode:'group',evaluationMode:'consensus',dimensions:['合作'],groupIds:['ga','gb','gc'],courseRole:'teacher',status:'published',maxScore:100,version:1,isPractice:false}
 const member=(id,name,score=null,reason='')=>({id,name,score,reason,isLeader:id.endsWith('1'),self:false,publishedScore:null})
 const submissions=[
  {id:'sa',studentName:'A 組',fileName:'A-observation.pdf',submittedBy:'合成甲',submittedAt:'2026-11-13T01:00:00Z',revision:1,version:1,returned:false,score:80,feedback:'以具體資料支持分析。',publishedScore:null,history:[],members:[member('a1','合成甲'),member('a2','合成乙')]},
  {id:'sb',studentName:'B 組',fileName:'B-observation.pdf',submittedBy:'合成丙',submittedAt:'2026-11-13T02:00:00Z',revision:1,version:1,returned:false,score:75,feedback:'可以補充資料限制。',publishedScore:null,history:[],members:[member('b1','合成丙'),member('b2','合成丁',90,'合成個別調整理由')]}
 ]
 await page.route('**/api/**',async route=>{
  const request=route.request(),path=new URL(request.url()).pathname.replace('/api','')
  let body
  if(path==='/auth/session')body={user:{id:'review-teacher',displayName:'合成教師',role:'Member',canTeach:true,canCreateCourses:true}}
  else if(path==='/auth/csrf')body={token:'synthetic-csrf'}
  else if(path==='/notifications')body={materials:0,assignments:0,questions:0,total:0,courses:[]}
  else if(path===`/courses/${course.id}`)body=course
  else if(path==='/reports/report-review')body=report
  else if(path==='/reports/report-review/roster')body=[{groupId:'gc',submissionId:null,name:'C 組',submitted:false},{groupId:'gb',submissionId:'sb',name:'B 組',submitted:true},{groupId:'ga',submissionId:'sa',name:'A 組',submitted:true}]
  else if(path==='/reports/report-review/submissions')body=submissions
  else if(path.endsWith('/evaluation')){evaluations.push(path);body={reviews:[],votes:[]}}
  else if(path.endsWith('/grade')&&request.method()==='PUT'){
   const input=request.postDataJSON();writes.push({path,input})
   const saved=submissions.find(s=>path.includes(`/submissions/${s.id}/`));Object.assign(saved,{score:input.score,feedback:input.feedback,version:saved.version+1})
   return route.fulfill({status:204})
  }
  else return route.fulfill({status:404,contentType:'application/json',body:JSON.stringify({error:`Missing group-review fixture ${path}`})})
  await route.fulfill({contentType:'application/json',body:JSON.stringify(body)})
 })
 await page.goto(`/courses/${course.id}/reports/report-review`)
 const sidebar=page.getByRole('complementary',{name:'指定小組繳交名單'}),paper=page.locator('.report-review-content')
 await expect(sidebar.locator('.report-group-select')).toHaveText(['A 組已繳交','B 組已繳交','C 組未繳交'])
 await expect(paper.getByRole('heading',{name:'A 組',exact:true})).toBeVisible()
 await expect(paper.getByLabel('A 組 小組基礎成績')).toHaveValue('80')
 await expect(paper.locator('.report-adjustments')).not.toHaveAttribute('open')
 await expect(paper.locator('.report-feedback-details')).not.toHaveAttribute('open')
 await expect(paper.locator('.report-member-grade').first()).not.toBeVisible()
 await expect(paper.getByText('成績分配：全組同分。',{exact:false})).toBeVisible()
 expect(evaluations).toEqual([])
 await expect(page.getByRole('button',{name:'全班發布成績',exact:true})).toBeDisabled()
 await expect(page.getByRole('button',{name:'發布成績',exact:true})).toHaveCount(0)
 await page.screenshot({path:'test-results/group-review-same-score-desktop.png',fullPage:true})

 await sidebar.getByRole('button',{name:/B 組.*已繳交/}).click()
 await expect(paper.getByRole('heading',{name:'B 組',exact:true})).toBeVisible()
 await expect(paper.locator('.report-adjustments')).toHaveAttribute('open','')
 await expect(paper.getByLabel('合成丁 個別成績')).toHaveValue('90')
 await paper.locator('.report-adjustments summary').click()
 await paper.getByLabel('B 組 小組基礎成績').fill('85')
 await paper.getByRole('button',{name:'儲存評分草稿',exact:true}).click()
 await expect(page.getByRole('status')).toContainText('評分草稿已儲存')
 expect(writes).toHaveLength(1);expect(writes[0].input).toMatchObject({score:85,publish:false,adjustments:[{studentId:'b1',score:null,reason:''},{studentId:'b2',score:90,reason:'合成個別調整理由'}]})
 await paper.locator('.report-feedback-details summary').click()
 await expect(paper.getByText('同分意願：尚未完成確認',{exact:true})).toBeVisible()
 expect(evaluations).toEqual(['/reports/report-review/submissions/sb/evaluation'])

 await sidebar.getByRole('button',{name:/C 組.*未繳交/}).click()
 await expect(paper.getByRole('heading',{name:'C 組',exact:true})).toBeVisible()
 await expect(paper.getByText('此小組尚未繳交 PDF。',{exact:false})).toBeVisible()
 await expect(paper.locator('form,input,textarea,.file-preview')).toHaveCount(0)
 await expect(paper.getByRole('button',{name:'下載／閱讀 PDF'})).toHaveCount(0)
 expect(writes).toHaveLength(1)
 await page.screenshot({path:'test-results/group-review-missing-desktop.png',fullPage:true})
 await page.setViewportSize({width:390,height:844})
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true)
 await page.screenshot({path:'test-results/group-review-missing-mobile.png',fullPage:true})
 expect(errors).toEqual([])
 await context.close()
})
