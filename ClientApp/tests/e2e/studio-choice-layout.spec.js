import {test,expect} from '@playwright/test'

const course={id:'choice-course',title:'英文教學與合作學習',code:'ENG',termCode:'115-1',courseRole:'teacher',status:'active',isOwner:true,canPractice:true,taRestrictions:[],studentCount:3,resourceCount:1,quizCount:1,reportCount:1}
const longName='第一組：英文文法、日常對話與跨文化溝通的資料整理及合作練習'
async function fixtures(page){
 await page.route('**/api/**',async route=>{
  const path=new URL(route.request().url()).pathname.replace('/api','');let body
  if(path==='/auth/session')body={user:{id:'choice-user',displayName:'測試教師',role:'Administrator',canTeach:true,hasStudentCourses:true}}
  else if(path==='/notifications')body={total:0,courses:[]}
  else if(path===`/courses/${course.id}`)body=course
  else if(path==='/courses/student-choice')body={...course,id:'student-choice',courseRole:'student',isOwner:false,canPractice:false}
  else if(path.endsWith('/units/u1'))body={id:'u1',name:'英文對話單元',sortOrder:1,canManage:true,activities:[]}
  else if(path.endsWith('/curriculum'))body={units:[{id:'u1',name:'英文對話單元'}],activities:[]}
  else if(path.endsWith('/resources'))body=[{id:'resource',title:longName,fileName:'reading.mp4',learningType:'video/mp4',resourceType:'file',unitId:'u1',size:100}]
  else if(path.endsWith('/groups'))body=[{id:'g1',name:longName},{id:'g2',name:'第二組'}]
  else if(path.endsWith('/members'))body=[{id:'teacher',displayName:'教師',courseRole:'teacher',taRestrictions:[]},{id:'ta',displayName:'助教',courseRole:'ta',taRestrictions:[]}]
  else if(path.endsWith('/eligible-accounts'))body=[]
  else if(path==='/accounts')body=[{id:'person',displayName:'測試成員',studentNumber:'115001',email:'person@example.invalid',role:'Member',canCreateCourses:false}]
  else if(path==='/reports/r')body={id:'r',courseId:'student-choice',title:'合作報告',status:'published',submissionMode:'group',evaluationMode:'consensus',dimensions:['合作'],groupIds:[],courseRole:'student',maxScore:100,version:1}
  else if(path==='/reports/r/roster')body=[{name:'第一組',submitted:true,canSubmit:false}]
  else if(path==='/reports/r/submissions')body=[{id:'s',studentName:'第一組',fileName:'作業報告.pdf',revision:1,version:1,feedback:'',score:null,members:[{id:'choice-user',name:'本人',self:true,isLeader:true},{id:'peer',name:'組員',self:false}],history:[]}]
  else if(path.endsWith('/evaluation'))body={reviews:[],votes:[]}
  else return route.fulfill({status:404,contentType:'application/json',body:JSON.stringify({error:`Missing choice fixture ${path}`})})
  return route.fulfill({contentType:'application/json',body:JSON.stringify(body)})
 })
}
async function aligned(input){
 await expect(input).toBeVisible()
 const layout=await input.evaluate(node=>{
  const box=node.getBoundingClientRect(),label=node.labels?.[0],walker=document.createTreeWalker(label,NodeFilter.SHOW_TEXT)
  let text;while((text=walker.nextNode())&&!text.textContent.trim()){}
  const range=document.createRange(),start=text.textContent.search(/\S/);range.setStart(text,start);range.setEnd(text,start+1)
  const first=range.getBoundingClientRect();return {width:box.width,height:box.height,space:first.left-box.right,alignment:Math.abs((box.top+box.height/2)-(first.top+first.height/2))}
 })
 expect(layout.width).toBeCloseTo(18,0);expect(layout.height).toBeCloseTo(18,0);expect(layout.space).toBeGreaterThanOrEqual(7);expect(layout.alignment).toBeLessThan(5)
}
for(const width of [1440,390])test(`checkbox and radio labels stay aligned at ${width}px`,async({browser,baseURL})=>{
 const context=await browser.newContext({baseURL,ignoreHTTPSErrors:true,viewport:{width,height:1050}}),page=await context.newPage(),errors=[]
 page.setDefaultTimeout(10000);page.on('pageerror',e=>errors.push(e.message));page.on('dialog',d=>d.accept());await fixtures(page)
 await page.goto(`/courses/${course.id}/units/u1`);await page.getByRole('button',{name:'新增測驗／作業',exact:true}).click();await page.getByRole('button',{name:'建立檔案繳交作業',exact:true}).click()
 const late=page.getByRole('checkbox',{name:'允許截止後補交',exact:true});await aligned(late)
 await late.locator('..').getByText('允許截止後補交',{exact:true}).click();await expect(late).toBeChecked()
 await late.focus();await page.keyboard.press('Space');await expect(late).not.toBeChecked();expect(await late.evaluate(node=>getComputedStyle(node).outlineStyle)).toBe('solid')
 await page.getByLabel('繳交方式').selectOption('group');await aligned(page.getByRole('checkbox',{name:longName,exact:true}));await aligned(page.getByRole('checkbox',{name:'允許組員代組長繳交',exact:true}))
 await page.screenshot({path:`test-results/choice-pdf-${width}.png`,fullPage:true})
 await page.getByRole('button',{name:'建立線上／課後測驗',exact:true}).click();await aligned(page.getByRole('checkbox',{name:'發布時通知學生',exact:true}));await aligned(page.getByRole('checkbox',{name:longName,exact:true}))
 await page.getByLabel('題型',{exact:true}).selectOption('multiple');await page.getByLabel('選項（每行一個）',{exact:true}).fill('Could you help me revise this long conversation and explain the grammar clearly?\nCan you help?')
 await aligned(page.getByRole('checkbox',{name:'Could you help me revise this long conversation and explain the grammar clearly?',exact:true}))
 await page.goto(`/courses/${course.id}?section=students`);await page.getByText('本課助教權限',{exact:true}).click();for(const control of await page.locator('input[type="checkbox"]').all())await aligned(control)
 await page.goto('/admin/accounts');await aligned(page.getByRole('checkbox',{name:'測試成員 可建立課程',exact:true}));await page.getByRole('button',{name:'建立帳號',exact:true}).click();await aligned(page.locator('.account-create-form input[type="checkbox"]'))
 await page.screenshot({path:`test-results/choice-accounts-${width}.png`,fullPage:true})
 await page.goto('/courses/student-choice/reports/r');const vote=page.getByRole('radio',{name:'希望教師依個人貢獻評估',exact:true});await aligned(vote)
 await vote.locator('..').getByText('希望教師依個人貢獻評估',{exact:true}).click();await expect(vote).toBeChecked();await vote.focus();await page.keyboard.press('ArrowUp');await expect(page.getByRole('radio',{name:'同意全組採相同分數',exact:true})).toBeChecked()
 await page.evaluate(()=>window.scrollTo(0,0));await page.screenshot({path:`test-results/choice-votes-${width}.png`,fullPage:true})
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);expect(errors).toEqual([]);await context.close()
})
