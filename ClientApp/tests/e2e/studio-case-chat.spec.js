import {test,expect} from '@playwright/test'
import {readFileSync} from 'node:fs'
import {resolve,basename} from 'node:path'
import {fileURLToPath} from 'node:url'

// Production bundle + synthetic API only: no service startup, database, or model request.
const portal=resolve(fileURLToPath(new URL('../../../wwwroot/portal/',import.meta.url)))
const stages=[{key:'premorbid',label:'A 病前功能'},{key:'illnessCourse',label:'B 疾病病程'},{key:'functionalAssessment',label:'C 功能評估結果'},{key:'psychosocial',label:'D 心理社會條件'}]
const metrics={promptTokens:120,cachedPromptTokens:0,responseTokens:36,tokensPerSecond:18,roundTripMilliseconds:2450,totalDurationNanoseconds:2400000000,loadDurationNanoseconds:100000000,promptEvalDurationNanoseconds:300000000,evalDurationNanoseconds:2000000000,doneReason:'stop'}
const classifications=[{code:'A1',label:'人口學資料',phase:'A'},{code:'A1a',label:'年齡',phase:'A'},{code:'A1b',label:'性別',phase:'A'},{code:'A1e',label:'疾病史',phase:'A'},{code:'B6',label:'診斷',phase:'B'},{code:'C5',label:'日常生活活動',phase:'C'},{code:'D3',label:'家庭支持',phase:'D'}]
const annotatedHistory=()=>{
 const content='  55歲男性，病前ADL可獨立完成。\n目前ADL需協助😀；病程穩定，家屬支持。<b>原文</b>'
 const annotation=(id,text,codes,status='unverified')=>({annotationId:id,start:content.indexOf(text),end:content.indexOf(text)+text.length,text,codes,status,explanation:'合成教學描述：僅分類辨識，請核對臨床依據。'})
 return {activeSessionId:'annotated-session',sessions:[{id:'annotated-session',status:'active',messages:[
  {id:'annotated-u',role:'user',content,status:'completed'},
  {id:'annotated-a',role:'assistant',replyTo:'annotated-u',status:'completed',content:'合成回饋：分類辨識與臨床正確性分別檢視。',annotations:[annotation('demographics','55歲男性',['A1a','A1b']),annotation('overlap','男性，病前ADL',['C5'],'unsupported'),annotation('illness','病程穩定',['B6']),annotation('support','家屬支持',['D3'],'misplaced')]},
  {id:'revised-u',role:'user',content:'另一版：男性 男性',status:'completed',revisesMessageId:'annotated-u'},
  {id:'revised-a',role:'assistant',replyTo:'revised-u',status:'completed',content:'第二版合成回饋',annotations:[{annotationId:'second-male',start:7,end:9,text:'男性',codes:['A1b'],status:'unverified',explanation:'只標示第二次出現的詞。'}]}
 ]}]}
}
const roundHistory=()=>({activeSessionId:'stage-session',sessions:[{id:'stage-session',stage:'premorbid',status:'active',modelName:'synthetic-model',promptVersion:'synthetic-v1',messages:[
 {id:'u1',role:'user',content:'  第一份原始作答 \n',status:'completed',revisesMessageId:null},{id:'a1',role:'assistant',content:'第一輪合成回饋',status:'completed',replyTo:'u1',metrics:null},
 {id:'u2',role:'user',content:'第二份獨立作答',status:'completed',revisesMessageId:null},{id:'a2',role:'assistant',content:'第二輪合成回饋',status:'completed',replyTo:'u2',metrics:{...metrics,responseTokens:22}},
 {id:'u3',role:'user',content:'第三份修改作答',status:'completed',revisesMessageId:'u1'},{id:'a3',role:'assistant',content:'第三輪合成回饋',status:'completed',replyTo:'u3',modelName:'synthetic-model',promptVersion:'synthetic-v1',metrics}
 ]}]})
async function fixture(page,staff=false,rejectRawCleanup=false,initialHistory=null,initialStage='premorbid'){
 const course={id:'chat-course',title:'合成個案分析課程',code:'CHAT115',termCode:'115-1',status:'active',courseRole:staff?'teacher':'student',isOwner:staff,canPractice:staff,caseAnalysisEnabled:true,taRestrictions:[],studentCount:1,resourceCount:0,quizCount:0,reportCount:0}
 let nextId=0,raw=new Map(),histories=new Map(initialHistory?[[initialStage,initialHistory]]:[]);const requests=[]
 await page.route('**/*',async route=>{
  const req=route.request(),path=new URL(req.url()).pathname
  if(path.startsWith('/portal/assets/'))return route.fulfill({body:readFileSync(resolve(portal,'assets',basename(path))),contentType:path.endsWith('.js')?'text/javascript':'text/css'})
  if(!path.startsWith('/api/'))return route.fulfill({body:readFileSync(resolve(portal,'index.html')),contentType:'text/html'})
  const p=path.slice(4),method=req.method(),body=req.postDataJSON();requests.push({path:p,method,body})
  let result
  if(p==='/auth/session')result={user:{id:'chat-user',displayName:staff?'合成老師':'合成同學',role:'Member',canTeach:staff,hasStudentCourses:!staff,canCreateCourses:staff}}
  else if(p==='/auth/csrf')result={token:'synthetic-token'}
  else if(p==='/courses')result=[course]
  else if(p==='/academic-calendar')result={currentTerm:'115-1',terms:['115-1'],semesters:[]}
  else if(p==='/notifications')result={materials:0,assignments:0,questions:0,total:0,courses:[]}
  else if(p==='/courses/chat-course')result=course
  else if(p==='/courses/chat-course/case-analysis')result={enabled:true,canChat:true,canReview:staff,isPractice:staff,modelName:'synthetic-model',maximumMessageCharacters:8000,stages,classifications}
  else if(p.endsWith('/case-analysis/settings')){course.caseAnalysisEnabled=body.enabled;result=null}
  else if(p.endsWith('/raw/sessions')){if(method==='DELETE'){raw.clear();result=null}else{if(raw.size>=3)return route.fulfill({status:429,json:{error:'Raw capacity reached'}});const id='raw-'+(++nextId);raw.set(id,[]);result={id}}}
  else if(p.includes('/raw/sessions/')){
   const id=p.split('/')[6]
   if(method==='DELETE'){if(rejectRawCleanup)return route.fulfill({status:503,json:{error:'Simulated unload cleanup loss'}});raw.delete(id);result=null}
   else{const messages=raw.get(id);messages.push({role:'user',content:body.content},{role:'assistant',content:'合成回覆：'+body.content,metrics});result={sessionId:id,messages,modelName:'synthetic-model'}}
  }
  else if(p.includes('/case-analysis/stages/')){
   const stage=p.split('/')[5];let history=histories.get(stage)||{sessions:[],activeSessionId:null}
   if(method==='POST'){
    if(!history.activeSessionId)history={activeSessionId:'stage-session',sessions:[{id:'stage-session',stage,status:'active',messages:[]}]}
    const messages=history.sessions.find(s=>s.id===history.activeSessionId).messages,id='u'+(messages.filter(m=>m.role==='user').length+1)
    if(!messages.some(m=>m.requestId===body.requestId))messages.push({id,role:'user',content:body.content,status:'completed',revisesMessageId:body.revisesMessageId||null,requestId:body.requestId},{id:'a'+id,role:'assistant',content:'已保存的階段回覆',status:'completed',replyTo:id,metrics})
    histories.set(stage,history)
   }
   result=history
  }
  else if(p.endsWith('/case-analysis/records'))result={students:[{id:'s1',displayName:'合成學員',studentNumber:'TEST01',statistics:{studentMessages:1,assistantReplies:1,roundTrips:1},stages:[{stage:'premorbid',statistics:{studentMessages:1,assistantReplies:1,roundTrips:1}}]}]}
  else if(/\/case-analysis\/records\/s1\/[^/]+$/.test(p))result=initialHistory||{activeSessionId:'formal',sessions:[{id:'formal',status:'active',messages:[{id:'formal-message',role:'user',content:'合成正式學生對話'}]}]}
  else return route.fulfill({status:404,json:{error:'Missing case chat fixture '+p}})
  await route.fulfill({json:result})
 })
 return {requests,getRawCount:()=>nextId,getHistory:stage=>histories.get(stage)}
}
for(const width of [1440,390])test(`case chat raw isolation and persisted stage at ${width}px`,async({page})=>{
 await page.setViewportSize({width,height:900});const state=await fixture(page),errors=[];page.on('pageerror',e=>errors.push(e.message))
 await page.goto('/courses/chat-course?section=case-analysis');await expect(page.getByRole('heading',{name:'個案分析 AI 助教',exact:true})).toBeVisible()
 const log=page.getByRole('log',{name:'對話內容'})
 for(const text of ['第一輪','第二輪','<b>第三輪純文字</b>']){await page.getByLabel('訊息',{exact:true}).fill(text);await page.getByRole('button',{name:'傳送',exact:true}).click();await expect(log).toContainText('合成回覆：'+text)}
 expect(await log.locator('b').count()).toBe(0)
 const sends=state.requests.filter(r=>r.path.endsWith('/raw-1/messages'));expect(sends.map(r=>r.body)).toEqual([{content:'第一輪'},{content:'第二輪'},{content:'<b>第三輪純文字</b>'}])
 page.once('dialog',dialog=>dialog.dismiss());await page.getByRole('link',{name:'← 返回我的課程',exact:true}).click();await expect(page).toHaveURL(/section=case-analysis/);await expect(log).toContainText('第一輪')
 page.once('dialog',dialog=>dialog.dismiss());await page.getByRole('button',{name:'A 病前功能',exact:true}).click();await expect(log).toContainText('第一輪')
 page.once('dialog',dialog=>dialog.accept());await page.getByRole('button',{name:'A 病前功能',exact:true}).click();await expect(log).not.toContainText('第一輪')
 await page.getByLabel('訊息',{exact:true}).fill('階段合成訊息');await page.getByRole('button',{name:'提交並取得 AI 助教回饋',exact:true}).click();await expect(log).toContainText('已保存的階段回覆')
 await page.reload();await expect(log).not.toContainText('階段合成訊息');expect(state.getRawCount()).toBe(2)
 await page.getByRole('button',{name:'A 病前功能',exact:true}).click();await expect(log).toContainText('已保存的階段回覆')
 expect(state.requests.some(r=>r.path.includes('/records'))).toBe(false)
 await expect(page.locator('[data-mode="premorbid"]')).toHaveCSS('background-color','rgb(91, 95, 199)')
 await page.screenshot({path:`test-results/case-chat-${width}.png`,fullPage:true});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);expect(errors).toEqual([])
})
test('teacher reads formal records and uses the owner course toggle',async({page})=>{
 const state=await fixture(page,true);await page.goto('/courses/chat-course?section=case-analysis')
 await expect(page.getByText(/教師／助教教學練習/)).toBeVisible();await page.getByRole('button',{name:'學生紀錄與統計',exact:true}).click()
 await expect(page.getByText(/全課程合計/)).toBeVisible();await page.getByRole('button',{name:'TEST01 合成學員',exact:true}).click();await expect(page.getByRole('log')).toContainText('合成正式學生對話');await expect(page.locator('textarea')).toHaveCount(0)
 await page.getByRole('link',{name:'課程設定',exact:true}).click();await page.getByLabel('啟用個案分析 AI 助教',{exact:true}).uncheck();await expect(page.getByLabel('啟用個案分析 AI 助教',{exact:true})).not.toBeChecked()
 expect(state.requests).toContainEqual({path:'/courses/chat-course/case-analysis/settings',method:'PUT',body:{enabled:false}})
})
test('raw allocation recovers explicitly when unload cleanup is lost across hard refreshes',async({page})=>{
 const state=await fixture(page,false,true);await page.goto('/courses/chat-course?section=case-analysis');await expect(page.getByRole('log')).toBeVisible()
 for(let i=0;i<5;i++){
  await page.reload()
  if(i===2){
   await expect(page.getByRole('button',{name:'清除本帳號的暫存聊天並重新開始',exact:true})).toBeVisible()
   page.once('dialog',async dialog=>{expect(dialog.message()).toBe('將清除本帳號所有課程與分頁的純 AI 聊天暫存，無法恢復，確定？');await dialog.accept()})
   await page.getByRole('button',{name:'清除本帳號的暫存聊天並重新開始',exact:true}).click()
  }
  await expect(page.getByRole('log')).toBeVisible();await expect(page.getByRole('alert')).toHaveCount(0)
  await expect.poll(()=>state.getRawCount()).toBe(i+2)
 }
 expect(state.requests.filter(r=>r.method==='DELETE'&&r.path==='/courses/chat-course/case-analysis/raw/sessions')).toHaveLength(1)
})
test('BFcache lifecycle events allocate a new raw context without restoring prior content',async({page})=>{
 const state=await fixture(page);await page.goto('/courses/chat-course?section=case-analysis')
 await page.getByLabel('訊息',{exact:true}).fill('不可從快取恢復的合成訊息');await page.getByRole('button',{name:'傳送',exact:true}).click();await expect(page.getByRole('log')).toContainText('合成回覆')
 await page.evaluate(()=>window.dispatchEvent(new PageTransitionEvent('pagehide',{persisted:true})))
 await expect(page.locator('.chat-message')).toHaveCount(0)
 await page.evaluate(()=>window.dispatchEvent(new PageTransitionEvent('pageshow',{persisted:true})))
 await expect.poll(()=>state.getRawCount()).toBe(2);await expect(page.locator('.chat-message')).toHaveCount(0)
 await page.getByLabel('訊息',{exact:true}).fill('新的合成訊息');await page.getByRole('button',{name:'傳送',exact:true}).click();await expect(page.getByRole('log')).toContainText('新的合成訊息')
 expect(state.requests).toContainEqual({path:'/courses/chat-course/case-analysis/raw/sessions/raw-2/messages',method:'POST',body:{content:'新的合成訊息'}})
})

for(const width of [1440,390])test(`feedback round selection and immutable revision at ${width}px`,async({page})=>{
 await page.setViewportSize({width,height:1000});const state=await fixture(page,false,false,roundHistory());await page.goto('/courses/chat-course?section=case-analysis');await page.getByRole('button',{name:'A 病前功能',exact:true}).click()
 const log=page.getByRole('log',{name:'對話內容'}),sidebar=page.getByRole('complementary',{name:'回饋輪次'})
 await expect(sidebar.locator('[data-round]')).toHaveCount(3);await expect(log).toContainText('第三輪合成回饋');await expect(log).not.toContainText('第一輪合成回饋')
 await page.getByRole('button',{name:'查看回覆效能',exact:true}).click();const performance=page.getByRole('dialog',{name:'第 3 輪回饋效能',exact:true});await expect(performance).toBeVisible();await expect(performance.locator('dl')).toContainText('120');await expect(performance.locator('dl')).toContainText('36');expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await page.screenshot({path:`test-results/feedback-performance-${width}.png`,fullPage:true});await page.keyboard.press('Escape');await expect(performance).not.toBeVisible()
 await page.getByRole('button',{name:'比較作答版本',exact:true}).click();const comparison=page.getByRole('dialog',{name:'比較作答版本',exact:true});await expect(comparison).toBeVisible();await comparison.getByLabel('較早版本',{exact:true}).selectOption('u1');await comparison.getByLabel('較新版本',{exact:true}).selectOption('u2');expect(await comparison.locator('[data-diff-output]').evaluate(el=>[...el.childNodes].filter(n=>n.nodeName!=='DEL').map(n=>n.textContent).join(''))).toBe('第二份獨立作答');await expect(comparison.locator('[data-diff-output]')).not.toContainText('第三份修改作答');expect(await comparison.getByLabel('較早版本',{exact:true}).locator('option').count()).toBe(3);expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await page.screenshot({path:`test-results/feedback-diff-${width}.png`,fullPage:true});await comparison.getByRole('button',{name:'關閉作答版本比較',exact:true}).click()
 await page.getByLabel('訊息',{exact:true}).fill('尚未提交草稿');await sidebar.locator('[data-round="u1"]').click();await expect(log).toContainText('第一輪合成回饋');await expect(log).not.toContainText('第三輪合成回饋');await expect(page.getByLabel('訊息',{exact:true})).toHaveValue('尚未提交草稿')
 await page.getByRole('button',{name:'查看回覆效能',exact:true}).click();await expect(page.getByRole('dialog',{name:'第 1 輪回饋效能',exact:true})).toContainText('此回覆未提供效能資料');await page.getByRole('button',{name:'關閉效能資訊',exact:true}).click()
 page.once('dialog',dialog=>dialog.accept());await page.getByRole('button',{name:'修改這次作答',exact:true}).click();await expect(page.getByLabel('訊息',{exact:true})).toHaveValue('  第一份原始作答 \n')
 await page.getByLabel('訊息',{exact:true}).fill('  從第一輪修改 \n');await page.getByRole('button',{name:'提交並取得 AI 助教回饋',exact:true}).click();await expect(log).toContainText('已保存的階段回覆');await expect(sidebar.locator('[data-round]')).toHaveCount(4)
 const sent=state.requests.filter(r=>r.path.endsWith('/premorbid/messages')&&r.method==='POST');expect(sent).toHaveLength(1);expect(sent[0].body).toEqual({requestId:expect.any(String),content:'  從第一輪修改 \n',revisesMessageId:'u1'});expect(state.getHistory('premorbid').sessions[0].messages[0].content).toBe('  第一份原始作答 \n')
 await expect(page.locator('[data-mode="premorbid"]')).toHaveAttribute('aria-pressed','true');await expect(page.getByRole('button',{name:'下一階段：B 疾病病程',exact:true})).toBeVisible()
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await page.screenshot({path:`test-results/feedback-rounds-${width}.png`,fullPage:true})
 await page.getByRole('button',{name:'下一階段：B 疾病病程',exact:true}).click();await expect(page.locator('[data-mode="illnessCourse"]')).toHaveAttribute('aria-pressed','true');await expect(log).not.toContainText('從第一輪修改');expect(state.requests.some(r=>/grade|unlock|pass/.test(r.path))).toBe(false)
})

test('keyboard navigation and session changes preserve unsent revisions when confirmation is canceled',async({page})=>{
 const seeded=roundHistory();seeded.sessions.push({id:'closed-session',status:'closed',messages:[{id:'old-user',role:'user',content:'歷史作答',status:'completed'}]});await fixture(page,false,false,seeded);await page.goto('/courses/chat-course?section=case-analysis');await page.getByRole('button',{name:'A 病前功能',exact:true}).click()
 const draft=page.getByLabel('訊息',{exact:true}),revise=page.getByRole('button',{name:'修改這次作答',exact:true}),selector=page.locator('.history-select select')
 await page.locator('[data-round="u1"]').click();await revise.click();await draft.fill('  尚未提交的修改 \n')
 page.once('dialog',dialog=>dialog.dismiss());await revise.focus();await page.keyboard.press('Enter');await expect(draft).toHaveValue('  尚未提交的修改 \n');await expect(page.locator('.revision-notice')).toContainText('第 1 輪')
 page.once('dialog',dialog=>dialog.dismiss());await selector.selectOption('closed-session');await expect(selector).toHaveValue('stage-session');await expect(draft).toHaveValue('  尚未提交的修改 \n')
 page.once('dialog',dialog=>dialog.dismiss());await page.getByRole('button',{name:'下一階段：B 疾病病程',exact:true}).focus();await page.keyboard.press('Enter');await expect(page.locator('[data-mode="premorbid"]')).toHaveAttribute('aria-pressed','true')
 page.once('dialog',dialog=>dialog.dismiss());await page.getByRole('link',{name:'← 返回我的課程',exact:true}).focus();await page.keyboard.press('Enter');await expect(page).toHaveURL(/section=case-analysis/);await expect(draft).toHaveValue('  尚未提交的修改 \n')
 page.once('dialog',dialog=>dialog.accept());await selector.selectOption('closed-session');await expect(selector).toHaveValue('closed-session');await expect(page.locator('textarea')).toHaveCount(0);await selector.selectOption('stage-session');await expect(draft).toHaveValue('');await expect(page.locator('.revision-notice')).toHaveCount(0)
 await page.locator('[data-round="u1"]').click();await revise.click();await draft.fill('將由確認覆寫');page.once('dialog',dialog=>dialog.accept());await revise.focus();await page.keyboard.press('Enter');await expect(draft).toHaveValue('  第一份原始作答 \n')
 page.once('dialog',dialog=>dialog.accept());await page.getByRole('button',{name:'下一階段：B 疾病病程',exact:true}).focus();await page.keyboard.press('Enter');await expect(page.locator('[data-mode="illnessCourse"]')).toHaveAttribute('aria-pressed','true');await expect(draft).toHaveValue('')
})

for(const width of [1440,390])test.describe(`phase annotations at ${width}px`,()=>{
 test.use({viewport:{width,height:950},hasTouch:width===390})
 for(const theme of ['light','dark'])test(`exact original, tooltip and legend in ${theme}`,async({page})=>{
  const seeded=annotatedHistory(),original=seeded.sessions[0].messages[0].content,errors=[];page.on('pageerror',e=>errors.push(e.message))
  await fixture(page,false,false,seeded);await page.goto('/courses/chat-course?section=case-analysis')
  if(theme==='dark')await page.getByRole('button',{name:'切換深色模式',exact:true}).click()
  await page.getByRole('button',{name:'A 病前功能',exact:true}).click();await page.locator('[data-round="annotated-u"]').click()
  const answer=page.locator('[data-annotated-text]'),triggers=answer.locator('[data-annotation-segment]'),tooltip=page.getByRole('tooltip')
  await expect(answer).toBeVisible();expect(await answer.textContent()).toBe(original);await expect(answer.locator('b')).toHaveCount(0)
  await expect(answer.locator('[data-phases="A C"]')).toHaveCount(1)
  expect((await answer.evaluate(e=>getComputedStyle(e).getPropertyValue('--phase-A'))).trim()).toBe(theme==='dark'?'#86bbff':'#1861b8')
  expect(await answer.locator('[data-current-phase="A"]').first().evaluate(e=>getComputedStyle(e).backgroundColor)).not.toBe('rgba(0, 0, 0, 0)')
  for(const phase of ['B','C','D'])expect(await answer.locator(`[data-underline-phases~="${phase}"]`).first().evaluate(e=>getComputedStyle(e).backgroundImage)).toContain('linear-gradient')
  await triggers.first().focus();await expect(tooltip).toContainText('A1a — 年齡');await expect(tooltip).toContainText('A1b — 性別');await page.keyboard.press('Escape');await expect(tooltip).toHaveCount(0)
  if(width===390){await triggers.first().tap();await expect(tooltip).toBeVisible();await triggers.first().tap();await expect(tooltip).toHaveCount(0)}
  else{await triggers.first().hover();await expect(tooltip).toBeVisible();await tooltip.hover();await page.waitForTimeout(180);await expect(tooltip).toBeVisible()}
  await answer.locator('[data-phases="A C"]').focus();await expect(tooltip).toContainText('有待查證')
  const bounds=await tooltip.boundingBox();expect(bounds.x).toBeGreaterThanOrEqual(0);expect(bounds.x+bounds.width).toBeLessThanOrEqual(width);expect(bounds.y+bounds.height).toBeLessThanOrEqual(950)
  await page.screenshot({path:`test-results/phase-annotation-${width}-${theme}-overview.png`,fullPage:true})
  await answer.scrollIntoViewIfNeeded();await answer.locator('[data-phases="A C"]').blur();await answer.locator('[data-phases="A C"]').focus();await expect(tooltip).toBeVisible()
  await page.screenshot({path:`test-results/phase-annotation-${width}-${theme}.png`})
  await page.getByRole('heading',{name:'個案分析 AI 助教',exact:true}).click();await expect(tooltip).toHaveCount(0)
  const legend=page.locator('.annotation-legend');await legend.locator(':scope > summary').click();await legend.getByText('C — 功能評估結果',{exact:true}).click();await legend.locator('[data-legend-code="C5"]').click();await expect(legend.locator('[data-legend-code="C5"]')).toHaveAttribute('aria-pressed','true');await expect(answer.locator('.annotation-emphasized')).toHaveCount(2)
  await page.getByRole('button',{name:'修改這次作答',exact:true}).click();await expect(page.getByLabel('訊息',{exact:true})).toHaveValue(original);await page.getByLabel('訊息',{exact:true}).fill('未提交修改');expect(await answer.textContent()).toBe(original)
  await page.locator('[data-round="revised-u"]').click();await expect(answer).toHaveText('另一版：男性 男性');await expect(answer.locator('[data-annotation-segment]')).toHaveCount(1);expect(await answer.locator('[data-annotation-segment]').evaluate(e=>{const range=document.createRange();range.setStart(e.parentNode,0);range.setEndBefore(e);return range.toString()})).toBe('另一版：男性 ')
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);expect(errors).toEqual([])
 })
})
test('teacher reads annotations on the saved answer without an editable composer',async({page})=>{
 await fixture(page,true,false,annotatedHistory());await page.goto('/courses/chat-course?section=case-analysis');await page.getByRole('button',{name:'學生紀錄與統計',exact:true}).click();await page.getByRole('button',{name:'TEST01 合成學員',exact:true}).click();await page.locator('[data-round="annotated-u"]').click()
 await expect(page.locator('[data-annotated-text] [data-annotation-segment]')).toHaveCount(5);await expect(page.locator('textarea')).toHaveCount(0);await page.locator('[data-phases="D"]').focus();await expect(page.getByRole('tooltip')).toContainText('階段可能不符')
})

for(const width of [1440,390])for(const theme of ['light','dark'])test(`B stage checks A-only praise and highlights B classifications at ${width}px in ${theme}`,async({page})=>{
 await page.setViewportSize({width,height:1050})
 const first='  55歲男性，病前生活可自理。\n',second=first+'診斷為腦中風，目前病程需追蹤。',praise='原有 AI 回饋：你清楚描述了個案的病前情況。'
 const age={annotationId:'age',start:2,end:7,text:'55歲男性',codes:['A1a','A1b'],status:'unverified',explanation:'合成人口學分類，未驗證臨床內容。'},start=second.indexOf('診斷為腦中風')
 const seeded={activeSessionId:'b-session',sessions:[{id:'b-session',stage:'illnessCourse',status:'active',messages:[
  {id:'b-old-u',role:'user',content:first,status:'completed'},{id:'b-old-a',role:'assistant',replyTo:'b-old-u',content:praise,annotations:[age]},
  {id:'b-mixed-u',role:'user',content:second,status:'completed'},{id:'b-mixed-a',role:'assistant',replyTo:'b-mixed-u',content:'原有 AI 回饋：請持續核對案例依據。',annotations:[age,{annotationId:'disease',start,end:start+'診斷為腦中風'.length,text:'診斷為腦中風',codes:['B6'],status:'unsupported',explanation:'分類為疾病病程，但內容依據仍待查證。'}]},
  {id:'b-context-u',role:'user',content:'病前有腦中風病史。',status:'completed'},{id:'b-context-a',role:'assistant',replyTo:'b-context-u',content:'原有 AI 回饋：請說明既往病史與本次病程的關係。',annotations:[{annotationId:'context',start:3,end:6,text:'腦中風',codes:['A1e','B6'],status:'unverified',explanation:'同一段可涉及既往疾病史與診斷，仍須核對目前問題所需的背景。'}]}
 ]}]}
 const capture=async name=>{await page.getByRole('heading',{name:'個案分析 AI 助教',exact:true}).click();await page.evaluate(()=>window.scrollTo({top:0,behavior:'instant'}));await page.screenshot({path:`test-results/phase-alignment-${name}-${width}-${theme}.png`,fullPage:true})}
 await fixture(page,false,false,seeded,'illnessCourse');await page.goto('/courses/chat-course?section=case-analysis')
 if(theme==='dark')await page.getByRole('button',{name:'切換深色模式',exact:true}).click()
 await page.getByRole('button',{name:'B 疾病病程',exact:true}).click();await page.locator('[data-round="b-old-u"]').click()
 const answer=page.locator('[data-annotated-text]'),notice=page.locator('.from-assistant [data-phase-alignment]')
 expect(await answer.textContent()).toBe(first);await expect(page.locator('.from-assistant .message-content')).toHaveText(praise)
 await expect(notice).toContainText('目前階段：B 疾病病程');await expect(notice).toContainText('A 病前功能');await expect(notice).toContainText('請核對是否放錯階段');await expect(notice).toContainText('非 AI 回覆原文')
 await expect(answer.locator('[data-current-phase="B"]')).toHaveCount(0);await expect(answer.locator('[data-underline-phases="A"]')).toHaveCount(1)
 await page.locator('.chat-log').evaluate(el=>el.style.maxHeight='none');await capture('B-A')
 await page.locator('[data-round="b-mixed-u"]').click();expect(await answer.textContent()).toBe(second)
 const current=answer.locator('[data-current-phase="B"]');await expect(current).toHaveCount(1);await expect(current).toHaveAttribute('data-underline-phases','')
 const background=await current.evaluate(el=>getComputedStyle(el).backgroundColor);expect(background).toBe('rgb(255, 224, 102)');expect(await current.evaluate(el=>getComputedStyle(el).color)).toBe('rgb(51, 42, 0)');expect(await current.evaluate(el=>getComputedStyle(el).backgroundImage)).toBe('none')
 expect(await answer.locator('[data-underline-phases="A"]').evaluate(el=>getComputedStyle(el).backgroundImage)).toContain('linear-gradient')
 await current.focus();await expect(page.getByRole('tooltip')).toContainText('目前階段相符不代表臨床正確');await expect(page.getByRole('tooltip')).toContainText('有待查證');await page.keyboard.press('Escape')
 await expect(notice).toContainText('必要的跨階段背景可以保留');await expect(notice).not.toContainText('請核對是否放錯階段');await expect(notice).toContainText('有待查證')
 await capture('B-highlight')
 await page.getByRole('button',{name:'修改這次作答',exact:true}).click();await expect(page.getByLabel('訊息',{exact:true})).toHaveValue(second);expect(await answer.textContent()).toBe(second)
 await page.locator('[data-round="b-context-u"]').click();const both=answer.locator('[data-current-phase="B"][data-underline-phases="A"]');await expect(both).toHaveText('腦中風');expect(await both.evaluate(el=>getComputedStyle(el).backgroundImage)).toContain('linear-gradient');expect(await both.evaluate(el=>getComputedStyle(el).backgroundColor)).not.toBe('rgba(0, 0, 0, 0)');await expect(notice).toContainText('必要的跨階段背景可以保留');await expect(notice).not.toContainText('請核對是否放錯階段');await both.focus();await expect(page.getByRole('tooltip')).toContainText('未驗證');await page.keyboard.press('Escape');await capture('B-context')
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true)
})
