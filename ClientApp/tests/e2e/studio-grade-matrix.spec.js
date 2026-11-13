import {test,expect} from '@playwright/test'

test('teacher overview uses assessment rows and learner columns with a pinned assessment column',async({browser,baseURL})=>{
 const context=await browser.newContext({baseURL,ignoreHTTPSErrors:true}),page=await context.newPage(),errors=[]
 const course={id:'matrix-course',title:'英文溝通成績',code:'ENG',termCode:'115-1',status:'active',courseRole:'teacher',canPractice:true,taRestrictions:[],studentCount:8,resourceCount:2,quizCount:1,reportCount:1}
 const students=Array.from({length:8},(_,i)=>({id:`s${i}`,studentNumber:`11500${i+1}`,displayName:`同學 ${i+1}`}))
 const items=[{id:'a',title:'對話 PDF 報告',category:'assignment',sourceReportId:'r',maxScore:50},{id:'q',title:'文法測驗',category:'quiz',sourceAssignmentId:'quiz',maxScore:100},{id:'mid',title:'期中筆試',category:'midterm',maxScore:100}]
 page.on('pageerror',e=>errors.push(e.message))
 page.on('console',message=>{if(message.type()==='error'&&!message.text().includes('status of 404'))errors.push(message.text())})
 await page.route('**/api/**',async route=>{
  const path=new URL(route.request().url()).pathname.replace('/api','');let body
  if(path==='/auth/session')body={user:{id:'teacher',displayName:'測試教師',role:'Member',canTeach:true}}
  else if(path==='/notifications')body={total:0,courses:[]}
  else if(path===`/courses/${course.id}`)body=course
  else if(path.endsWith('/curriculum'))body={units:[{id:'u',name:'日常對話'}],activities:[{id:'r',kind:'report',unitId:'u'},{id:'quiz',kind:'quiz',unitId:'u'}]}
  else if(path.endsWith('/gradebook'))body={staff:true,items,students,scores:[{itemId:'a',studentId:'s0',score:0,publishedScore:0},{itemId:'a',studentId:'s1',score:25,publishedScore:20},{itemId:'q',studentId:'s2',score:80,publishedScore:null}],averages:[],itemAverages:[],finals:[{studentId:'s0',publishedTotal:0}],classAdjustment:0,classAverage:40,gradedStudentCount:3}
  else return route.fulfill({status:404,contentType:'application/json',body:JSON.stringify({error:`Missing matrix fixture ${path}`})})
  await route.fulfill({contentType:'application/json',body:JSON.stringify(body)})
 })
 await page.goto(`/courses/${course.id}?section=grades`)
 const table=page.getByTestId('grade-overview'),region=page.getByRole('region',{name:'全班成績對照表，可水平捲動'}),row=table.locator('tr[data-item-id="a"]')
 await expect.poll(async()=>errors.length?errors.join('\n'):await table.count()?'ready':'loading').toBe('ready')
 await expect(table.locator('thead th')).toHaveCount(9);await expect(table.locator('tbody tr')).toHaveCount(3)
 await expect(row.locator('td[data-student-id="s0"]')).toContainText('0.00 分');await expect(row.locator('td[data-student-id="s1"]')).toContainText('25.00 分')
 await expect(row.locator('td[data-student-id="s1"]')).toContainText('另有草稿');await expect(table.locator('tr[data-summary="published"] td[data-student-id="s0"]')).toHaveText('0.00 分')
 const before=await row.getByRole('rowheader').boundingBox();await region.evaluate(node=>node.scrollLeft=420)
 expect(await region.evaluate(node=>node.scrollLeft)).toBeGreaterThan(0);expect(Math.abs((await row.getByRole('rowheader').boundingBox()).x-before.x)).toBeLessThan(2)
 await page.screenshot({path:'test-results/grade-matrix-scrolled.png',fullPage:true});await region.evaluate(node=>node.scrollLeft=0)
 await page.screenshot({path:'test-results/grade-matrix-desktop.png',fullPage:true})
 await page.setViewportSize({width:390,height:844});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true)
 await region.evaluate(node=>{node.scrollLeft=0;node.scrollTop=0});await page.screenshot({path:'test-results/grade-matrix-mobile.png',fullPage:true})
 await page.getByLabel('成績單元',{exact:true}).selectOption('u');await expect(table.locator('tbody tr')).toHaveCount(2);await expect(table.locator('thead th')).toHaveCount(9)
 expect(errors).toEqual([]);await context.close()
})
