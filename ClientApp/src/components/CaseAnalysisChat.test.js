import {mount,flushPromises} from '@vue/test-utils'
import {beforeEach,afterEach,it,expect,vi} from 'vitest'
import CaseAnalysisChat from './CaseAnalysisChat.vue'
import PhaseAnnotatedAnswer from './PhaseAnnotatedAnswer.vue'
import ChatPerformanceDialog from './ChatPerformanceDialog.vue'
import {auth} from '../composables/auth'
import {experience} from '../composables/experience'
import {api} from '../api'
import {onBeforeRouteLeave,onBeforeRouteUpdate} from 'vue-router'
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({onBeforeRouteLeave:vi.fn(),onBeforeRouteUpdate:vi.fn()}))
const base='/courses/c/case-analysis',stages=[{key:'premorbid',label:'A 病前功能'},{key:'illnessCourse',label:'B 疾病病程'},{key:'functionalAssessment',label:'C 功能評估結果'},{key:'psychosocial',label:'D 心理社會條件'}]
const empty=()=>({sessions:[],activeSessionId:null})
let wrappers=[],rawId,rawMessages,history,config
function view(){const v=mount(CaseAnalysisChat,{props:{course:{id:'c',courseRole:config.canReview?'teacher':'student'}}});wrappers.push(v);return v}
async function mode(v,key){await v.get(`[data-mode="${key}"]`).trigger('click');await flushPromises()}
async function send(v,text){await v.get('textarea').setValue(text);await v.get('form').trigger('submit');await flushPromises()}
beforeEach(()=>{
 auth.user=null;experience.student=false
 rawId=0;rawMessages=[];history=empty();config={enabled:true,canChat:true,canReview:false,isPractice:false,modelName:'test-model',maximumMessageCharacters:8000,stages}
 vi.spyOn(window,'confirm').mockReturnValue(true)
 api.mockReset().mockImplementation(async(p,o)=>{
  if(p===base)return config
  if(p===base+'/raw/sessions'){rawMessages=[];return {id:'raw-'+(++rawId)}}
  if(p.endsWith('/messages')&&p.includes('/raw/')){rawMessages.push({role:'user',content:o.body.content},{role:'assistant',content:'回覆 '+o.body.content});return {sessionId:'raw-'+rawId,messages:[...rawMessages],modelName:'test-model'}}
  if(o?.method==='DELETE')return null
  if(p.includes('/stages/'))return history
  if(p===base+'/records')return {students:[]}
  throw new Error('Unexpected '+p)
 })
})
const capture=text=>({promptVersion:'captured-v3',requests:[{purpose:'feedback',url:'http://model.test/api/chat',payload:{model:'captured-model',messages:[{role:'system',content:'實際系統提示'},{role:'user',content:text}],format:{type:'object'},options:{temperature:0.2}}}]})
it.each(['raw','premorbid'])('shows a reply placeholder while %s inference is pending and clears it on success or failure',async(key)=>{
 const v=view();await flushPromises();if(key!=='raw')await mode(v,key)
 let finish;api.mockImplementationOnce(()=>new Promise(resolve=>{finish=resolve}))
 await v.get('textarea').setValue('pending answer');await v.get('form').trigger('submit')
 expect(v.get('[data-reply-loading]').attributes('role')).toBe('status');expect(v.get('[data-reply-loading]').text()).toContain('AI 助教正在回覆');expect(v.text()).not.toContain('尚無訊息。')
 const reply={id:'a',role:'assistant',replyTo:'u',content:'completed response'}
 finish(key==='raw'?{sessionId:'raw-1',messages:[reply]}:{activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'u',role:'user',status:'completed',content:'pending answer'},reply]}]});await flushPromises()
 expect(v.find('[data-reply-loading]').exists()).toBe(false);expect(v.text()).toContain('completed response')
 let fail;api.mockImplementationOnce(()=>new Promise((_,reject)=>{fail=reject}))
 await v.get('textarea').setValue('retained on failure');await v.get('form').trigger('submit');expect(v.find('[data-reply-loading]').exists()).toBe(true)
 fail(new Error('模型暫時無法回覆'));await flushPromises();expect(v.find('[data-reply-loading]').exists()).toBe(false);expect(v.get('textarea').element.value).toBe('retained on failure')
})
it('binds each live stage capture to its exact request reply and preserves prior captures across full-history sends',async()=>{
 config.canReview=true;config.canInspectRequests=true;history=savedRounds();const original=api.getMockImplementation()
 api.mockImplementation(async(p,o)=>{
  if(p.endsWith('/premorbid/messages')&&o?.method==='POST'){
   const id=o.body.requestId;history.sessions[0].messages.push({id,role:'user',content:o.body.content,status:'completed'},{id:'a-'+id,role:'assistant',replyTo:id,content:'新回饋'});return {...structuredClone(history),requestInspection:capture(o.body.content)}
  }return original(p,o)
 })
 const storage=vi.spyOn(Storage.prototype,'setItem'),v=view();await flushPromises();await mode(v,'premorbid');await send(v,'第一個擷取');const first=v.findAll('[data-round]').at(0).attributes('data-round');await send(v,'第二個擷取')
 await v.get('[data-action="performance"]').trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toEqual(capture('第二個擷取'))
 await v.get('[data-action="request-inspection"]').trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('initialTab')).toBe('request');expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toEqual(capture('第二個擷取'))
 await v.get(`[data-round="${first}"]`).trigger('click');await v.get('[data-action="performance"]').trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toEqual(capture('第一個擷取'))
 await v.get('[data-round="u3"]').trigger('click');await v.get('[data-action="performance"]').trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toBeNull()
 expect(v.get('textarea').element.value).toBe('');expect(storage).not.toHaveBeenCalled();expect(api.mock.calls.filter(([,o])=>o?.body).every(([,o])=>!JSON.stringify(o.body).includes('實際系統提示'))).toBe(true)
 await mode(v,'illnessCourse');await mode(v,'premorbid');await v.get(`[data-round="${first}"]`).trigger('click');await v.get('[data-action="performance"]').trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toBeNull()
})
it('captures successful retry only for the retried persisted user',async()=>{
 config.canReview=true;config.canInspectRequests=true;history={activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'retry-user',role:'user',content:'retry',status:'failed'}]}]};const v=view();await flushPromises();await mode(v,'premorbid')
 api.mockImplementationOnce(async()=>({activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'retry-user',role:'user',content:'retry',status:'completed'},{id:'retry-a',role:'assistant',replyTo:'retry-user',content:'retry success'}]}],requestInspection:capture('retry')}))
 await v.get('[data-action="retry-message"]').trigger('click');await flushPromises();await v.get('[data-action="performance"]').trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toEqual(capture('retry'))
})
it('keeps Raw captures by session and reply position and clears them on expiry, clear and pagehide',async()=>{
 config.canReview=true;config.canInspectRequests=true;const original=api.getMockImplementation();api.mockImplementation(async(p,o)=>{const data=await original(p,o);return p.includes('/raw/')&&p.endsWith('/messages')?{...data,requestInspection:capture(o.body.content)}:data})
 const v=view();await flushPromises();await send(v,'raw first');await send(v,'raw second');await v.findAll('[data-action="performance"]')[0].trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toEqual(capture('raw first'))
 api.mockImplementationOnce(async()=>{throw Object.assign(new Error('expired'),{status:404})});await send(v,'expired');await v.findAll('[data-action="performance"]')[0].trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toBeNull()
 await v.get('[data-action="clear-raw"]').trigger('click');await flushPromises();await send(v,'fresh');await v.get('[data-action="performance"]').trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toEqual(capture('fresh'))
 window.dispatchEvent(new PageTransitionEvent('pagehide',{persisted:true}));await flushPromises();expect(v.getComponent(ChatPerformanceDialog).props('open')).toBe(false);expect(v.findAll('[data-action="performance"]')).toHaveLength(0)
})
it('disables inspection for students even if a payload is accidentally returned and never exposes it in teacher review',async()=>{
 config.canInspectRequests=true;const original=api.getMockImplementation();api.mockImplementation(async(p,o)=>{const data=await original(p,o);return p.includes('/raw/')&&p.endsWith('/messages')?{...data,requestInspection:capture('secret')}:data})
 const v=view();await flushPromises();await send(v,'student');await v.get('[data-action="performance"]').trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('canInspectRequests')).toBe(false);expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toBeNull()
 expect(v.find('[data-action="request-inspection"]').exists()).toBe(false)
 v.unmount();config.canReview=true;const staff=view();await flushPromises();await send(staff,'staff');await staff.get('[data-action="performance"]').trigger('click');expect(staff.getComponent(ChatPerformanceDialog).props('canInspectRequests')).toBe(true)
 await staff.get('[data-action="review"]').trigger('click');await flushPromises();expect(staff.getComponent(ChatPerformanceDialog).props('canInspectRequests')).toBe(false);expect(staff.getComponent(ChatPerformanceDialog).props('requestInspection')).toBeNull()
})
it('drops captures synchronously on account or student-experience changes and ignores late responses',async()=>{
 config.canReview=true;config.canInspectRequests=true;const v=view();await flushPromises();let finish;api.mockImplementationOnce(()=>new Promise(resolve=>{finish=resolve}));await v.get('textarea').setValue('pending');await v.get('form').trigger('submit')
 experience.student=true;await v.vm.$nextTick();finish({sessionId:'raw-1',messages:[{role:'assistant',content:'late'}],requestInspection:capture('late')});await flushPromises();expect(v.text()).not.toContain('late');expect(v.getComponent(ChatPerformanceDialog).props('canInspectRequests')).toBe(false)
 experience.student=false;auth.user={id:'new-user'};await v.vm.$nextTick();expect(v.getComponent(ChatPerformanceDialog).props('open')).toBe(false)
})
afterEach(()=>{wrappers.forEach(v=>v.unmount());wrappers=[];vi.restoreAllMocks()})
it('checks only the paired original against the active B stage inside the feedback bubble without rewriting AI praise',async()=>{
 config.classifications=[{code:'A1a',phase:'A',label:'年齡'}]
 history={activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'u',role:'user',content:'55歲',status:'completed'},{id:'a',role:'assistant',replyTo:'u',content:'原有 AI 讚美文字',annotations:[{annotationId:'age',start:0,end:3,text:'55歲',codes:['A1a'],status:'unverified'}]},{id:'u2',role:'user',content:'另一版',status:'completed'},{id:'a2',role:'assistant',replyTo:'u2',content:'舊回饋',annotations:null}]}]}
 const v=view();await flushPromises();await mode(v,'illnessCourse');await v.get('[data-round="u"]').trigger('click')
 expect(v.getComponent(PhaseAnnotatedAnswer).props('stage')).toBe('illnessCourse');expect(v.get('.from-assistant .message-content').text()).toBe('原有 AI 讚美文字')
 expect(v.get('.from-assistant [data-phase-alignment]').text()).toContain('目前階段：B 疾病病程');expect(v.get('.from-assistant [data-phase-alignment]').text()).toContain('A 病前功能')
 await v.get('[data-round="u2"]').trigger('click');expect(v.find('[data-phase-alignment]').exists()).toBe(false)
})
it('uses the teacher review stage for annotation colors and the same in-bubble check',async()=>{
 config.canReview=true;config.classifications=[{code:'A1a',phase:'A',label:'年齡'},{code:'B1',phase:'B',label:'疾病診斷'}]
 const original=api.getMockImplementation(),historyFor=stage=>({activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'u',role:'user',content:'55歲',status:'completed'},{id:'a',role:'assistant',replyTo:'u',content:'原有回饋',annotations:[{annotationId:'age',start:0,end:3,text:'55歲',codes:['A1a'],status:'unverified'}]}]}]})
 api.mockImplementation(async(p,o)=>p===base+'/records'?{students:[{id:'s1',displayName:'合成學員',statistics:{},stages:[]}]}:p.includes('/records/s1/')?historyFor(p.split('/').at(-1)):original(p,o))
 const v=view();await flushPromises();await v.get('[data-action="review"]').trigger('click');await flushPromises();await v.get('[data-student="s1"]').trigger('click');await flushPromises()
 expect(v.getComponent(PhaseAnnotatedAnswer).props('stage')).toBe('premorbid');expect(v.get('[data-annotation-segment]').attributes('data-current-phase')).toBe('A')
 await v.get('.review-stage select').setValue('illnessCourse');await flushPromises();expect(v.getComponent(PhaseAnnotatedAnswer).props('stage')).toBe('illnessCourse');expect(v.get('[data-annotation-segment]').attributes('data-underline-phases')).toBe('A');expect(v.get('.from-assistant [data-phase-alignment]').text()).toContain('B 疾病病程');expect(v.find('textarea').exists()).toBe(false)
})
it('keeps three raw turns on the server session, clears it, and never restores on remount',async()=>{
 const store=vi.spyOn(Storage.prototype,'setItem'),v=view();await flushPromises()
 for(const text of ['第一輪','第二輪','第三輪'])await send(v,text)
 const requests=api.mock.calls.filter(([p])=>p.includes('/raw/')&&p.endsWith('/messages'))
 expect(requests).toHaveLength(3);expect(requests.map(([,o])=>o.body)).toEqual([{content:'第一輪'},{content:'第二輪'},{content:'第三輪'}]);expect(requests.every(([p])=>p.includes('/raw-1/'))).toBe(true)
 expect(v.findAll('.chat-message')).toHaveLength(6)
 await v.get('[data-action="clear-raw"]').trigger('click');await flushPromises()
 expect(api).toHaveBeenCalledWith(base+'/raw/sessions/raw-1',expect.objectContaining({method:'DELETE'}));expect(rawId).toBe(2);expect(v.findAll('.chat-message')).toHaveLength(0)
 v.unmount();const fresh=view();await flushPromises();expect(rawId).toBe(3);expect(fresh.findAll('.chat-message')).toHaveLength(0);expect(store).not.toHaveBeenCalled()
})
it('confirms raw departure, fetches persisted stage history and keeps modes separate',async()=>{
 const v=view();await flushPromises();await send(v,'raw only')
 window.confirm.mockReturnValueOnce(false);await mode(v,'premorbid');expect(v.text()).toContain('raw only')
 history={activeSessionId:'s',sessions:[{id:'s',stage:'premorbid',status:'active',messages:[{id:'u',role:'user',content:'saved stage',status:'completed'}]}]}
 await mode(v,'premorbid');expect(v.text()).toContain('saved stage');expect(v.text()).not.toContain('raw only')
 await mode(v,'raw');expect(v.text()).not.toContain('saved stage');expect(rawId).toBe(2)
 expect(api.mock.calls.some(([p])=>p.includes('/records'))).toBe(false)
})
it('recovers failed persisted user messages and retries their ID without resending the draft',async()=>{
 const v=view();await flushPromises();await mode(v,'premorbid')
 api.mockImplementationOnce(async()=>{history={activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'failed-user',role:'user',content:'keep answer',status:'failed'}]}]};throw new Error('模型暫時失敗')})
 await send(v,'keep answer');expect(v.text()).toContain('模型暫時失敗');expect(v.get('textarea').element.value).toBe('keep answer')
 await v.get('[data-action="retry-message"]').trigger('click');await flushPromises()
 expect(api).toHaveBeenCalledWith(base+'/stages/premorbid/messages/failed-user/retry',expect.objectContaining({method:'POST'}))
 expect(api.mock.calls.filter(([p,o])=>p===base+'/stages/premorbid/messages'&&o?.method==='POST')).toHaveLength(1)
})
it('preserves request identity after an ambiguous send failure and escapes untrusted content',async()=>{
 const v=view();await flushPromises();await mode(v,'premorbid')
 api.mockImplementationOnce(async()=>{throw new TypeError('網路中斷')})
 await send(v,'<img src=x onerror=alert(1)>');await send(v,'<img src=x onerror=alert(1)>')
 const requests=api.mock.calls.filter(([p,o])=>p.endsWith('/premorbid/messages')&&o?.method==='POST')
 expect(requests[0][1].body.requestId).toBe(requests[1][1].body.requestId)
 await mode(v,'raw');await send(v,'<img src=x onerror=alert(1)>');expect(v.find('.chat-message img').exists()).toBe(false);expect(v.text()).toContain('<img src=x onerror=alert(1)>')
})
it('supports authorized staff records and labels teaching practice as excluded from formal statistics',async()=>{
 config.canReview=true;config.isPractice=true
 api.mockImplementation(async(p,o)=>{
  if(o?.method==='DELETE')return null
  if(p===base)return config
  if(p===base+'/raw/sessions')return {id:'raw-1'}
  if(p===base+'/records')return {students:[{id:'student-1',displayName:'合成學員',studentNumber:'TEST01',statistics:{studentMessages:3,assistantReplies:2,roundTrips:2,revisionCount:1,lastActivityAt:'2026-11-16T00:00:00Z'},stages:[{stage:'premorbid',statistics:{studentMessages:3,assistantReplies:2,roundTrips:2,revisionCount:1}}]}]}
  if(p===base+'/records/student-1/premorbid')return {activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'u',role:'user',content:'formal student message'}]}]}
  throw new Error('Unexpected '+p)
 })
 const v=view();await flushPromises();expect(v.text()).toContain('不計入正式學生統計')
 await v.get('[data-action="review"]').trigger('click');await flushPromises();expect(v.text()).toContain('TEST01');expect(v.text()).toContain('全課程合計');expect(v.get('.statistics-summary').text()).toContain('修改 1')
 await v.get('[data-student="student-1"]').trigger('click');await flushPromises();expect(v.text()).toContain('formal student message');expect(v.find('textarea').exists()).toBe(false)
})
it('does not let a late raw response append to a stage after switching modes',async()=>{
 const v=view();await flushPromises();let finish
 api.mockImplementationOnce(()=>new Promise(resolve=>{finish=resolve}))
 await v.get('textarea').setValue('pending raw');await v.get('form').trigger('submit');await mode(v,'premorbid')
 finish({sessionId:'raw-1',messages:[{role:'assistant',content:'late raw'}]});await flushPromises()
 expect(v.text()).not.toContain('late raw');expect(v.get('[data-mode="premorbid"]').attributes('aria-pressed')).toBe('true')
})
it('retains raw context on 413 and offers a fresh session after expiry',async()=>{
 const v=view();await flushPromises();await send(v,'first turn')
 api.mockImplementationOnce(async()=>{throw Object.assign(new Error('too long'),{status:413})})
 await send(v,'draft retained');expect(v.text()).toContain('不會自動刪除歷史');expect(v.text()).toContain('first turn');expect(v.get('textarea').element.value).toBe('draft retained')
 api.mockImplementationOnce(async()=>{throw Object.assign(new Error('expired'),{status:404})})
 await send(v,'draft retained');expect(v.get('[data-action="clear-raw"]').text()).toContain('對話已逾時');expect(v.get('textarea').attributes('disabled')).toBeDefined()
 await v.get('[data-action="clear-raw"]').trigger('click');await flushPromises();expect(rawId).toBe(2);expect(v.findAll('.chat-message')).toHaveLength(0)
})
it('explains a stage input limit without suggesting that clearing database history increases context',async()=>{
 const v=view();await flushPromises();await mode(v,'premorbid')
 api.mockImplementationOnce(async()=>{throw Object.assign(new Error('too long'),{status:413})})
 await send(v,'stage draft retained');expect(v.text()).toContain('請縮短本次作答');expect(v.text()).toContain('重新開始對話不會增加本次容量');expect(v.get('textarea').element.value).toBe('stage draft retained')
})
it('fetches saved stages again after remount and makes closed histories read-only',async()=>{
 history={activeSessionId:'active',sessions:[{id:'closed',status:'closed',messages:[{id:'u',role:'user',content:'old saved conversation'}]},{id:'active',status:'active',messages:[]}]}
 const v=view();await flushPromises();await mode(v,'premorbid');await v.get('.history-select select').setValue('closed')
 expect(v.text()).toContain('old saved conversation');expect(v.find('textarea').exists()).toBe(false)
 v.unmount();const again=view();await flushPromises();await mode(again,'premorbid');await again.get('.history-select select').setValue('closed');expect(again.text()).toContain('old saved conversation')
 expect(api.mock.calls.filter(([p])=>p===base+'/stages/premorbid')).toHaveLength(2)
})
it('reminds before browser unload and preserves the raw conversation if clearing fails',async()=>{
 const v=view();await flushPromises();await send(v,'keep this raw')
 const event=new Event('beforeunload',{cancelable:true});window.dispatchEvent(event);expect(event.defaultPrevented).toBe(true)
 api.mockImplementationOnce(async()=>{throw new Error('clear failed')})
 await mode(v,'premorbid');expect(v.text()).toContain('clear failed');expect(v.text()).toContain('keep this raw');expect(v.get('[data-mode="raw"]').attributes('aria-pressed')).toBe('true')
})
it('starts a new stage conversation when only closed history exists',async()=>{
 history={activeSessionId:null,sessions:[{id:'closed',status:'closed',messages:[{id:'u',role:'user',content:'old closed conversation'}]}]}
 const v=view();await flushPromises();await mode(v,'premorbid')
 expect(v.find('textarea').exists()).toBe(true);expect(v.text()).not.toContain('old closed conversation');expect(v.get('.history-select select').element.value).toBe('')
 await v.get('.history-select select').setValue('closed');expect(v.text()).toContain('old closed conversation');expect(v.find('textarea').exists()).toBe(false)
})
it('honors read-only preview and restricted TA review capabilities',async()=>{
 config.canChat=false;config.canReview=false;config.isPractice=true
 const v=mount(CaseAnalysisChat,{props:{course:{id:'c',courseRole:'ta'}}});wrappers.push(v);await flushPromises()
 expect(v.text()).toContain('目前為唯讀檢視');expect(v.find('textarea').exists()).toBe(false);expect(v.find('[data-action="review"]').exists()).toBe(false)
 expect(api.mock.calls.some(([p])=>p.includes('/records')||p.includes('/raw/sessions'))).toBe(false)
})
it('confirms stage restart and keeps old sessions available as read-only history',async()=>{
 history={activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'u',role:'user',content:'saved before restart'}]}]}
 const v=view();await flushPromises();await mode(v,'premorbid')
 window.confirm.mockReturnValueOnce(false);await v.get('[data-action="restart-stage"]').trigger('click');expect(api.mock.calls.some(([p])=>p.endsWith('/restart'))).toBe(false)
 history={activeSessionId:null,sessions:[{id:'s',status:'closed',messages:[{id:'u',role:'user',content:'saved before restart'}]}]}
 await v.get('[data-action="restart-stage"]').trigger('click');await flushPromises();expect(v.find('textarea').exists()).toBe(true);expect(v.text()).not.toContain('saved before restart')
 await v.get('.history-select select').setValue('s');expect(v.text()).toContain('saved before restart');expect(v.find('textarea').exists()).toBe(false)
})
it('reclaims raw capacity on pagehide across more than three reloads using keepalive deletion',async()=>{
 const live=new Set();let created=0
 api.mockImplementation(async(p,o)=>{
  if(p===base)return config
  if(p===base+'/raw/sessions'){
   if(live.size>=3)throw Object.assign(new Error('capacity reached'),{status:429})
   const id='reload-'+(++created);live.add(id);return {id}
  }
  if(o?.method==='DELETE'){live.delete(p.split('/').at(-1));return null}
  throw new Error('Unexpected '+p)
 })
 for(let i=0;i<5;i++){
  const v=view();await flushPromises();expect(live.size).toBe(1);expect(v.find('[role="alert"]').exists()).toBe(false)
  window.dispatchEvent(new PageTransitionEvent('pagehide',{persisted:false}));await flushPromises()
  expect(api).toHaveBeenCalledWith(`${base}/raw/sessions/reload-${i+1}`,{method:'DELETE',keepalive:true})
  expect(live.size).toBe(0);v.unmount()
 }
 expect(created).toBe(5)
})
it('clears raw messages and draft immediately on pagehide and allocates new context on BFcache restore',async()=>{
 const v=view();await flushPromises();await send(v,'raw must not restore');await v.get('textarea').setValue('private unsent draft')
 let finishDelete
 api.mockImplementationOnce(()=>new Promise(resolve=>{finishDelete=resolve}))
 window.dispatchEvent(new PageTransitionEvent('pagehide',{persisted:true}));await v.vm.$nextTick()
 expect(v.findAll('.chat-message')).toHaveLength(0);expect(v.get('textarea').element.value).toBe('')
 window.dispatchEvent(new PageTransitionEvent('pageshow',{persisted:true}));await flushPromises();expect(rawId).toBe(1)
 finishDelete(null);await flushPromises();expect(rawId).toBe(2);expect(v.text()).not.toContain('raw must not restore')
 await send(v,'fresh context');expect(api).toHaveBeenCalledWith(`${base}/raw/sessions/raw-2/messages`,expect.objectContaining({body:{content:'fresh context'}}))
})
it('refetches persisted stage history on BFcache restoration without creating raw sessions',async()=>{
 const v=view();await flushPromises();await mode(v,'premorbid');const rawAllocations=rawId
 window.dispatchEvent(new PageTransitionEvent('pagehide',{persisted:true}))
 history={activeSessionId:'saved',sessions:[{id:'saved',status:'active',messages:[{id:'u',role:'user',content:'stage updated while hidden'}]}]}
 window.dispatchEvent(new PageTransitionEvent('pageshow',{persisted:true}));await flushPromises()
 expect(v.text()).toContain('stage updated while hidden');expect(rawId).toBe(rawAllocations)
})
it('retries a persisted failed message with original padded content through the composer without duplicate user messages',async()=>{
 const content='  原始內容保留前後空白 \n',v=view();await flushPromises();await mode(v,'premorbid')
 api.mockImplementationOnce(async()=>{history={activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'padded-user',role:'user',content,status:'failed'}],statistics:{studentMessages:1,assistantReplies:0,roundTrips:0}}]};throw new Error('模型失敗')})
 await send(v,content);expect(v.get('textarea').element.value).toBe(content);expect(v.get('.message-content').element.textContent).toBe(content)
 api.mockImplementationOnce(async()=>({activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'padded-user',role:'user',content,status:'completed'},{id:'a',role:'assistant',content:'成功回覆',status:'completed',replyTo:'padded-user'}],statistics:{studentMessages:1,assistantReplies:1,roundTrips:1}}]}))
 await v.get('form').trigger('submit');await flushPromises()
 expect(api).toHaveBeenCalledWith(`${base}/stages/premorbid/messages/padded-user/retry`,expect.objectContaining({method:'POST'}))
 expect(api.mock.calls.filter(([p,o])=>p===base+'/stages/premorbid/messages'&&o?.method==='POST')).toHaveLength(1)
 expect(v.findAll('.from-user')).toHaveLength(1);expect(v.findAll('.from-assistant')).toHaveLength(1);expect(v.get('.from-user .message-content').element.textContent).toBe(content);expect(v.get('textarea').element.value).toBe('')
})
it('requires explicit account-wide confirmation to recover from raw allocation capacity after unload cleanup fails',async()=>{
 const original=api.getMockImplementation();let limited=false
 api.mockImplementation(async(p,o)=>{
  if(p===base+'/raw/sessions'&&o?.method==='DELETE'){limited=false;return null}
  if(p===base+'/raw/sessions'&&limited)throw Object.assign(new Error('too many sessions'),{status:429})
  return original(p,o)
 })
 const v=view();await flushPromises();await send(v,'old raw context')
 limited=true;api.mockImplementationOnce(async()=>{throw new Error('browser dropped keepalive')})
 window.dispatchEvent(new PageTransitionEvent('pagehide',{persisted:true}));window.dispatchEvent(new PageTransitionEvent('pageshow',{persisted:true}));await flushPromises()
 expect(v.find('[data-action="recover-raw"]').exists()).toBe(true);expect(v.text()).not.toContain('old raw context')
 window.confirm.mockReturnValueOnce(false);await v.get('[data-action="recover-raw"]').trigger('click');await flushPromises()
 expect(api.mock.calls.some(([p,o])=>p===base+'/raw/sessions'&&o?.method==='DELETE')).toBe(false)
 await v.get('[data-action="recover-raw"]').trigger('click');await flushPromises()
 expect(window.confirm).toHaveBeenLastCalledWith('將清除本帳號所有課程與分頁的純 AI 聊天暫存，無法恢復，確定？')
 expect(api).toHaveBeenCalledWith(base+'/raw/sessions',expect.objectContaining({method:'DELETE',keepalive:false}));expect(rawId).toBe(2)
 expect(v.find('[data-action="recover-raw"]').exists()).toBe(false);expect(v.findAll('.chat-message')).toHaveLength(0)
 await send(v,'new account context');expect(api).toHaveBeenCalledWith(base+'/raw/sessions/raw-2/messages',expect.objectContaining({body:{content:'new account context'}}))
})

function savedRounds(){return {activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[
 {id:'u1',role:'user',content:'  第一份原始作答 \n',status:'completed',revisesMessageId:null},{id:'a1',role:'assistant',content:'第一輪回饋',replyTo:'u1',metrics:null},
 {id:'u2',role:'user',content:'第二份獨立作答',status:'completed',revisesMessageId:null},{id:'a2',role:'assistant',content:'第二輪回饋',replyTo:'u2',metrics:{promptTokens:20,responseTokens:10}},
 {id:'u3',role:'user',content:'第三份修改作答',status:'completed',revisesMessageId:'u1'},{id:'a3',role:'assistant',content:'第三輪回饋',replyTo:'u3',modelName:'saved-response-model',promptVersion:'saved-v1',metrics:{promptTokens:30,responseTokens:15}}
 ]}]}}
it('retains the annotation display choice when selecting another response round without storing it',async()=>{
 config.classifications=[{code:'A1a',label:'年齡',phase:'A'}];history=savedRounds()
 const messages=history.sessions[0].messages
 for(const reply of messages.filter(m=>m.role==='assistant')){const answer=messages.find(m=>m.id===reply.replyTo);reply.annotations=[{annotationId:reply.id,start:0,end:answer.content.length,text:answer.content,codes:['A1a'],status:'unverified'}]}
 const storage=vi.spyOn(Storage.prototype,'setItem'),v=view();await flushPromises();await mode(v,'premorbid')
 await v.get('[data-annotation-mode="badges"]').trigger('click');await v.get('[data-round="u1"]').trigger('click')
 expect(v.get('[data-annotation-mode="badges"]').attributes('aria-pressed')).toBe('true');expect(v.getComponent(PhaseAnnotatedAnswer).props('displayMode')).toBe('badges');expect(v.find('[data-annotation-reference]').exists()).toBe(true)
 expect(v.get('.from-assistant [data-phase-alignment]').text()).toContain('編碼徽章');expect(storage).not.toHaveBeenCalled()
})
it('selects one feedback pair without changing the draft and labels explicit revisions only',async()=>{
 history=savedRounds();const v=view();await flushPromises();await mode(v,'premorbid')
 expect(v.findAll('[data-round]')).toHaveLength(3);expect(v.get('.chat-log').text()).toContain('第三輪回饋');expect(v.get('.chat-log').text()).not.toContain('第一輪回饋')
 expect(v.findAll('[data-round]').map(item=>item.attributes('data-round'))).toEqual(['u3','u2','u1'])
 expect(v.findAll('[data-round]').map(item=>item.get('strong').text())).toEqual(['第 3 輪 · 修改 1 次','第 2 輪 · 原始作答','第 1 輪 · 原始作答'])
 await v.get('textarea').setValue('尚未提交的內容');await v.get('[data-round="u1"]').trigger('click')
 expect(v.get('.chat-log').text()).toContain('第一輪回饋');expect(v.get('.chat-log').text()).not.toContain('第三輪回饋');expect(v.get('textarea').element.value).toBe('尚未提交的內容')
 expect(v.get('[data-round="u2"]').text()).toContain('原始作答');expect(v.get('[data-round="u3"]').text()).toContain('修改 1 次')
})
it('copies an old answer verbatim and submits a new immutable revision with its parent',async()=>{
 history=savedRounds();const v=view();await flushPromises();await mode(v,'premorbid');await v.get('[data-round="u1"]').trigger('click');await v.get('[data-action="revise-answer"]').trigger('click')
 expect(v.get('textarea').element.value).toBe('  第一份原始作答 \n');await v.get('textarea').setValue('  修改舊答案 \n')
 const original=api.getMockImplementation();api.mockImplementation(async(p,o)=>{if(p.endsWith('/premorbid/messages')&&o?.method==='POST'){history.sessions[0].messages.push({id:'u4',role:'user',content:o.body.content,revisesMessageId:o.body.revisesMessageId,status:'completed'},{id:'a4',role:'assistant',content:'新修改回饋',replyTo:'u4'});return structuredClone(history)}return original(p,o)})
 await v.get('form').trigger('submit');await flushPromises()
 const request=api.mock.calls.find(([p,o])=>p.endsWith('/premorbid/messages')&&o?.method==='POST')[1].body
 expect(request).toEqual({requestId:expect.any(String),content:'  修改舊答案 \n',revisesMessageId:'u1'});expect(history.sessions[0].messages[0].content).toBe('  第一份原始作答 \n');expect(v.get('.chat-log').text()).toContain('新修改回饋')
 expect(v.get('[data-action="next-stage"]').text()).toContain('B 疾病病程');expect(v.get('[data-mode="premorbid"]').attributes('aria-pressed')).toBe('true')
})
it('preserves revision parent and request identity on ambiguous failure and clears both when switching stage',async()=>{
 history=savedRounds();const v=view();await flushPromises();await mode(v,'premorbid');await v.get('[data-round="u1"]').trigger('click');await v.get('[data-action="revise-answer"]').trigger('click')
 api.mockImplementationOnce(async()=>{throw new TypeError('network uncertain')});await send(v,' revision ');await send(v,' revision ')
 const sends=api.mock.calls.filter(([p,o])=>p.endsWith('/premorbid/messages')&&o?.method==='POST');expect(sends[0][1].body).toEqual(sends[1][1].body);expect(sends[0][1].body.revisesMessageId).toBe('u1')
 await mode(v,'illnessCourse');await send(v,'independent answer');const last=api.mock.calls.filter(([p,o])=>p.endsWith('/illnessCourse/messages')&&o?.method==='POST').at(-1)
 expect(last[1].body.revisesMessageId).toBeNull()
})
it('opens metrics for the selected response including unavailable old metrics and closes on mode switch',async()=>{
 history=savedRounds();const v=view();await flushPromises();await mode(v,'premorbid')
 await v.get('[data-action="performance"]').trigger('click');expect(v.findComponent({name:'ChatPerformanceDialog'}).props('metrics')).toEqual({promptTokens:30,responseTokens:15});expect(v.findComponent({name:'ChatPerformanceDialog'}).props('modelName')).toBe('saved-response-model');expect(v.findComponent({name:'ChatPerformanceDialog'}).props('promptVersion')).toBe('saved-v1')
 await v.findComponent({name:'ChatPerformanceDialog'}).vm.$emit('close');await v.get('[data-round="u1"]').trigger('click');await v.get('[data-action="performance"]').trigger('click')
 expect(v.findComponent({name:'ChatPerformanceDialog'}).props('metrics')).toBeNull();await mode(v,'illnessCourse');expect(v.findComponent({name:'ChatPerformanceDialog'}).props('open')).toBe(false)
})
it('compares all student inputs in the selected stage while excluding AI responses and other stages',async()=>{
 history=savedRounds();const v=view();await flushPromises();await mode(v,'premorbid')
 const revisions=v.findComponent({name:'AnswerRevisionDiff'}).props('revisions');expect(revisions.map(r=>r.id)).toEqual(['u1','u2','u3']);expect(revisions[0].content).toBe('  第一份原始作答 \n')
 history={activeSessionId:'different',sessions:[{id:'different',status:'active',messages:[{id:'other-stage',role:'user',content:'不同階段'}]}]};await mode(v,'illnessCourse')
 expect(v.findComponent({name:'AnswerRevisionDiff'}).props('revisions').map(r=>r.id)).toEqual(['other-stage'])
})
it('retries a failed revision without creating another revision and preserves its padded content',async()=>{
 history=savedRounds();const v=view();await flushPromises();await mode(v,'premorbid');await v.get('[data-round="u1"]').trigger('click');await v.get('[data-action="revise-answer"]').trigger('click')
 const content='  修改但暫時失敗 \n';api.mockImplementationOnce(async(p,o)=>{history.sessions[0].messages.push({id:'failed-revision',role:'user',content:o.body.content,status:'failed',revisesMessageId:o.body.revisesMessageId});history=structuredClone(history);throw new Error('模型失敗')})
 await send(v,content);expect(v.get('textarea').element.value).toBe(content);expect(v.get('[data-round="failed-revision"]').text()).toContain('修改 1 次');expect(v.get('[data-action="revise-answer"]').attributes('disabled')).toBeDefined()
 const original=api.getMockImplementation();api.mockImplementation(async(p,o)=>{if(p.endsWith('/failed-revision/retry')){history.sessions[0].messages.at(-1).status='completed';return structuredClone(history)}return original(p,o)})
 await v.get('form').trigger('submit');await flushPromises()
 expect(api.mock.calls.filter(([p,o])=>p.endsWith('/premorbid/messages')&&o?.method==='POST')).toHaveLength(1);expect(api.mock.calls.some(([p])=>p.endsWith('/failed-revision/retry'))).toBe(true);expect(v.findAll('[data-round]')).toHaveLength(4);expect(v.get('.from-user .message-content').element.textContent).toBe(content)
})
it('keeps comparison inputs isolated to the selected learner and blocks edits in teacher review',async()=>{
 config.canReview=true;const original=api.getMockImplementation();api.mockImplementation(async(p,o)=>{
  if(p===base+'/records')return {students:[{id:'s1',displayName:'學員一'},{id:'s2',displayName:'學員二'}]}
  if(p===base+'/records/s1/premorbid')return savedRounds()
  if(p===base+'/records/s2/premorbid')return {activeSessionId:'second',sessions:[{id:'second',status:'active',messages:[{id:'second-user',role:'user',content:'第二位學生作答'}]}]}
  return original(p,o)
 })
 const v=view();await flushPromises();await v.get('[data-action="review"]').trigger('click');await flushPromises();await v.get('[data-student="s1"]').trigger('click');await flushPromises()
 expect(v.findComponent({name:'AnswerRevisionDiff'}).props('revisions')).toHaveLength(3);expect(v.find('[data-action="revise-answer"]').exists()).toBe(false);expect(v.find('textarea').exists()).toBe(false)
 await v.get('[data-student="s2"]').trigger('click');await flushPromises();expect(v.findComponent({name:'AnswerRevisionDiff'}).props('revisions').map(r=>r.id)).toEqual(['second-user']);expect(v.get('.chat-log').text()).not.toContain('第三份修改作答')
})
it('displays ephemeral raw performance and removes it with the raw conversation',async()=>{
 const v=view();await flushPromises();const original=api.getMockImplementation();api.mockImplementation(async(p,o)=>{const data=await original(p,o);if(p.includes('/raw/')&&p.endsWith('/messages'))data.messages.at(-1).metrics={promptTokens:17,responseTokens:5};return data})
 await send(v,'raw metrics');await v.get('[data-action="performance"]').trigger('click');expect(v.findComponent({name:'ChatPerformanceDialog'}).props('metrics')).toEqual({promptTokens:17,responseTokens:5})
 await v.get('[data-action="clear-raw"]').trigger('click');await flushPromises();expect(v.findComponent({name:'ChatPerformanceDialog'}).props('open')).toBe(false);expect(v.findAll('[data-action="performance"]')).toHaveLength(0);expect(v.find('.round-sidebar').exists()).toBe(false)
})
it('confirms replacing a draft and preserves its revision parent when revising again is canceled',async()=>{
 history=savedRounds();const v=view();await flushPromises();await mode(v,'premorbid');await v.get('[data-round="u1"]').trigger('click');await v.get('textarea').setValue('  私人草稿 \n')
 window.confirm.mockReturnValueOnce(false);await v.get('[data-action="revise-answer"]').trigger('click');expect(v.get('textarea').element.value).toBe('  私人草稿 \n');expect(v.find('.revision-notice').exists()).toBe(false)
 await v.get('[data-action="revise-answer"]').trigger('click');expect(v.get('textarea').element.value).toBe('  第一份原始作答 \n');await v.get('textarea').setValue('  編輯中的修改 \n')
 window.confirm.mockReturnValueOnce(false);await v.get('[data-action="revise-answer"]').trigger('click');expect(v.get('textarea').element.value).toBe('  編輯中的修改 \n');expect(v.get('.revision-notice').text()).toContain('第 1 輪')
 await v.get('[data-round="u2"]').trigger('click');window.confirm.mockReturnValueOnce(false);await v.get('[data-action="revise-answer"]').trigger('click');expect(v.get('.revision-notice').text()).toContain('第 1 輪');expect(v.get('textarea').element.value).toBe('  編輯中的修改 \n')
 await v.get('[data-action="revise-answer"]').trigger('click');expect(v.get('.revision-notice').text()).toContain('第 2 輪');expect(v.get('textarea').element.value).toBe('第二份獨立作答')
})
it('guards next-stage and session changes without losing the selected session or draft on cancel',async()=>{
 history=savedRounds();history.sessions.push({id:'closed',status:'closed',messages:[{id:'old',role:'user',status:'completed',content:'舊對話'}]});const v=view();await flushPromises();await mode(v,'premorbid');await v.get('[data-round="u1"]').trigger('click');await v.get('[data-action="revise-answer"]').trigger('click');await v.get('textarea').setValue('正在編輯')
 window.confirm.mockReturnValueOnce(false);await v.get('[data-action="next-stage"]').trigger('click');await flushPromises();expect(v.get('[data-mode="premorbid"]').attributes('aria-pressed')).toBe('true');expect(v.get('textarea').element.value).toBe('正在編輯')
 window.confirm.mockReturnValueOnce(false);await v.get('.history-select select').setValue('closed');expect(v.get('.history-select select').element.value).toBe('s');expect(v.get('.chat-log').text()).toContain('第一輪回饋');expect(v.get('.revision-notice').text()).toContain('第 1 輪')
 await v.get('.history-select select').setValue('closed');expect(v.find('textarea').exists()).toBe(false);expect(v.get('.chat-log').text()).toContain('舊對話')
 await v.get('.history-select select').setValue('s');expect(v.get('textarea').element.value).toBe('');expect(v.find('.revision-notice').exists()).toBe(false)
 await v.get('textarea').setValue('允許清除');await v.get('[data-action="next-stage"]').trigger('click');await flushPromises();expect(v.get('[data-mode="illnessCourse"]').attributes('aria-pressed')).toBe('true');expect(v.get('textarea').element.value).toBe('')
})
it('protects formal drafts on teacher review, browser unload and router navigation',async()=>{
 config.canReview=true;history=savedRounds();const v=view();await flushPromises();await mode(v,'premorbid');await v.get('textarea').setValue('尚未提交')
 const event=new Event('beforeunload',{cancelable:true});window.dispatchEvent(event);expect(event.defaultPrevented).toBe(true)
 const leave=onBeforeRouteLeave.mock.calls.at(-1)[0],update=onBeforeRouteUpdate.mock.calls.at(-1)[0],from={params:{courseId:'c'},query:{section:'case-analysis'}}
 window.confirm.mockReturnValueOnce(false);expect(leave()).toBe(false);window.confirm.mockReturnValueOnce(false);expect(update({params:{courseId:'c'},query:{section:'units'}},from)).toBe(false);expect(update(from,from)).toBe(true)
 window.confirm.mockReturnValueOnce(false);await v.get('[data-action="review"]').trigger('click');await flushPromises();expect(v.get('textarea').element.value).toBe('尚未提交');expect(api.mock.calls.some(([p])=>p===base+'/records')).toBe(false)
 expect(leave()).toBe(true);await v.get('[data-action="review"]').trigger('click');await flushPromises();expect(v.find('textarea').exists()).toBe(false)
})
it('anchors annotations to the paired assistant and submitted version while leaving drafts and Raw untouched',async()=>{
 config.classifications=[{code:'A1a',label:'年齡',phase:'A'}]
 const annotations=[{annotationId:'age',start:0,end:3,text:'55歲',codes:['A1a'],explanation:'年齡',status:'unverified'}]
 history={activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'u1',role:'user',content:'55歲',status:'completed'},{id:'a1',role:'assistant',replyTo:'u1',content:'回饋',annotations},{id:'u2',role:'user',content:'另一版',status:'pending'}]}]}
 const v=view();await flushPromises();expect(v.findComponent(PhaseAnnotatedAnswer).exists()).toBe(false);await mode(v,'premorbid');expect(v.findComponent(PhaseAnnotatedAnswer).exists()).toBe(false)
 await v.get('[data-round="u1"]').trigger('click');expect(v.getComponent(PhaseAnnotatedAnswer).props('content')).toBe('55歲');expect(v.getComponent(PhaseAnnotatedAnswer).props('annotations')).toEqual(annotations)
 await v.get('[data-action="revise-answer"]').trigger('click');expect(v.get('textarea').element.value).toBe('55歲');await v.get('textarea').setValue('未提交修改');expect(v.getComponent(PhaseAnnotatedAnswer).props('content')).toBe('55歲')
 await v.get('[data-round="u2"]').trigger('click');expect(v.findComponent(PhaseAnnotatedAnswer).exists()).toBe(false)
})
it('clears annotation inspection when switching independent sessions and supports teacher readback only for a paired response',async()=>{
 config.canReview=true;config.classifications=[{code:'A1a',label:'年齡',phase:'A'}]
 const annotations=[{annotationId:'age',start:0,end:3,text:'55歲',codes:['A1a'],explanation:'合成分類',status:'unverified'}]
 const first={id:'first',status:'closed',messages:[{id:'user-first',role:'user',content:'55歲',status:'completed'},{id:'assistant-first',role:'assistant',replyTo:'user-first',content:'合成回饋',annotations}]}
 const second={id:'second',status:'active',messages:[{id:'user-second',role:'user',content:'不同會話',status:'completed'},{id:'assistant-second',role:'assistant',replyTo:'user-second',content:'舊回饋',annotations:null}]}
 const original=api.getMockImplementation();api.mockImplementation(async(p,o)=>{
  if(p===base+'/records')return {students:[{id:'student',displayName:'合成學員'}]}
  if(p===base+'/records/student/premorbid')return {activeSessionId:'first',sessions:[first,second]}
  return original(p,o)
 })
 const v=view();await flushPromises();await v.get('[data-action="review"]').trigger('click');await flushPromises();await v.get('[data-student="student"]').trigger('click');await flushPromises()
 await v.get('[data-annotation-segment]').trigger('focus');expect(v.find('[role="tooltip"]').exists()).toBe(true);expect(v.find('textarea').exists()).toBe(false)
 await v.get('.history-select select').setValue('second');expect(v.find('[role="tooltip"]').exists()).toBe(false);expect(v.find('[data-annotation-segment]').exists()).toBe(false);expect(v.get('[data-annotated-text]').element.textContent).toBe('不同會話')
 await v.get('.history-select select').setValue('first');expect(v.find('[role="tooltip"]').exists()).toBe(false);expect(v.getComponent(PhaseAnnotatedAnswer).props('content')).toBe('55歲')
})
