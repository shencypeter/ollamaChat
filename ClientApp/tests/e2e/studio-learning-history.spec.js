import {test,expect} from '@playwright/test'
import {readFileSync} from 'node:fs'
import {retryLogin} from '../helpers/auth'
const stateFile=new URL('../../../.codex-build/portal-smoke.json',import.meta.url)
test.use({actionTimeout:15000})

test('PDF and video readers accumulate only active server-bounded study sessions',async({playwright,browser,baseURL})=>{
 test.setTimeout(120000)
 const state=JSON.parse(readFileSync(stateFile,'utf8')),teacher=await playwright.request.newContext({baseURL}),student=await playwright.request.newContext({baseURL})
 for(const [ctx,role] of [[teacher,'Professor'],[student,'Student']])expect((await retryLogin(ctx,{email:state.accounts.find(a=>a.role===role).email,password:state.password})).status()).toBe(200)
 async function write(ctx,path,data,extra={}){const token=(await(await ctx.get('/api/auth/csrf')).json()).token;return ctx.post('/api'+path,{...(data?{data}:{}),...extra,headers:{'X-CSRF-TOKEN':token}})}
 const c=await(await write(teacher,'/courses',{code:'MEDIA-'+Date.now(),title:'合成影片與講義計時',termCode:'115-1'})).json(),base=`/courses/${c.id}`
 await write(teacher,base+'/members',{userId:state.accounts.find(a=>a.role==='Student').id,courseRole:'student'})
 const context=await browser.newContext({storageState:await student.storageState()}),page=await context.newPage()
 const videoBytes=Buffer.from(await page.evaluate(async()=>{
  const canvas=document.createElement('canvas');canvas.width=320;canvas.height=180;const ctx=canvas.getContext('2d'),chunks=[]
  const stream=canvas.captureStream(5),recorder=new MediaRecorder(stream,{mimeType:'video/webm;codecs=vp8'})
  let frame=0;const tick=setInterval(()=>{ctx.fillStyle='#4f4c9b';ctx.fillRect(0,0,320,180);ctx.fillStyle='white';ctx.font='20px sans-serif';ctx.fillText('Synthetic lesson '+frame++,20,90)},200)
  const done=new Promise(resolve=>{recorder.ondataavailable=e=>chunks.push(e.data);recorder.onstop=async()=>{clearInterval(tick);stream.getTracks().forEach(t=>t.stop());resolve([...new Uint8Array(await new Blob(chunks).arrayBuffer())])}})
  recorder.start();setTimeout(()=>recorder.stop(),14000);return done
 }))
 const stream='BT /F1 18 Tf 30 100 Td (Synthetic PDF lesson) Tj ET',objects=['<< /Type /Catalog /Pages 2 0 R >>','<< /Type /Pages /Kids [3 0 R] /Count 1 >>','<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 200] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>','<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',`<< /Length ${stream.length} >>\nstream\n${stream}\nendstream`];let text='%PDF-1.4\n',offsets=[];for(let i=0;i<objects.length;i++){offsets.push(Buffer.byteLength(text));text+=`${i+1} 0 obj\n${objects[i]}\nendobj\n`}const start=Buffer.byteLength(text);text+=`xref\n0 6\n0000000000 65535 f \n${offsets.map(o=>String(o).padStart(10,'0')+' 00000 n \n').join('')}trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${start}\n%%EOF`
 const resources=[]
 for(const [name,buffer] of [['lesson.pdf',Buffer.from(text)],['lesson.webm',videoBytes]])resources.push(await(await write(teacher,base+'/resources',null,{multipart:{file:{name,mimeType:'application/octet-stream',buffer}}})).json())
 for(let i=0;i<resources.length;i++){
  const r=resources[i];await page.goto(baseURL+`/courses/${c.id}/materials/${r.id}`);await expect(page.getByRole('button',{name:'開始／繼續計時'})).toBeEnabled()
  await page.getByRole('button',{name:'開始／繼續計時'}).click();if(i===1)await page.locator('video').evaluate(v=>v.play())
  await expect.poll(async()=> (await(await student.get('/api'+base+`/resources/${r.id}/learning`)).json()).accumulatedSeconds,{timeout:20000}).toBeGreaterThanOrEqual(5)
  await page.getByRole('button',{name:'暫停計時',exact:true}).click();await expect(page.getByText('計時已暫停',{exact:true})).toBeVisible()
  await page.screenshot({path:`test-results/learning-reader-${i===0?'pdf':'video'}.png`,fullPage:true})
 }
 await context.close();await teacher.dispose();await student.dispose()
})

test('student cross-course history and material time gate protect questions and preserve grades',async({playwright,browser,baseURL})=>{
 test.setTimeout(120000)
 const state=JSON.parse(readFileSync(stateFile,'utf8')),teacher=await playwright.request.newContext({baseURL}),student=await playwright.request.newContext({baseURL}),peer=await playwright.request.newContext({baseURL})
 const learner=state.accounts.find(a=>a.role==='Student'),feedback='只有公布後可見的評語-'+Date.now()
 for(const [ctx,role] of [[teacher,'Professor'],[student,'Student'],[peer,'TeachingAssistant']])expect((await retryLogin(ctx,{email:state.accounts.find(a=>a.role===role).email,password:state.password})).status()).toBe(200)
 async function write(ctx,path,data,method='POST',extra={}){const token=(await(await ctx.get('/api/auth/csrf')).json()).token;return ctx.fetch('/api'+path,{method,headers:{'X-CSRF-TOKEN':token},...(data?{data}:{}),...extra})}
 await write(student,'/studio/profile',{displayName:'合成歷程學生',studentNumber:'QA-'+state.run,departmentYear:'合成系級'},'PUT')
 const cs=[]
 for(const term of ['114-2','115-1']){const r=await write(teacher,'/courses',{code:'LH-'+term+'-'+Date.now(),title:'跨學期學習歷程合成課程',termCode:term});expect(r.status()).toBe(200);const c=await r.json();cs.push(c);expect((await write(teacher,`/courses/${c.id}/members`,{userId:learner.id,courseRole:'student'})).status()).toBe(204)}
 const c=cs[1],root=`/courses/${c.id}`
 const image=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jR1sAAAAASUVORK5CYII=','base64')
 const upload=await write(teacher,root+'/resources',null,'POST',{multipart:{file:{name:'合成教材.png',mimeType:'image/png',buffer:image},title:'測驗前指定教材'}});expect(upload.status()).toBe(200);const resource=await upload.json()
 expect((await peer.get('/api'+root+`/resources/${resource.id}/learning`)).status()).toBe(403)
 const tc=await browser.newContext({storageState:await teacher.storageState()}),tp=await tc.newPage(),errors=[];tp.on('pageerror',e=>errors.push(e.message))
 await tp.goto(baseURL+`/assessments?course=${c.id}`);await tp.getByRole('button',{name:'建立線上／課後測驗',exact:true}).click()
 await tp.getByLabel('測驗名稱',{exact:true}).fill('教材時間門檻合成測驗')
 await tp.getByLabel('題目 1 內容',{exact:true}).fill('只在完成教材門檻後顯示的合成題目')
 await tp.getByLabel('測驗前指定教材',{exact:true}).check()
 await tp.getByLabel('最低有效秒數',{exact:true}).fill('5')
 await tp.getByRole('button',{name:'儲存測驗草稿',exact:true}).click();await expect(tp).toHaveURL(/quizzes\//)
 const quizId=tp.url().split('/').pop();expect((await(await teacher.get(`/api/assignments/${quizId}`)).json()).learningGate.requirements).toHaveLength(1)
 expect((await write(teacher,`/assignments/${quizId}/publish`)).status()).toBe(200)
 const hidden=await(await student.get(`/api/assignments/${quizId}`)).json();expect(hidden.questions).toEqual([]);expect(hidden.learningGate.locked).toBe(true)
 expect((await write(student,`/assignments/${quizId}/start`)).status()).toBe(409)
 expect((await write(student,`/assignments/${quizId}/my-submission`,{expectedVersion:0,answers:[],submit:true})).status()).toBe(409)
 const sc=await browser.newContext({storageState:await student.storageState()}),sp=await sc.newPage();sp.on('pageerror',e=>errors.push(e.message))
 await sp.goto(baseURL+`/quizzes/${quizId}`);await expect(sp.getByRole('heading',{name:'測驗前教材學習門檻',exact:true})).toBeVisible();await expect(sp.getByText('只在完成教材門檻後顯示的合成題目',{exact:true})).toHaveCount(0)
 await sp.getByRole('link',{name:'測驗前指定教材',exact:true}).click();await expect(sp.getByRole('button',{name:'開始／繼續計時'})).toBeEnabled();await sp.getByRole('button',{name:'開始／繼續計時'}).click()
 await expect.poll(async()=>{const r=await student.get('/api'+root+`/resources/${resource.id}/learning`);return (await r.json()).accumulatedSeconds},{timeout:20000}).toBeGreaterThanOrEqual(5)
 await sp.getByRole('button',{name:'暫停計時',exact:true}).click();await sp.goto(baseURL+`/quizzes/${quizId}`);await expect(sp.getByRole('button',{name:'開始測驗',exact:true})).toBeEnabled();await sp.getByRole('button',{name:'開始測驗',exact:true}).click();await expect(sp.getByText('只在完成教材門檻後顯示的合成題目',{exact:true})).toBeVisible()
 const opened=await(await student.get(`/api/assignments/${quizId}`)).json()
 expect((await write(student,`/assignments/${quizId}/my-submission`,{expectedVersion:0,submit:true,answers:[{questionId:opened.questions[0].id,text:'合成測驗回答'}]})).status()).toBe(200)
 const submissions=await(await teacher.get(`/api/assignments/${quizId}/submissions`)).json(),revs=await(await teacher.get(`/api/assignments/${quizId}/submissions/${submissions[0].id}/revisions`)).json()
 const grade=await(await write(teacher,`/assignments/${quizId}/revisions/${revs[0].id}/grades`,{score:0,feedback,criteria:[],essayScores:{[opened.questions[0].id]:0}})).json()
 let h=await(await student.get('/api/learning-history')).json();expect(h.items.find(x=>x.id===quizId).score).toBeNull();expect(JSON.stringify(h)).not.toContain(feedback)
 expect((await write(teacher,`/assignments/${quizId}/revisions/${revs[0].id}/grades/${grade.id}/release`)).status()).toBe(200)
 h=await(await student.get('/api/learning-history')).json();expect(h.items.find(x=>x.id===quizId).score).toBe(0);expect(h.items.some(x=>x.kind==='material')).toBe(true)
 expect((await(await teacher.get('/api/learning-history/courses')).json())).toEqual([])
 expect((await(await peer.get(`/api/learning-history?course=${c.id}`)).json()).items).toEqual([])
 await sp.goto(baseURL+'/learning-history');await expect(sp.getByRole('heading',{name:'我的學習歷程',exact:true})).toBeVisible();await expect(sp.getByText('教師評語：'+feedback)).toBeVisible()
 await sp.getByLabel('學期',{exact:true}).selectOption('114-2');await expect(sp.getByText('教師評語：'+feedback)).toHaveCount(0);await sp.getByLabel('學期',{exact:true}).selectOption('115-1')
 await expect(sp.getByText('教師評語：'+feedback)).toBeVisible();await sp.screenshot({path:'test-results/learning-history-desktop.png',fullPage:true})
 await sp.setViewportSize({width:390,height:844});expect(await sp.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await sp.screenshot({path:'test-results/learning-history-mobile.png',fullPage:true})
 expect(errors).toEqual([]);await tc.close();await sc.close();for(const ctx of [teacher,student,peer])await ctx.dispose()
})
