import {test,expect} from '@playwright/test'

// Render the production SPA against deterministic API fixtures. No database writes or login secrets.
const course={id:'ux-course',title:'資料分析與校園觀察',code:'UX115',termCode:'115-1',status:'active',studentCount:3,resourceCount:3,youTubeCount:2,quizCount:1,reportCount:1,taRestrictions:[]}
const curriculum={canManage:true,units:[{id:'u1',name:'觀察與證據',description:'從閱讀資料到分析作業',sortOrder:1,version:1}],activities:[{id:'q1',kind:'quiz',unitId:'u1',title:'觀察練習',status:'published'}]}
async function fixtures(page,staff){
 const current={...course,courseRole:staff?'teacher':'student',canPractice:staff,isOwner:staff}
 const item={id:'g1',title:'觀察練習',category:'quiz',sourceAssignmentId:'q1',maxScore:100,weight:1}
 await page.route('**/api/**',async route=>{
  const path=new URL(route.request().url()).pathname.replace('/api','')
  let body
  if(path==='/auth/session')body={user:{id:'ux-user',displayName:staff?'陳老師':'林同學',role:'Member',canTeach:staff,hasStudentCourses:!staff,canCreateCourses:staff}}
  else if(path==='/notifications')body={materials:0,assignments:1,questions:0,total:1,courses:[{courseId:course.id,assignments:1,total:1}]}
  else if(path==='/courses')body=[current]
  else if(path==='/portal-feed')body={items:[{id:'news',kind:'announcement',courseId:course.id,courseTitle:course.title,title:'期中考範圍提醒',authorName:'陳老師',occurredAt:'2026-11-13T02:00:00Z',url:`/courses/${course.id}?section=announcements&announcement=news`}],hasMore:false}
  else if(path==='/portal-feed/agenda')body={items:[{id:'paper',kind:'midterm',courseId:course.id,courseTitle:course.title,title:'期中紙本考試',dueAt:'2026-11-20T01:00:00Z',url:`/courses/${course.id}?section=assessments&exam=paper`}],hasMore:false}
  else if(path.endsWith('/announcements'))body={items:[{id:'news',title:'期中考範圍提醒',body:'請複習前三單元，考後統一公布成績。',status:'published',authorName:'陳老師',publishedAt:'2026-11-13T02:00:00Z',version:1}],canManage:staff}
  else if(path.endsWith('/statistics'))body={enrolled:3,submitters:2,submissions:4,materialLearners:2}
  else if(path===`/courses/${course.id}`)body=current
  else if(path.endsWith('/curriculum'))body=curriculum
  else if(path.endsWith('/units/u1'))body={...curriculum.units[0],canManage:staff,activities:curriculum.activities}
  else if(path==='/learning-history/courses')body=[{...current,term:'115-1',required:2,submitted:1,pending:0,average:0}]
  else if(path==='/learning-history')body={total:1,items:new URL(route.request().url()).searchParams.has('todo')?[{id:'todo',title:'單元 PDF 報告',courseId:course.id,state:'todo',url:`/courses/${course.id}/reports/report1`,dueAt:'2026-11-15T12:00:00Z'}]:[{id:'g1',kind:'quiz',title:'觀察練習',courseId:course.id,state:'graded',score:0,maxScore:100,url:`/courses/${course.id}/quizzes/q1`}]}
  else if(path.endsWith('/gradebook'))body={staff,items:[item],students:[{id:'ux-user',studentNumber:'115001',displayName:'林同學'}],scores:[{studentId:'ux-user',itemId:'g1',score:0,publishedScore:0}],averages:[{studentId:'ux-user',average:0}],itemAverages:[],finals:[],classAverage:0,gradedStudentCount:1,classAdjustment:0,canPublishFinal:staff}
  else if(path.endsWith('/questions'))body={items:[],total:0}
  else if(path==='/assignments/q1/submissions')body=[{id:'s1',studentName:'林同學',gradingStatus:'ungraded',submittedAt:'2026-11-13T04:00:00Z'}]
  else return route.fulfill({status:404,contentType:'application/json',body:JSON.stringify({error:`Missing UX fixture ${path}`})})
  await route.fulfill({contentType:'application/json',body:JSON.stringify(body)})
 })
}
for(const staff of [false,true])test(`${staff?'teacher':'student'} dashboard, portal menu and grade layout`,async({browser,baseURL})=>{
 const context=await browser.newContext({baseURL,ignoreHTTPSErrors:true});const page=await context.newPage();const errors=[];page.on('pageerror',error=>errors.push(error.message));await fixtures(page,staff)
 await page.goto('/');await expect(page.getByRole('heading',{name:staff?'我的授課':'我的修課',exact:true})).toBeVisible()
 await expect(page.getByRole('heading',{name:'跨課訊息',exact:true})).toBeVisible();await expect(page.getByText('期中紙本考試',{exact:true})).toBeVisible()
 const panels=page.locator('details.portal-panel'),inbox=panels.filter({has:page.getByRole('heading',{name:'跨課訊息',exact:true})})
 await expect(panels).toHaveCount(staff?6:5)
 await inbox.locator('summary').focus();await page.keyboard.press('Enter');await expect(inbox).toHaveJSProperty('open',false)
 await expect(inbox.locator('summary')).toContainText('本頁 1 則');await expect(inbox.getByText('期中考範圍提醒',{exact:true})).not.toBeVisible()
 await expect.poll(()=>page.evaluate(scope=>localStorage.getItem(`portal:panels:v1:ux-user:${scope}:inbox`),staff?'teaching':'student')).toBe('closed')
 await page.reload();await expect(page.getByRole('heading',{name:staff?'我的授課':'我的修課',exact:true})).toBeVisible();await expect(inbox).toHaveJSProperty('open',false)
 await inbox.locator('summary').focus();await page.keyboard.press('Space');await expect(inbox).toHaveJSProperty('open',true)
 for(const panel of await panels.all())await panel.locator('summary').click()
 await page.screenshot({path:`test-results/ux-${staff?'teacher':'student'}-panels-collapsed-desktop.png`,fullPage:true})
 await page.setViewportSize({width:390,height:844});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true)
 await page.screenshot({path:`test-results/ux-${staff?'teacher':'student'}-panels-collapsed-mobile.png`,fullPage:true})
 const workId=staff?'grading-queue':'learning-tasks';await page.locator(`a.summary-link[href="#${workId}"]`).click();await expect(page.locator(`#${workId}`)).toHaveJSProperty('open',true)
 for(const panel of await panels.all())if(!await panel.evaluate(node=>node.open))await panel.locator('summary').click()
 await page.setViewportSize({width:1440,height:1050})
 await page.getByRole('button',{name:/帳號選單/}).click();await expect(page.getByRole('link',{name:'我的首頁（Portal）'})).toBeVisible();await page.getByRole('link',{name:'我的首頁（Portal）'}).click()
 await page.screenshot({path:`test-results/ux-${staff?'teacher':'student'}-dashboard-desktop.png`,fullPage:true})
 await page.setViewportSize({width:390,height:844});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true)
 await page.screenshot({path:`test-results/ux-${staff?'teacher':'student'}-dashboard-mobile.png`,fullPage:true})
 await page.setViewportSize({width:1440,height:1050})
 if(staff){await page.goto('/courses');await expect(page.getByText('YouTube 影音教材・2 部',{exact:true})).toBeVisible()}
 await page.goto(`/courses/${course.id}?section=grades`)
 await expect(page.locator(staff?'[data-testid="grade-overview"]':'table')).toBeVisible()
 expect(await page.locator('.gradebook-panel').textContent()).not.toMatch(/權重|加權/)
 await expect(page.getByRole('alert')).toHaveCount(0)
 if(staff){await page.getByRole('button',{name:'成績明細',exact:true}).click();await expect(page.locator('[data-testid="grade-details"]')).toBeVisible()}
 await page.screenshot({path:`test-results/ux-${staff?'teacher':'student'}-grades-desktop.png`,fullPage:true})
 await page.setViewportSize({width:390,height:844});await expect(page.locator('table').first()).toBeVisible();expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true)
 await page.screenshot({path:`test-results/ux-${staff?'teacher':'student'}-grades-mobile.png`,fullPage:true})
 if(!staff){const score=page.locator('[data-testid="my-grades"] [data-label="得分"]');await expect(score).toHaveText('0');const box=await score.boundingBox();expect(box.x+box.width).toBeLessThanOrEqual(390)}
 await page.goto('/learning-history');await expect(page.getByRole('heading',{name:'我的學習歷程',exact:true})).toBeVisible()
 await page.setViewportSize({width:1440,height:1050});await page.goto(`/courses/${course.id}`);await expect(page.getByRole('heading',{name:staff?'本課學習概況':'我的課程進度',exact:true})).toBeVisible()
 await page.getByRole('link',{name:'課程公告',exact:true}).click();await expect(page.getByText('請複習前三單元，考後統一公布成績。',{exact:true})).toBeVisible();await expect(page.getByRole('alert')).toHaveCount(0)
 await page.screenshot({path:`test-results/ux-${staff?'teacher':'student'}-announcements.png`,fullPage:true})
 await page.goto(`/courses/${course.id}/units/u1`);await expect(page.getByRole('heading',{name:'觀察與證據',exact:true})).toBeVisible()
 expect(errors).toEqual([]);await context.close()
})
