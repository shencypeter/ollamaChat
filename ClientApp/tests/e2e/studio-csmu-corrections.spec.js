import {test,expect} from '@playwright/test'
const student={id:'student',displayName:'林同學',role:'Member',canTeach:false,hasStudentCourses:true}
const courses=[['current','115-1'],['history','114-S']].map(([id,termCode])=>({id,termCode,title:id==='current'?'英文文法':'歷史課程',code:id,courseRole:'student',status:'active',studentCount:3,resourceCount:1,quizCount:1,reportCount:1}))

test('shared static attachment returns after login with safe filename, download and no timer',async({page})=>{
 let signedIn=false;const requests=[]
 await page.route('**/api/**',async route=>{
  const path=new URL(route.request().url()).pathname.replace('/api','');requests.push(path)
  let body={}
  if(path==='/auth/session')body={user:signedIn?student:null}
  else if(path==='/auth/csrf')body={token:'fixture'}
  else if(path==='/auth/login'){signedIn=true;body=student}
  else if(path==='/notifications')body={total:0,courses:[]}
  else if(path==='/courses/current')body=courses[0]
  else if(path.endsWith('/learning'))return route.fulfill({status:415,json:{error:'Static material'}})
  else if(path.endsWith('/resources'))body=[{id:'file',title:'文法講義',fileName:'C:\\fakepath\\英文文法.pptx',unitId:'unit',previewType:null}]
  else if(path.endsWith('/interactions'))body={items:[],total:0,likes:2,writable:false}
  else if(path.endsWith('/summary'))body={summary:'本週文法重點'}
  await route.fulfill({json:body})
 })
 await page.goto('/courses/current/materials/file')
 await expect(page).toHaveURL(/\/login\?next=/)
 await expect(page.getByText('中山醫學大學個案分析 AI 助教系統',{exact:true})).toBeVisible()
 await page.getByLabel('電子郵件',{exact:true}).fill('student@csmu.edu.tw')
 await page.getByLabel('密碼',{exact:true}).fill('fixture-password')
 await page.getByRole('button',{name:'登入工作區'}).click()
 await expect(page).toHaveURL(/\/courses\/current\/materials\/file$/)
 await expect(page.getByRole('heading',{name:'文法講義',exact:true})).toBeVisible()
 await expect(page.getByText('原始檔名：英文文法.pptx',{exact:true})).toBeVisible()
 await expect(page.getByRole('button',{name:'下載原始檔案',exact:true})).toBeVisible()
 await expect(page.locator('object,video,.material-timer')).toHaveCount(0)
 await expect(page.getByText('fakepath',{exact:false})).toHaveCount(0)
 await page.getByRole('button',{name:'複製連結',exact:true}).click()
 expect(requests.some(path=>path.includes('/sessions'))).toBe(false)
 await expect(page).toHaveTitle(/中山醫學大學個案分析 AI 助教系統/)
})

test('student current-term course list hides management states and keeps historical terms accessible',async({page})=>{
 await page.route('**/api/**',async route=>{
  const path=new URL(route.request().url()).pathname.replace('/api','')
  const body=path==='/auth/session'?{user:student}:path==='/academic-calendar'?{currentTerm:'115-1',terms:['115-1','114-S']}:path==='/courses'?courses:{total:0,courses:[]}
  await route.fulfill({json:body})
 })
 await page.goto('/student/courses')
 await expect(page.getByRole('heading',{name:'英文文法',exact:true})).toBeVisible()
 await expect(page.getByRole('heading',{name:'歷史課程',exact:true})).toHaveCount(0)
 await expect(page.locator('.course-card .status-badge,.course-card progress')).toHaveCount(0)
 await expect(page.getByRole('button',{name:'公開課程',exact:true})).toHaveCount(0)
 await page.getByLabel('學年度',{exact:true}).selectOption('114')
 await page.getByLabel('學期',{exact:true}).selectOption('S')
 await expect(page.getByRole('heading',{name:'歷史課程',exact:true})).toBeVisible()
 await expect(page.getByRole('heading',{name:'英文文法',exact:true})).toHaveCount(0)
 await page.setViewportSize({width:390,height:844})
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true)
})
