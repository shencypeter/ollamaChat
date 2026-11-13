import {test,expect} from '@playwright/test'

test('paper exam saves a versioned class draft and publishes everyone together',async({browser,baseURL})=>{
 const context=await browser.newContext({baseURL,ignoreHTTPSErrors:true}),page=await context.newPage(),errors=[],writes=[]
 const course={id:'paper-course',title:'英文溝通',code:'ENG',termCode:'115-1',status:'active',courseRole:'teacher',canPractice:true,taRestrictions:[]}
 const item={id:'paper-midterm',title:'期中筆試',category:'midterm',maxScore:100,version:1,scheduledAt:'2026-11-20T01:00:00Z'}
 const book={staff:true,items:[item],students:[{id:'a',studentNumber:'001',displayName:'林志明'},{id:'b',studentNumber:'002',displayName:'陳怡君'}],scores:[]}
 page.on('pageerror',e=>errors.push(e.message));page.on('dialog',dialog=>dialog.accept())
 await page.route('**/api/**',async route=>{
  const request=route.request(),path=new URL(request.url()).pathname.replace('/api','');let body
  if(path==='/auth/session')body={user:{id:'teacher',displayName:'王老師',role:'Member',canTeach:true}}
  else if(path==='/auth/csrf')body={token:'synthetic'}
  else if(path==='/notifications')body={total:0,courses:[]}
  else if(path===`/courses/${course.id}`)body=course
  else if(path.endsWith('/assessments'))body={staff:true,quizzes:[],reports:[]}
  else if(path.endsWith('/exams'))body=[]
  else if(path.endsWith('/gradebook'))body=book
  else if(path.endsWith('/items/paper-midterm/scores')){
   const input=request.postDataJSON();writes.push({path,input});book.scores=input.scores.map(score=>({...score,itemId:item.id,version:1,publishedScore:null}));return route.fulfill({status:204})
  }else if(path.endsWith('/items/paper-midterm/publish-scores')){
   const input=request.postDataJSON();writes.push({path,input});book.scores=book.scores.map(score=>({...score,version:2,publishedScore:score.score}));return route.fulfill({status:204})
  }else return route.fulfill({status:404,contentType:'application/json',body:JSON.stringify({error:`Missing paper fixture ${path}`})})
  return route.fulfill({contentType:'application/json',body:JSON.stringify(body)})
 })
 await page.goto(`/courses/${course.id}?section=assessments&exam=${item.id}`)
 await expect(page.getByRole('heading',{name:'紙本期中／期末考',exact:true})).toBeVisible()
 await expect(page.locator('[data-action="publish"]')).toBeDisabled()
 await page.getByLabel('林志明 得分',{exact:true}).fill('0');await page.getByLabel('陳怡君 得分',{exact:true}).fill('88')
 await page.locator('[data-action="save"]').click();await expect(page.getByRole('status')).toContainText('草稿已儲存')
 expect(writes[0].input.scores).toEqual([{studentId:'a',score:0,expectedVersion:0},{studentId:'b',score:88,expectedVersion:0}])
 await page.locator('[data-action="publish"]').click();await expect(page.getByRole('status')).toContainText('全班考試成績已一次發布')
 expect(writes).toHaveLength(2);expect(writes[1].input).toEqual({expectedItemVersion:1,scores:[{studentId:'a',expectedVersion:1},{studentId:'b',expectedVersion:1}]})
 await expect(page.getByRole('alert')).toHaveCount(0)
 await page.screenshot({path:'test-results/offline-exam-published-desktop.png',fullPage:true})
 await page.setViewportSize({width:390,height:844});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true)
 await page.screenshot({path:'test-results/offline-exam-published-mobile.png',fullPage:true});expect(errors).toEqual([]);await context.close()
})
