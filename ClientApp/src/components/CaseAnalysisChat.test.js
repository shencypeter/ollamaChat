import {mount,flushPromises} from '@vue/test-utils'
import {beforeEach,afterEach,it,expect,vi} from 'vitest'
import CaseAnalysisChat from './CaseAnalysisChat.vue'
import CaseFormalSubmissions from './CaseFormalSubmissions.vue'
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
function view(attach=false,role='teacher'){const v=mount(CaseAnalysisChat,{attachTo:attach?document.body:undefined,props:{course:{id:'c',courseRole:role}}});wrappers.push(v);return v}
async function mode(v,key){if(key!=='raw'&&!v.find('[data-phase-navigation]').exists()){await v.get(`[data-workspace-tab="${!config.canChat||history.sessions?.some(s=>s.messages?.some(m=>m.role==='user'))?'view':'write'}"]`).trigger('click');await flushPromises();if(!v.find('[data-phase-navigation]').exists())return}await v.get(`[data-mode="${key}"]`).trigger('click');await flushPromises()}
async function chooseRound(v,id){await v.get('[data-workspace-tab="view"]').trigger('click');await v.get(`[data-round="${id}"]`).trigger('click')}
function selectedRoundRemove(v){const id=v.get('[data-round][aria-current="true"]').attributes('data-round');return v.get(`[data-remove-round="${id}"]`)}
function mixedModelHistory(){return {activeSessionId:'active-models',sessions:[
 {id:'active-models',status:'active',messages:[{id:'mixed-3',role:'user',content:'最新作答',status:'completed',createdAt:'2026-11-23T03:00:00Z',modelName:'qwen3:8b'},{id:'mixed-a3',role:'assistant',replyTo:'mixed-3',content:'Qwen 新回饋',modelName:'qwen3:8b'},{id:'mixed-4',role:'user',content:'不同模型作答',status:'completed',createdAt:'2026-11-23T04:00:00Z',modelName:'llama3.1:8b'},{id:'mixed-a4',role:'assistant',replyTo:'mixed-4',content:'Llama 新回饋',modelName:'llama3.1:8b'}]},
 {id:'legacy-models',status:'closed',messages:[{id:'mixed-1',role:'user',content:'舊版原稿',status:'completed',createdAt:'2026-11-23T01:00:00Z'},{id:'mixed-a1',role:'assistant',replyTo:'mixed-1',content:'未記錄模型的舊回饋'},{id:'mixed-2',role:'user',content:'舊模型作答',status:'completed',createdAt:'2026-11-23T02:00:00Z',modelName:'retired-model:7b'},{id:'mixed-a2',role:'assistant',replyTo:'mixed-2',content:'歷史模型回饋',modelName:'retired-model:7b'}]}
]}}
function historyPage(before=13,model='',size=12){
 const all=Array.from({length:size},(_,i)=>({id:`page-${i+1}`,role:'user',historyNumber:i+1,content:`合成分頁${i+1}`,status:'completed',createdAt:`2026-11-23T00:00:${String(i+1).padStart(2,'0')}Z`,modelName:i%2?'llama3.1:8b':'qwen3:8b'}))
 const matches=all.filter(m=>!model||`model:${m.modelName}`===model),eligible=matches.filter(m=>m.historyNumber<before).reverse(),chosen=eligible.slice(0,5)
 return {activeSessionId:'paged',sessions:[{id:'paged',status:'active',messages:chosen.slice().reverse().flatMap(m=>[m,{id:`reply-${m.id}`,role:'assistant',replyTo:m.id,content:`回饋${m.historyNumber}`,modelName:m.modelName}])}],pagination:{nextCursor:eligible.length>5?chosen.at(-1).historyNumber:null,totalCount:matches.length,trashCount:0,models:[{key:'model:llama3.1:8b',modelName:'llama3.1:8b'},{key:'model:qwen3:8b',modelName:'qwen3:8b'}],activeLastMessageId:'reply-page-12',modelKey:model,trashed:false,pageSize:5}}
}
function pagedMock(intercept){const fallback=api.getMockImplementation();api.mockImplementation(async(p,o)=>{if(p.includes('/stages/')&&(!o?.method||o.method==='GET')){const query=new URL('https://test'+p).searchParams;if(intercept){const result=await intercept(query,o);if(result)return result}return historyPage(Number(query.get('before')||13),query.get('model')||'')}return fallback(p,o)})}
async function send(v,text){const writing=v.find('[data-workspace-tab="write"]');if((!v.find('[data-mode="raw"]').exists()||v.find('[data-mode="raw"]').attributes('aria-selected')!=='true')&&writing.exists()&&!writing.element.disabled)await writing.trigger('click');await v.get('textarea').setValue(text);await v.get('form').trigger('submit');await flushPromises()}
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
  if(p===base+'/submissions')return {submissions:[],assignments:[]}
  throw new Error('Unexpected '+p)
 })
})
it('makes raw Enter sending opt in and preserves multiline, IME and modifier input',async()=>{
 const v=view();await flushPromises();const input=v.get('#case-chat-draft'),toggle=v.get('[data-enter-send]'),sent=()=>api.mock.calls.filter(([p])=>p.includes('/raw/')&&p.endsWith('/messages'))
 const key=options=>{const event=new KeyboardEvent('keydown',{key:'Enter',bubbles:true,cancelable:true,...options});input.element.dispatchEvent(event);return event}
 expect(toggle.element.checked).toBe(false);await input.setValue('第一行');expect(key().defaultPrevented).toBe(false);await flushPromises();expect(sent()).toHaveLength(0)
 await toggle.setValue(true);expect(v.text()).toContain('Shift+Enter 換行')
 for(const options of [{shiftKey:true},{ctrlKey:true},{metaKey:true},{altKey:true},{isComposing:true},{keyCode:229}]){expect(key(options).defaultPrevented).toBe(false)}
 await input.trigger('compositionstart');expect(key().defaultPrevented).toBe(false);await input.trigger('compositionend');await flushPromises();expect(sent()).toHaveLength(0)
 expect(key({repeat:true}).defaultPrevented).toBe(true);await flushPromises();expect(sent()).toHaveLength(0)
 expect(key().defaultPrevented).toBe(true);key();await flushPromises();expect(sent()).toHaveLength(1);expect(sent()[0][1].body.content).toBe('第一行');expect(input.element.value).toBe('')
 await toggle.setValue(false);await input.setValue('第二行');expect(key().defaultPrevented).toBe(false);await flushPromises();expect(sent()).toHaveLength(1)
})
it('keeps Enter sending out of formal writing and resets it for a newly opened page',async()=>{
 const v=view();await flushPromises();await v.get('[data-enter-send]').setValue(true);await mode(v,'premorbid');await v.get('[data-workspace-tab="write"]').trigger('click');await v.get('textarea').setValue('正式作答保留換行')
 expect(v.find('[data-enter-send]').exists()).toBe(false);const before=api.mock.calls.length,event=new KeyboardEvent('keydown',{key:'Enter',bubbles:true,cancelable:true});v.get('textarea').element.dispatchEvent(event);await flushPromises();expect(event.defaultPrevented).toBe(false);expect(api.mock.calls).toHaveLength(before);expect(v.get('textarea').element.value).toBe('正式作答保留換行')
 const fresh=view();await flushPromises();expect(fresh.get('[data-enter-send]').element.checked).toBe(false)
})
it('keeps a copied revision number when older pages append or a model filter hides its parent',async()=>{
 pagedMock();const v=view();await flushPromises();await mode(v,'premorbid');await v.get('[data-action="revise-answer"]').trigger('click');expect(v.get('.revision-notice').text()).toContain('#12')
 await v.get('[data-workspace-tab="view"]').trigger('click');await v.get('[data-action="load-more-history"]').trigger('click');await flushPromises();expect(v.get('.revision-notice').text()).toContain('#12')
 await v.get('[data-history-model-filter]').setValue('model:qwen3:8b');await flushPromises();expect(v.get('.revision-notice').text()).toContain('#12');expect(v.get('textarea').element.value).toBe('合成分頁12')
})
it('does not skip a gap when a mutation returns five newer attempts from another tab',async()=>{
 pagedMock(async query=>query.get('before')==='16'?historyPage(16,'',20):undefined);const fallback=api.getMockImplementation();api.mockImplementation(async(p,o)=>p.split('?')[0].endsWith('/trash')?historyPage(21,'',20):fallback(p,o))
 const v=view();await flushPromises();await mode(v,'premorbid');await chooseRound(v,'page-10');await v.get('[data-remove-round="page-10"]').trigger('click');await flushPromises();expect(v.findAll('[data-round]')).toHaveLength(5)
 await v.get('[data-action="load-more-history"]').trigger('click');await flushPromises();expect(v.find('[data-round="page-15"]').exists()).toBe(true);expect(v.findAll('[data-round]')).toHaveLength(10)
})
it('looks up an older completed result directly after a filter and newer concurrent attempts',async()=>{
 Object.defineProperty(HTMLElement.prototype,'scrollIntoView',{configurable:true,value:vi.fn()});let completedId,evolved=false
 const completion=()=>{const data=historyPage(14,'',20);data.sessions[0].messages.forEach(m=>{if(m.id==='page-13'){m.id=completedId;m.content='已完成的本輪作答'}if(m.replyTo==='page-13'){m.id='completed-reply';m.replyTo=completedId}});data.pagination.focused=true;return data}
 pagedMock(async query=>query.has('target')?completion():evolved?historyPage(21,query.get('model')||'',20):undefined);const fallback=api.getMockImplementation();api.mockImplementation(async(p,o)=>{if(p.split('?')[0].endsWith('/messages')&&o?.method==='POST'){completedId=o.body.requestId;evolved=true;return completion()}return fallback(p,o)})
 const v=view();await flushPromises();await mode(v,'premorbid');await send(v,'已完成的本輪作答');await v.get('[data-workspace-tab="view"]').trigger('click');await v.get('[data-history-model-filter]').setValue('model:llama3.1:8b');await flushPromises();await v.get('[data-workspace-tab="write"]').trigger('click');await v.get('[data-action="latest-result"]').trigger('click');await flushPromises()
 expect(api.mock.calls.some(([p])=>new URL('https://test'+p).searchParams.get('target')===completedId)).toBe(true);expect(v.get('[data-round][aria-current="true"]').attributes('data-round')).toBe(completedId);expect(v.get('.chat-log').text()).toContain('已完成的本輪作答')
})
it('refreshes a failed outgoing model outside the history filter and resubmits through retry',async()=>{
 let failedId,retried=false;config.canChooseModel=true;config.modelName='qwen3:8b';config.modelProfiles=[{key:'qwen',label:'Qwen',modelName:'qwen3:8b'},{key:'llama3',label:'Llama',modelName:'llama3.1:8b'}]
 const failurePage=()=>{const data=historyPage();data.sessions[0].messages=[{id:failedId,role:'user',status:retried?'completed':'failed',content:'不可被清掉的失敗作答',modelName:'qwen3:8b',historyNumber:13}];if(retried)data.sessions[0].messages.push({id:'retry-reply',role:'assistant',replyTo:failedId,content:'重試成功'});data.pagination={...data.pagination,activeLastMessageId:retried?'retry-reply':failedId,focused:true};return data}
 pagedMock(async query=>query.has('target')?failurePage():undefined);const fallback=api.getMockImplementation();api.mockImplementation(async(p,o)=>{const path=p.split('?')[0];if(path.endsWith('/messages')&&o?.method==='POST'){failedId=o.body.requestId;throw new Error('合成模型失敗')}if(path.endsWith('/retry')){retried=true;return failurePage()}return fallback(p,o)})
 const v=view();await flushPromises();await mode(v,'premorbid');await v.get('[data-history-model-filter]').setValue('model:llama3.1:8b');await flushPromises();await send(v,'不可被清掉的失敗作答');expect(v.get('textarea').element.value).toBe('不可被清掉的失敗作答');await v.get('form').trigger('submit');await flushPromises();expect(retried).toBe(true);expect(v.get('textarea').element.value).toBe('')
 expect(api.mock.calls.filter(([p,o])=>p.split('?')[0].endsWith('/premorbid/messages')&&o?.method==='POST')).toHaveLength(1)
})
it('fetches only five pairs initially and appends more only on clicks without changing selection or draft',async()=>{
 pagedMock();const v=view();await flushPromises();await mode(v,'premorbid');expect(v.findAll('[data-round]')).toHaveLength(5);expect(v.get('[data-round]').text()).toContain('#12')
 const reads=()=>api.mock.calls.filter(([p,o])=>p.includes('/stages/')&&(!o?.method||o.method==='GET'))
 expect(new URL('https://test'+reads()[0][0]).searchParams.get('paged')).toBe('true');window.dispatchEvent(new Event('scroll'));await flushPromises();expect(reads()).toHaveLength(1)
 await v.get('[data-workspace-tab="write"]').trigger('click');await v.get('textarea').setValue('保留未送出內容');await chooseRound(v,'page-10')
 await v.get('[data-action="load-more-history"]').trigger('click');await flushPromises();expect(v.findAll('[data-round]')).toHaveLength(10);expect(v.get('[data-round][aria-current="true"]').attributes('data-round')).toBe('page-10');expect(v.get('textarea').element.value).toBe('保留未送出內容')
 expect(new URL('https://test'+reads()[1][0]).searchParams.get('before')).toBe('8');await v.get('[data-action="load-more-history"]').trigger('click');await flushPromises();expect(v.findAll('[data-round]')).toHaveLength(12);expect(v.find('[data-action="load-more-history"]').exists()).toBe(false);expect(reads()).toHaveLength(3)
 expect(new Set(v.findAll('[data-round]').map(b=>b.attributes('data-round'))).size).toBe(12)
})
it('keeps loaded rows on next-page failure and retries the same cursor',async()=>{
 let fail=true;pagedMock(async query=>{if(query.has('before')&&fail){fail=false;throw new Error('合成下一頁失敗')}})
 const v=view();await flushPromises();await mode(v,'premorbid');await v.get('[data-action="load-more-history"]').trigger('click');await flushPromises();expect(v.findAll('[data-round]')).toHaveLength(5);expect(v.get('[data-history-error]').text()).toContain('合成下一頁失敗')
 await v.get('[data-action="load-more-history"]').trigger('click');await flushPromises();expect(v.findAll('[data-round]')).toHaveLength(10);expect(v.find('[data-history-error]').exists()).toBe(false)
})
it('deduplicates clicks and ignores a late page after changing the server-side model filter',async()=>{
 let finish,signal;pagedMock(async(query,o)=>{if(query.has('before')&&!query.get('model')){signal=o.signal;return new Promise(resolve=>{finish=resolve})}})
 const v=view();await flushPromises();await mode(v,'premorbid');const more=v.get('[data-action="load-more-history"]');await more.trigger('click');await more.trigger('click');expect(api.mock.calls.filter(([p])=>p.includes('before=8'))).toHaveLength(1);expect(more.element.disabled).toBe(true)
 await v.get('[data-history-model-filter]').setValue('model:qwen3:8b');await flushPromises();expect(signal.aborted).toBe(true);expect(v.findAll('[data-round]')).toHaveLength(5);expect(v.get('[data-round]').text()).toContain('#11')
 finish(historyPage(8));await flushPromises();expect(v.findAll('[data-round]')).toHaveLength(5);expect(v.get('[data-history-model-filter]').element.value).toBe('model:qwen3:8b');expect(v.find('[data-round="page-12"]').exists()).toBe(false)
})
it('keeps a paged model filter when the selected attempt is trashed',async()=>{
 pagedMock();const fallback=api.getMockImplementation();api.mockImplementation(async(p,o)=>{
  if(p.split('?')[0].endsWith('/trash')){const data=historyPage(13,'model:qwen3:8b');data.sessions[0].messages=data.sessions[0].messages.filter(m=>m.id!=='page-9'&&m.replyTo!=='page-9').concat(historyPage(3,'model:qwen3:8b').sessions[0].messages);data.pagination={...data.pagination,totalCount:5,trashCount:1,nextCursor:null};return data}return fallback(p,o)
 })
 const v=view();await flushPromises();await mode(v,'premorbid');await v.get('[data-history-model-filter]').setValue('model:qwen3:8b');await flushPromises();await chooseRound(v,'page-9');await v.get('[data-remove-round="page-9"]').trigger('click');await flushPromises()
 expect(v.get('[data-history-model-filter]').element.value).toBe('model:qwen3:8b');expect(v.find('[data-round="page-9"]').exists()).toBe(false);expect(v.findAll('[data-round]')).toHaveLength(5)
})
it('preserves the model filter when restoring its last trash item through a direct lookup',async()=>{
 pagedMock(async query=>{if(query.get('trashed')==='true'){const data=historyPage(3,'model:qwen3:8b');data.sessions[0].messages.forEach(m=>{m.trashedAt='2026-11-23T01:00:00Z'});data.pagination={...data.pagination,modelKey:query.get('model')||'',trashed:true,trashCount:1,totalCount:1};return data}})
 const fallback=api.getMockImplementation();api.mockImplementation(async(p,o)=>{if(p.split('?')[0].endsWith('/restore')){const data=historyPage();data.sessions=[];data.pagination={...data.pagination,modelKey:'model:qwen3:8b',trashed:true,trashCount:0,totalCount:0,nextCursor:null};return data}return fallback(p,o)})
 const v=view();await flushPromises();await mode(v,'premorbid');await v.get('[data-history-model-filter]').setValue('model:qwen3:8b');await flushPromises();await v.get('[data-action="toggle-trash"]').trigger('click');await flushPromises();await v.get('[data-action="restore-round"]').trigger('click');await flushPromises()
 expect(v.get('[data-history-model-filter]').element.value).toBe('model:qwen3:8b');const lookup=api.mock.calls.find(([p])=>new URL('https://test'+p).searchParams.has('target'));expect(new URL('https://test'+lookup[0]).searchParams.get('model')).toBe('model:qwen3:8b')
})
it('restores the previous model and page cursor when fetching a new filter fails',async()=>{
 pagedMock(async query=>{if(query.get('model'))throw new Error('合成篩選失敗')});const v=view();await flushPromises();await mode(v,'premorbid');await v.get('[data-history-model-filter]').setValue('model:qwen3:8b');await flushPromises()
 expect(v.get('[data-history-model-filter]').element.value).toBe('');expect(v.findAll('[data-round]')).toHaveLength(5);expect(v.get('[data-round]').text()).toContain('#12')
 await v.get('[data-action="load-more-history"]').trigger('click');await flushPromises();expect(v.findAll('[data-round]')).toHaveLength(10)
})
it.each(['teacher','student'])('flattens all legacy conversations into single feedback pairs and filters by saved model (%s)',async role=>{
 history=mixedModelHistory();const v=view(true,role);await flushPromises();await mode(v,'premorbid')
 expect(v.find('[data-action="restart-stage"]').exists()).toBe(false);expect(v.findAll('[data-round] strong').map(item=>item.text())).toEqual(['#4','#3','#2','#1'])
 const filter=v.get('[data-history-model-filter]');expect(filter.element.options[0].textContent).toBe('全部模型');expect([...filter.element.options].map(option=>option.textContent).join(' ')).toContain('retired-model:7b')
 await v.get('textarea').setValue('保留草稿');const requests=api.mock.calls.length
 await filter.setValue('model:qwen3:8b');expect(v.findAll('[data-round] strong').map(item=>item.text())).toEqual(['#3']);expect(v.get('.chat-log').text()).toContain('Qwen 新回饋');expect(v.get('.chat-log').text()).not.toContain('Llama 新回饋')
 await filter.setValue('model:retired-model:7b');expect(v.get('[data-round="mixed-2"] strong').text()).toBe('#2');expect(v.get('.chat-log').text()).toContain('歷史模型回饋');expect(v.find('.history-select').text()).not.toContain('版本 —')
 expect(v.get('textarea').element.value).toBe('保留草稿');expect(api.mock.calls.length).toBe(requests)
 await filter.setValue('unknown');expect(v.get('[data-round="mixed-1"] strong').text()).toBe('#1');expect(v.get('.chat-log').text()).toContain('未記錄模型的舊回饋')
 await filter.setValue('');expect(v.findAll('[data-round]')).toHaveLength(4)
})
it('copies a closed conversation attempt with its existing ancestry and keeps history filtering independent from send model',async()=>{
 history=mixedModelHistory();config.canChooseModel=true;config.modelProfiles=[{key:'qwen',label:'Qwen3',modelName:'qwen3:8b'}]
 const v=view(true);await flushPromises();await mode(v,'premorbid');await v.get('[data-model-select]').setValue('qwen');await chooseRound(v,'mixed-2');await v.get('[data-action="revise-answer"]').trigger('click')
 expect(v.get('textarea').element.value).toBe('舊模型作答');await v.get('[data-workspace-tab="view"]').trigger('click');await v.get('[data-history-model-filter]').setValue('model:llama3.1:8b');expect(v.get('[data-model-select]').element.value).toBe('qwen')
 await v.get('form').trigger('submit');await flushPromises();const request=api.mock.calls.filter(([path,options])=>path.endsWith('/premorbid/messages')&&options?.method==='POST').at(-1)[1].body
 expect(request).toMatchObject({content:'舊模型作答',revisesMessageId:'mixed-2',modelProfile:'qwen'});expect(request).not.toHaveProperty('modelFilter')
})
it('reveals a new model result hidden by the history filter and preserves its global number on completion navigation',async()=>{
 Element.prototype.scrollIntoView=vi.fn()
 history=mixedModelHistory();config.modelName='qwen3:8b';const original=api.getMockImplementation()
 api.mockImplementation(async(path,options)=>{
  if(path===base+'/stages/premorbid/messages'&&options?.method==='POST'){
   const id=options.body.requestId;history.sessions[0].messages.push({id,role:'user',content:options.body.content,status:'completed',createdAt:'2026-11-23T05:00:00Z',modelName:'qwen3:8b'},{id:'latest-new-model',role:'assistant',replyTo:id,content:'新模型完成回饋',modelName:'qwen3:8b'});return structuredClone(history)
  }
  return original(path,options)
 })
 const v=view(true);await flushPromises();await mode(v,'premorbid');await v.get('[data-history-model-filter]').setValue('model:llama3.1:8b');await send(v,'第五份作答')
 expect(v.get('[data-history-model-filter]').element.value).toBe('');expect(v.get('[data-action="latest-result"]').text()).toContain('#5')
 await v.get('[data-workspace-tab="view"]').trigger('click');await v.get('[data-history-model-filter]').setValue('model:llama3.1:8b');expect(v.get('.chat-log').text()).toContain('Llama 新回饋')
 await v.get('[data-action="latest-result"]').trigger('click');await flushPromises();expect(v.get('.chat-log').text()).toContain('新模型完成回饋');expect(v.get('.round-heading h3').text()).toBe('#5');expect(v.get('[data-history-model-filter]').element.value).toBe('')
})
it('shows no unrelated feedback when a selected model has only trashed attempts',async()=>{
 history=mixedModelHistory();history.sessions[1].messages.forEach(m=>{if(m.id==='mixed-2'||m.replyTo==='mixed-2')m.trashedAt='2026-11-23T06:00:00Z'})
 const v=view();await flushPromises();await mode(v,'premorbid');await v.get('[data-history-model-filter]').setValue('model:retired-model:7b')
 expect(v.findAll('[data-round]')).toHaveLength(0);expect(v.get('.round-sidebar').text()).toContain('此模型尚無作答紀錄');expect(v.findAll('.chat-log .chat-message')).toHaveLength(0)
 await v.get('[data-action="toggle-trash"]').trigger('click');expect(v.get('.chat-log').text()).toContain('歷史模型回饋');expect(v.find('[data-action="restore-round"]').exists()).toBe(true)
})
it.each(['teacher','ta','student'])('shows phase writing guidance for the actual teaching role without changing draft or requests (%s)',async role=>{
 const v=view(true,role);await flushPromises();await mode(v,'premorbid')
 const guide=v.get('[data-stage-guidance]');expect(guide.attributes('data-guidance-audience')).toBe(role==='student'?'student':'teacher')
 expect(guide.text()).toContain('個案生病以前是什麼樣子？');expect(guide.text()).toContain('利手（慣用手）');expect(guide.text()).toContain('尚待與原始教材核對')
 const calls=api.mock.calls.length;await v.get('textarea').setValue('尚未提交的作答');await guide.get('summary').trigger('click');expect(v.get('textarea').element.value).toBe('尚未提交的作答');expect(api.mock.calls.length).toBe(calls)
 if(role!=='student'){
  expect(guide.findAll('[data-guide-phase]')).toHaveLength(4);expect(guide.find('.phase-comparison').exists()).toBe(false)
  expect(guide.findAll('.phase-core-question')).toHaveLength(4)
  for(const phase of guide.findAll('[data-guide-phase]')){expect(phase.get('summary .phase-core-question').text()).toMatch(/？$/);expect(guide.text().split(phase.get('.phase-core-question').text())).toHaveLength(2)}
 }else expect(guide.findAll('.phase-comparison tbody tr')).toHaveLength(4)
 await mode(v,'functionalAssessment');expect(v.get('[data-stage-guidance]').text()).toContain('個案現在的功能如何？')
 if(role==='student')expect(v.get('[data-stage-guidance]').findAll('[data-guide-phase]')).toHaveLength(1)
 if(role==='teacher'){experience.student=true;await flushPromises();expect(v.find('[data-guidance-audience="teacher"]').exists()).toBe(false)}
})
it.each(['teacher','student'])('offers current course category definitions below the composer without changing drafts or requests (%s)',async role=>{
 config.classifications=[{code:'A1f',phase:'A',label:'利手'},{code:'A1a',phase:'A',label:'年齡',description:'課內定義 <img src=x>'},{code:'A1b',phase:'A',label:'重新命名的分類'},{code:'B6',phase:'B',label:'診斷'},{code:'C2',phase:'C',label:'Muscle Power（肌力與相關動作檢查）',enabled:false}]
 const v=view(true,role);await flushPromises();if(role==='teacher')expect(v.find('[data-writing-category-reference]').exists()).toBe(false)
 await mode(v,'premorbid');await v.get('[data-workspace-tab="write"]').trigger('click')
 const reference=v.get('[data-writing-category-reference]');expect(reference.isVisible()).toBe(true);expect(Boolean(v.get('form').element.compareDocumentPosition(reference.element)&Node.DOCUMENT_POSITION_FOLLOWING)).toBe(true)
 expect(reference.get('[data-reference-phase="A"]').attributes('open')).toBeDefined();expect(reference.get('[data-reference-phase="B"]').attributes('open')).toBeUndefined();expect(reference.get('[data-reference-code="A1f"]').text()).toBe('A1f — 利手');expect(reference.text()).not.toContain('慣用手（handedness）');expect(document.querySelector('[data-writing-definition-tip]')).toBeNull();expect(reference.find('[data-reference-code="C2"]').exists(),'disabled rules must be omitted').toBe(false)
 await reference.get('[data-reference-code="A1f"]').trigger('mouseenter');let tip=document.querySelector('[data-writing-definition-tip]');expect(tip.textContent).toContain('慣用手（handedness）');expect(reference.get('[data-reference-code="A1f"]').attributes('aria-describedby')).toBe(tip.id)
 reference.get('[data-reference-code="A1a"]').element.focus();await flushPromises();tip=document.querySelector('[data-writing-definition-tip]');expect(tip.textContent).toContain('課內定義 <img src=x>');expect(tip.querySelector('img')).toBeNull()
 await reference.get('[data-reference-code="A1a"]').trigger('click');await flushPromises();expect(document.activeElement).toBe(tip);document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));await flushPromises();expect(document.querySelector('[data-writing-definition-tip]')).toBeNull();expect(document.activeElement).toBe(reference.get('[data-reference-code="A1a"]').element)
 reference.get('[data-reference-code="A1b"]').element.focus();await flushPromises();expect(document.querySelector('[data-writing-definition-tip]').textContent).toContain('此分類尚未提供詳細定義。');document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));await flushPromises();expect(document.querySelector('[data-writing-definition-tip]')).toBeNull()
 await v.get('textarea').setValue('正在撰寫的原稿');const requests=api.mock.calls.length;await reference.get('[data-reference-phase="B"] summary').trigger('click');expect(v.get('textarea').element.value).toBe('正在撰寫的原稿');expect(api.mock.calls.length).toBe(requests)
 await v.get('[data-workspace-tab="view"]').trigger('click');expect(reference.isVisible(),'reference hidden in feedback').toBe(false);expect(document.querySelector('[data-writing-definition-tip]')).toBeNull();await v.get('[data-workspace-tab="write"]').trigger('click');expect(reference.isVisible()).toBe(true);expect(v.get('textarea').element.value).toBe('正在撰寫的原稿');expect(api.mock.calls.length).toBe(requests)
 await mode(v,'illnessCourse');expect(v.get('[data-writing-category-reference] [data-reference-phase="B"]').attributes('open')).toBeDefined()
})
it('places writing and feedback above their phase tabs and keeps Raw as an isolated top-level workspace',async()=>{
 const v=view(true);await flushPromises()
 const primary=v.get('[data-workspace-navigation]')
 expect(primary.findAll('[role="tab"]').map(item=>item.text())).toEqual(['作答','回饋','AI 對話測試'])
 expect(v.find('[data-phase-navigation]').exists()).toBe(false)
 await primary.get('[data-workspace-tab="write"]').trigger('click');await flushPromises()
 expect(v.get('[data-phase-navigation]').findAll('[role="tab"]').map(item=>item.text())).toEqual(stages.map(item=>item.label))
 expect(v.get('[data-workspace-tab="write"]').attributes('aria-selected')).toBe('true');expect(v.get('form').isVisible()).toBe(true)
 await v.get('textarea').setValue('保留同階段草稿');const requests=api.mock.calls.length
 await v.get('[data-workspace-tab="view"]').trigger('click');expect(v.get('textarea').element.value).toBe('保留同階段草稿');expect(api.mock.calls.length).toBe(requests)
 window.confirm.mockReturnValueOnce(false);await v.get('[data-mode="raw"]').trigger('click');await flushPromises();expect(v.get('[data-workspace-tab="view"]').attributes('aria-selected')).toBe('true')
 await v.get('textarea').setValue('');await v.get('[data-mode="illnessCourse"]').trigger('click');await flushPromises();expect(v.get('[data-workspace-tab="view"]').attributes('aria-selected')).toBe('true')
 await v.get('[data-mode="raw"]').trigger('click');await flushPromises();expect(v.find('[data-phase-navigation]').exists()).toBe(false)
 await v.get('[data-workspace-tab="view"]').trigger('click');await flushPromises();expect(v.get('[data-mode="illnessCourse"]').attributes('aria-selected')).toBe('true');expect(v.get('[data-workspace-tab="view"]').attributes('aria-selected')).toBe('true')
})
it('separates writing and feedback tabs while preserving drafts and existing formal state',async()=>{
 history=savedRounds();const v=view(true,'student');await flushPromises();await mode(v,'premorbid')
 const detail=v.get('.round-detail');expect(detail.find('form').exists()).toBe(true)
 const writing=v.get('[data-workspace-tab="write"]'),reading=v.get('[data-workspace-tab="view"]')
 expect(reading.attributes('aria-selected')).toBe('true');expect(detail.get('form').isVisible()).toBe(false);expect(v.get('.chat-log').isVisible()).toBe(true)
 const requests=api.mock.calls.length,formal=v.getComponent(CaseFormalSubmissions).vm.$
 await writing.trigger('click');expect(detail.get('form').isVisible()).toBe(true);expect(v.get('.chat-log').isVisible()).toBe(false);expect(v.get('.feedback-rail').isVisible()).toBe(false);expect(v.get('.category-sidebar').isVisible()).toBe(false);expect(v.getComponent(CaseFormalSubmissions).isVisible()).toBe(false)
 await v.get('textarea').setValue('保留目前草稿')
 await reading.trigger('click');expect(v.get('textarea').element.value).toBe('保留目前草稿');expect(v.get('.chat-log').isVisible()).toBe(true);expect(v.getComponent(CaseFormalSubmissions).vm.$).toBe(formal)
 await reading.trigger('keydown',{key:'ArrowLeft'});expect(reading.attributes('aria-selected')).toBe('true');await writing.trigger('click');expect(writing.attributes('aria-selected')).toBe('true');expect(document.activeElement).toBe(writing.element);expect(v.get('textarea').element.value).toBe('保留目前草稿');expect(api.mock.calls.length).toBe(requests)
 expect(v.get('form').text()).toContain('A 病前功能');expect(v.get('form').text()).toContain('取得 AI 分析')
 expect(v.get('[data-round="u3"] strong').text()).toBe('#3')
 expect(v.get('[data-round="u3"] small').text()).toBe(new Date('2026-11-20T03:00:00Z').toLocaleString('zh-TW'))
 expect(v.find('[role="combobox"][aria-label="分析版本"]').exists()).toBe(false)
 expect(v.find('.feedback-rail .category-sidebar').exists()).toBe(false);expect(detail.find('.category-sidebar').exists()).toBe(true)
 expect(v.find('[data-action="performance"]').exists()).toBe(false);expect(v.find('[data-current-model]').exists()).toBe(false)
 expect(v.find('[data-action="submit-formal"]').exists()).toBe(true)
})
it('starts an empty stage in writing and supports an empty feedback view without losing a draft',async()=>{
 const v=view(true,'student');await flushPromises()
 expect(v.get('[data-workspace-tab="write"]').attributes('aria-selected')).toBe('true');expect(v.get('form').isVisible()).toBe(true)
 await v.get('textarea').setValue('尚未分析的原稿');await v.get('[data-workspace-tab="view"]').trigger('click')
 expect(v.get('form').isVisible()).toBe(false);expect(v.get('.chat-log').isVisible()).toBe(true);expect(v.get('.empty-inline').text()).toContain('完成 AI 分析')
 await v.get('[data-workspace-tab="view"]').trigger('keydown',{key:'Home'});await v.get('[data-workspace-tab="write"]').trigger('click');expect(v.get('textarea').element.value).toBe('尚未分析的原稿');expect(v.get('form').isVisible()).toBe(true)
})
it('starts actual students in phase A and hides the staff testing chat even with permissive flags',async()=>{
 config.canUseTesting=true;config.canChooseModel=true;const v=view(false,'student');await flushPromises()
 expect(v.find('[data-mode="raw"]').exists()).toBe(false);expect(v.get('[data-mode="premorbid"]').attributes('aria-selected')).toBe('true');expect(v.get('[data-mode="premorbid"]').attributes('tabindex')).toBe('0')
 expect(api.mock.calls.some(([p])=>p.includes('/raw/'))).toBe(false);expect(v.find('[data-model-select]').exists()).toBe(false)
})
it('numbers attempts by recorded date across sessions while retaining immutable formal submission sources',async()=>{
 history={activeSessionId:'middle',sessions:[
  {id:'old',status:'closed',messages:[{id:'old-u',role:'user',content:'舊作答',status:'completed',createdAt:'2026-11-18T01:00:00Z'}]},
  {id:'new',status:'closed',messages:[{id:'new-u',role:'user',content:'新分析尚未正式提交',status:'completed',createdAt:'2026-11-20T01:00:00Z'}]},
  {id:'middle',status:'active',messages:[{id:'middle-u',role:'user',content:'目前作答',status:'completed',createdAt:'2026-11-19T01:00:00Z'}]}
 ]}
 const original=api.getMockImplementation();api.mockImplementation((p,o)=>p===base+'/submissions'?Promise.resolve({submissions:[{id:'formal-old',studentId:'student',stage:'premorbid',version:7,status:'submitted',analysisMessageId:'old-u',answer:'舊作答',feedback:'回饋',annotations:[],submittedAt:'2026-11-18T02:00:00Z'}],assignments:[]}):original(p,o))
 const v=view(false,'student');await flushPromises();const menu=v.get('[data-history-model-filter]')
 expect(menu.findAll('option').map(option=>option.text())).toEqual(['全部模型','未記錄模型']);expect(menu.element.value).toBe('')
 expect(v.findAll('[data-round]').map(item=>item.attributes('data-round'))).toEqual(['new-u','middle-u','old-u']);expect(v.findAll('[data-round] strong').map(item=>item.text())).toEqual(['#3','#2','#1'])
 expect(history.sessions.map(item=>item.id)).toEqual(['old','new','middle']);expect(v.getComponent(CaseFormalSubmissions).props('selectedRound').id).toBe('new-u')
 await chooseRound(v,'old-u');expect(v.getComponent(CaseFormalSubmissions).props('selectedRound').id).toBe('old-u');expect(v.find('[data-remove-round="old-u"]').exists()).toBe(false)
 expect(v.text()).toContain('正式第 7 版')
})
it.each(['teacher','ta','student'])('shows course rules maintenance only to an explicitly authorized course teacher (%s)',async role=>{
 config.canMaintainRules=true;config.canReview=role!=='student';const v=mount(CaseAnalysisChat,{props:{course:{id:'c',courseRole:role}}});wrappers.push(v);await flushPromises();await mode(v,'premorbid')
 expect(v.find('[data-case-rules-editor]').exists()).toBe(role==='teacher')
})
const capture=text=>({promptVersion:'captured-v3',requests:[{purpose:'feedback',url:'http://model.test/api/chat',payload:{model:'captured-model',messages:[{role:'system',content:'實際系統提示'},{role:'user',content:text}],format:{type:'object'},options:{temperature:0.2}}}]})
function trashFixture(){
 history=savedRounds()
 const original=api.getMockImplementation()
 api.mockImplementation(async(p,o)=>{
  const match=p.match(/\/messages\/([^/]+)\/(trash|restore)$/)
  if(match){const [,id,action]=match;for(const s of history.sessions)for(const m of s.messages)if(m.id===id||m.replyTo===id)m.trashedAt=action==='trash'?'2026-11-19T00:00:00Z':null;return structuredClone(history)}
  return original(p,o)
 })
}
it('confirms the targeted card X without selecting it or losing the current draft',async()=>{
 trashFixture();const v=view();await flushPromises();await mode(v,'premorbid');await v.get('textarea').setValue('保留未送草稿')
 window.confirm.mockReturnValueOnce(false);await v.get('[data-remove-round="u1"]').trigger('click');expect(api.mock.calls.some(([p])=>p.endsWith('/trash'))).toBe(false)
 expect(v.get('[data-round="u3"]').attributes('aria-current')).toBe('true')
 await v.get('[data-remove-round="u1"]').trigger('click');await flushPromises();expect(api.mock.calls.filter(([p])=>p.endsWith('/trash')).map(([p])=>p)).toEqual([base+'/stages/premorbid/messages/u1/trash'])
 expect(v.get('[data-round="u3"]').attributes('aria-current')).toBe('true');expect(v.get('textarea').element.value).toBe('保留未送草稿');expect(v.find('[data-round="u1"]').exists()).toBe(false)
})
it('protects submitted and returned source cards and waits for formal metadata before offering removal',async()=>{
 trashFixture();let finish;const original=api.getMockImplementation();api.mockImplementation((p,o)=>p===base+'/submissions'?new Promise(resolve=>{finish=resolve}):original(p,o))
 const v=view(false,'student');await flushPromises();await mode(v,'premorbid');expect(v.find('[data-remove-round]').exists()).toBe(false)
 finish({submissions:[{id:'f1',answer:'First submitted answer',feedback:'Feedback',analysisMessageId:'u1',stage:'premorbid',status:'returned',version:1},{id:'f2',answer:'Second submitted answer',feedback:'Feedback',analysisMessageId:'u2',stage:'premorbid',status:'submitted',version:2}],assignments:[]});await flushPromises()
 expect(v.find('[data-remove-round="u1"]').exists()).toBe(false);expect(v.find('[data-remove-round="u2"]').exists()).toBe(false);expect(v.find('[data-remove-round="u3"]').exists()).toBe(true)
 await chooseRound(v,'u1');expect(v.find('[data-action="trash-round"]').exists()).toBe(false);await chooseRound(v,'u2');expect(v.find('[data-action="trash-round"]').exists()).toBe(false)
})
it('filters trash from normal rounds and comparisons while keeping original numbering and revision ancestry',async()=>{
 trashFixture();history.sessions[0].messages.filter(m=>m.id==='u1'||m.replyTo==='u1').forEach(m=>{m.trashedAt='2026-11-19T00:00:00Z'})
 const v=view();await flushPromises();await mode(v,'premorbid')
 expect(v.findAll('[data-round]').map(b=>b.attributes('data-round'))).toEqual(['u3','u2']);expect(v.get('[data-round="u3"]').text()).toContain('#3')
 expect(v.findComponent({name:'AnswerRevisionDiff'}).props('revisions').map(r=>r.id)).toEqual(['u2','u3'])
 await v.get('textarea').setValue('保留的草稿');await v.get('[data-action="toggle-trash"]').trigger('click')
 expect(v.get('.chat-log').text()).toContain('第一輪回饋');expect(v.find('textarea').exists()).toBe(false);expect(v.find('[data-action="revise-answer"]').exists()).toBe(false);expect(v.find('[data-action="retry-message"]').exists()).toBe(false)
 await v.get('[data-action="toggle-trash"]').trigger('click');expect(v.get('textarea').element.value).toBe('保留的草稿')
})
it.each([false,true])('confirms trash of an owned round and restores the complete pair without changing formal statistics (staff=%s)',async staff=>{
 config.canReview=staff;trashFixture();const v=view();await flushPromises();await mode(v,'premorbid');await chooseRound(v,'u2')
 window.confirm.mockReturnValueOnce(false);await selectedRoundRemove(v).trigger('click');expect(api.mock.calls.some(([p])=>p.endsWith('/trash'))).toBe(false)
 await selectedRoundRemove(v).trigger('click');await flushPromises();expect(window.confirm).toHaveBeenLastCalledWith(expect.stringContaining('AI 回饋'));expect(v.get('.chat-log').text()).toContain('第三輪回饋');expect(v.find('[data-round="u2"]').exists()).toBe(false)
 expect(v.text()).toContain('正式學習統計不變');expect(v.get('[data-action="toggle-trash"]').text()).toContain('1')
 await v.get('[data-action="toggle-trash"]').trigger('click');expect(v.get('.chat-log').text()).toContain('第二輪回饋');await v.get('[data-action="restore-round"]').trigger('click');await flushPromises()
 expect(api.mock.calls.find(([p])=>p.endsWith('/u2/restore'))[1]).toMatchObject({method:'POST',signal:expect.any(AbortSignal)});expect(v.get('[data-action="toggle-trash"]').attributes('aria-pressed')).toBe('false');expect(v.get('.chat-log').text()).toContain('第二輪回饋');expect(v.findAll('[data-round]')).toHaveLength(3)
})
it('retains draft text but clears a trashed revision source and ambiguous request identity',async()=>{
 trashFixture();const v=view();await flushPromises();await mode(v,'premorbid');await chooseRound(v,'u1');await v.get('[data-action="revise-answer"]').trigger('click');await v.get('textarea').setValue('繼續保留的修改草稿')
 api.mockRejectedValueOnce(new Error('ambiguous failure'));await v.get('form').trigger('submit');await flushPromises();const previous=api.mock.calls.filter(([p,o])=>p.endsWith('/premorbid/messages')&&o?.method==='POST').at(-1)[1].body
 await chooseRound(v,'u1');await selectedRoundRemove(v).trigger('click');await flushPromises();expect(v.get('textarea').element.value).toBe('繼續保留的修改草稿');expect(v.find('.revision-notice').exists()).toBe(false)
 await v.get('form').trigger('submit');await flushPromises();const next=api.mock.calls.filter(([p,o])=>p.endsWith('/premorbid/messages')&&o?.method==='POST').at(-1)[1].body;expect(next.revisesMessageId).toBeNull();expect(next.requestId).not.toBe(previous.requestId)
})
it('manages owned closed sessions without jumping to the active session',async()=>{
 trashFixture();history.sessions[0].status='closed';history.sessions.push({id:'active',status:'active',messages:[]});history.activeSessionId='active';const v=view();await flushPromises();await mode(v,'premorbid')
 expect(v.find('textarea').exists()).toBe(true);await selectedRoundRemove(v).trigger('click');await flushPromises();expect(v.get('.chat-log').text()).toContain('第二輪回饋')
 await v.get('[data-action="toggle-trash"]').trigger('click');await v.get('[data-action="restore-round"]').trigger('click');await flushPromises();expect(v.get('.chat-log').text()).toContain('第三輪回饋');expect(history.activeSessionId).toBe('active')
})
it('clears moved-round result links and request snapshots without retaining stale diagnostics after restore',async()=>{
 config.canReview=true;config.canInspectRequests=true;trashFixture();const original=api.getMockImplementation()
 api.mockImplementation(async(p,o)=>{
  if(p.endsWith('/premorbid/messages')&&o?.method==='POST'){const id=o.body.requestId;history.sessions[0].messages.push({id,role:'user',content:o.body.content,status:'completed'},{id:'reply-'+id,replyTo:id,role:'assistant',content:'new feedback'});return {...structuredClone(history),requestInspection:capture(o.body.content)}}
  return original(p,o)
 })
 const v=view();await flushPromises();await mode(v,'premorbid');await send(v,'new captured submission');await v.get('[data-action="request-inspection"]').trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toEqual(capture('new captured submission'))
 v.getComponent(ChatPerformanceDialog).vm.$emit('close');await selectedRoundRemove(v).trigger('click');await flushPromises();expect(v.find('[data-action="latest-result"]').exists()).toBe(false);expect(v.getComponent(ChatPerformanceDialog).props('open')).toBe(false)
 await v.get('[data-action="toggle-trash"]').trigger('click');await v.get('[data-action="restore-round"]').trigger('click');await flushPromises();await v.get('[data-action="request-inspection"]').trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toBeNull()
})
it('preserves the selected round and draft on management failure and excludes trashed failures from retries',async()=>{
 trashFixture();history.sessions[0].messages.find(m=>m.id==='u3').status='failed';const v=view();await flushPromises();await mode(v,'premorbid');await v.get('textarea').setValue('第三份修改作答')
 api.mockRejectedValueOnce(Object.assign(new Error('移至垃圾桶失敗'),{status:409}));await selectedRoundRemove(v).trigger('click');await flushPromises();expect(v.get('[role="alert"]').text()).toContain('移至垃圾桶失敗');expect(v.get('textarea').element.value).toBe('第三份修改作答');expect(v.get('[data-round="u3"]').attributes('aria-current')).toBe('true')
 await selectedRoundRemove(v).trigger('click');await flushPromises();expect(v.find('[data-action="retry-message"]').exists()).toBe(false);expect(v.get('form').text()).not.toContain('重試這則訊息');await v.get('form').trigger('submit');await flushPromises();expect(api.mock.calls.filter(([p])=>p.endsWith('/u3/retry'))).toHaveLength(0);expect(api.mock.calls.filter(([p,o])=>p.endsWith('/premorbid/messages')&&o?.method==='POST')).toHaveLength(1)
})
it('disables trash on pending rounds and while a trash operation is running, and ignores stale completion after a stage change',async()=>{
 trashFixture();history.sessions[0].messages.find(m=>m.id==='u3').status='pending';const v=view();await flushPromises();await mode(v,'premorbid');expect(selectedRoundRemove(v).attributes('disabled')).toBeDefined()
 await chooseRound(v,'u2');let finish;api.mockImplementationOnce(()=>new Promise(resolve=>{finish=resolve}));await selectedRoundRemove(v).trigger('click');expect(selectedRoundRemove(v).attributes('disabled')).toBeDefined();expect(v.get('[data-action="toggle-trash"]').attributes('disabled')).toBeDefined()
 history=empty();await mode(v,'illnessCourse');finish(savedRounds());await flushPromises();expect(v.findAll('[data-round]')).toHaveLength(0);expect(v.find('[data-reply-loading]').exists()).toBe(false)
})
it.each(['disabled','preview'])('keeps trash management unavailable for %s while allowing read-only trash browsing',async kind=>{
 config.canReview=true;trashFixture();history.sessions[0].messages.filter(m=>m.id==='u1'||m.replyTo==='u1').forEach(m=>{m.trashedAt='2026-11-19T00:00:00Z'})
 if(kind==='disabled')config.canChat=false;if(kind==='preview')experience.student=true
 const original=api.getMockImplementation();api.mockImplementation(async(p,o)=>p===base+'/records'?{students:[{id:'s1',displayName:'Synthetic'}]}:p.includes('/records/s1/')?history:original(p,o))
 const v=view();await flushPromises();if(kind==='review'){await v.get('[data-action="review"]').trigger('click');await flushPromises();await v.get('[data-student="s1"]').trigger('click');await flushPromises()}else await mode(v,'premorbid')
 expect(v.find('[data-action="trash-round"]').exists()).toBe(false);await v.get('[data-action="toggle-trash"]').trigger('click');expect(v.get('.chat-log').text()).toContain('第一輪回饋');expect(v.find('[data-action="restore-round"]').exists()).toBe(false)
})
const modelProfiles=[{key:'llama3',label:'IAN02 Llama3',modelName:'llama3.1:8b'},{key:'qwen',label:'IAN02 Qwen3',modelName:'qwen3:8b'},{key:'breeze',label:'Taipei Breeze',modelName:'ycchen/breeze-7b-instruct-v1_0'},{key:'qwen25-omni',label:'Taipei Qwen2.5 Omni',modelName:'rockn/Qwen2.5-Omni-7B-Q4_K_M:latest'}]
it('uses side diagnostic icons with model tooltips and hides the assistant model footer',async()=>{
 config.canReview=true;config.canInspectRequests=true;history=savedRounds()
 const v=view();await flushPromises();await mode(v,'premorbid')
 const performance=v.get('.from-assistant [data-action="performance"]'),request=v.get('.from-assistant [data-action="request-inspection"]')
 expect(performance.attributes('aria-label')).toBe('查看回覆效能');expect(request.attributes('aria-label')).toBe('查看 Ollama 請求')
 expect(performance.find('svg').exists()).toBe(true);expect(performance.text()).toBe('');expect(v.find('[data-message-model]').exists()).toBe(false)
 await performance.trigger('focus');expect(v.get('[role="tooltip"]').text()).toContain('saved-response-model');expect(performance.attributes('aria-describedby')).toBe(v.get('[role="tooltip"]').attributes('id'))
 await performance.trigger('click');expect(v.find('[role="tooltip"]').exists()).toBe(false);expect(v.getComponent(ChatPerformanceDialog).props('modelName')).toBe('saved-response-model')
})
function enableModelSelection(){config.canReview=true;config.canChooseModel=true;config.modelProfiles=modelProfiles;config.defaultModelProfile='qwen25-omni'}
it.each(['teacher','ta','student','preview','disabled'])('guards model choices by current course role and student POV (%s)',async role=>{
 enableModelSelection();if(role==='preview')experience.student=true;if(role==='disabled')config.canChooseModel=false
 const v=mount(CaseAnalysisChat,{props:{course:{id:'c',courseRole:role==='preview'||role==='disabled'?'teacher':role}}});wrappers.push(v);await flushPromises()
 const allowed=['teacher','ta'].includes(role);expect(v.find('[data-model-select]').exists()).toBe(allowed)
 if(allowed){expect(v.get('[data-model-select]').element.value).toBe('');expect(v.findAll('[data-model-select] option')).toHaveLength(5);expect(v.get('form [data-model-select]').exists()).toBe(true)}
 else expect(v.text()).not.toContain('IAN02 Llama3')
 const rawCreation=api.mock.calls.find(([p,o])=>p.endsWith('/raw/sessions')&&o?.method==='POST');if(['teacher','ta','disabled'].includes(role))expect(rawCreation[1].body).toBeUndefined();else expect(rawCreation).toBeUndefined()
})
it('keeps formal history and draft on model changes, resets ambiguous request identity and displays stored round models',async()=>{
 enableModelSelection();history={activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'old-u',role:'user',content:'saved original',status:'completed',modelName:'llama3.1:8b'},{id:'old-a',role:'assistant',replyTo:'old-u',content:'saved feedback',modelName:'llama3.1:8b'}]}]}
 const v=view();await flushPromises();await mode(v,'premorbid');await v.get('[data-model-select]').setValue('qwen');await send(v,'comparison answer')
 const first=api.mock.calls.filter(([p,o])=>p.endsWith('/premorbid/messages')&&o?.method==='POST').at(-1)[1].body
 expect(first.modelProfile).toBe('qwen');expect(v.get('[data-current-model]').text()).toContain('qwen3:8b');expect(v.get('[data-round="old-u"]').text()).not.toContain('llama3.1:8b');expect(v.get('.from-assistant [data-reply-model]').attributes('data-reply-model')).toBe('llama3.1:8b')
 api.mockRejectedValueOnce(new TypeError('ambiguous failure'));await send(v,'same draft');const ambiguous=api.mock.calls.filter(([p,o])=>p.endsWith('/premorbid/messages')&&o?.method==='POST').at(-1)[1].body
 const callsBefore=api.mock.calls.length;await v.get('[data-model-select]').setValue('llama3');await flushPromises();expect(api.mock.calls).toHaveLength(callsBefore);expect(v.get('textarea').element.value).toBe('same draft');expect(v.text()).toContain('saved feedback')
 await v.get('form').trigger('submit');await flushPromises();const changed=api.mock.calls.filter(([p,o])=>p.endsWith('/premorbid/messages')&&o?.method==='POST').at(-1)[1].body;expect(changed.requestId).not.toBe(ambiguous.requestId);expect(changed.modelProfile).toBe('llama3')
 await v.get('[data-model-select]').setValue('');await send(v,'default answer');expect(api.mock.calls.filter(([p,o])=>p.endsWith('/premorbid/messages')&&o?.method==='POST').at(-1)[1].body).not.toHaveProperty('modelProfile')
})
it('retries a saved failure with its original model and submits a new comparison under the selected model',async()=>{
 enableModelSelection();history={activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'failed-u',role:'user',content:'failed answer',status:'failed',modelName:'llama3.1:8b'}]}]}
 const v=view();await flushPromises();await mode(v,'premorbid');await v.get('[data-model-select]').setValue('qwen');await v.get('[data-action="retry-message"]').trigger('click');await flushPromises()
 expect(api.mock.calls.find(([p])=>p.endsWith('/failed-u/retry'))[1].body).toEqual({modelProfile:'llama3'})
 await send(v,'failed answer');expect(api.mock.calls.filter(([p,o])=>p.endsWith('/premorbid/messages')&&o?.method==='POST').at(-1)[1].body.modelProfile).toBe('qwen')
})
it('chooses Raw models per message while retaining the same session, draft and earlier replies',async()=>{
 enableModelSelection();const v=view();await flushPromises();await send(v,'Raw original');await v.get('textarea').setValue('Raw draft')
 await v.get('[data-model-select]').setValue('qwen');await flushPromises();expect(v.get('textarea').element.value).toBe('Raw draft');expect(v.text()).toContain('Raw original');expect(rawId).toBe(1);expect(window.confirm).not.toHaveBeenCalled()
 await send(v,'new Raw');expect(api.mock.calls.filter(([p])=>p.endsWith('/raw-1/messages')).at(-1)[1].body).toEqual({content:'new Raw',modelProfile:'qwen'})
 await v.get('[data-model-select]').setValue('llama3');await send(v,'third Raw');expect(rawId).toBe(1);expect(api.mock.calls.filter(([p])=>p.endsWith('/raw-1/messages')).at(-1)[1].body).toEqual({content:'third Raw',modelProfile:'llama3'})
})
it('labels Raw replies with the actual session response model when messages omit their own model metadata',async()=>{
 enableModelSelection();const v=view();await flushPromises();await v.get('[data-model-select]').setValue('qwen');await flushPromises()
 api.mockResolvedValueOnce({sessionId:'raw-2',modelName:'actual-session-model',messages:[{role:'user',content:'test'},{role:'assistant',content:'actual reply'}]});await send(v,'test')
 expect(v.get('.from-assistant [data-reply-model]').attributes('data-reply-model')).toBe('actual-session-model');await v.get('[data-action="performance"]').trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('modelName')).toBe('actual-session-model')
})
it('ignores model selection while inference is pending and clears it on role or POV changes',async()=>{
 enableModelSelection();const v=view();await flushPromises();await mode(v,'premorbid');await v.get('[data-model-select]').setValue('qwen');let finish
 api.mockImplementationOnce(()=>new Promise(resolve=>{finish=resolve}));await v.get('textarea').setValue('pending');await v.get('form').trigger('submit');expect(v.get('[data-model-select]').attributes('disabled')).toBeDefined()
 const select=v.get('[data-model-select]').element;select.value='llama3';select.dispatchEvent(new Event('change'));expect(select.value).toBe('qwen');finish(empty());await flushPromises()
 experience.student=true;await flushPromises();expect(v.find('[data-model-select]').exists()).toBe(false);expect(v.find('[data-current-model]').exists()).toBe(false);experience.student=false;await flushPromises();expect(v.find('[data-model-select]').exists()).toBe(false)
})
it('uses keyboard-focusable hierarchical tabs and preserves Raw departure confirmation',async()=>{
 const v=view(true);await flushPromises();const list=v.get('[data-workspace-navigation]'),tabs=list.findAll('[role="tab"]');expect(tabs).toHaveLength(3);expect(tabs[2].attributes('aria-selected')).toBe('true');expect(v.get('[role="tabpanel"]').attributes('aria-labelledby')).toBe(tabs[2].attributes('id'))
 await tabs[2].trigger('keydown',{key:'ArrowRight'});expect(document.activeElement).toBe(tabs[0].element);expect(tabs[0].attributes('aria-selected')).toBe('false');expect(rawId).toBe(1)
 await v.get('textarea').setValue('未儲存');window.confirm.mockReturnValue(false);await tabs[0].trigger('click');await flushPromises();expect(tabs[2].attributes('aria-selected')).toBe('true');expect(v.get('textarea').element.value).toBe('未儲存')
 window.confirm.mockReturnValue(true);await tabs[0].trigger('click');await flushPromises();expect(tabs[0].attributes('aria-selected')).toBe('true');expect(v.get('[role="tabpanel"]').attributes('id')).toBe(tabs[0].attributes('aria-controls'))
 const phases=v.get('[data-phase-navigation]').findAll('[role="tab"]');await phases[0].trigger('keydown',{key:'ArrowRight'});expect(document.activeElement).toBe(phases[1].element);expect(phases[0].attributes('aria-selected')).toBe('true')
})
it.each([true,false])('shows safe failure diagnostics and a failed-request dialog only for current course staff (%s)',async staff=>{
 config.canReview=staff;config.canInspectRequests=staff;const v=view();await flushPromises();await mode(v,'illnessCourse');
 api.mockRejectedValueOnce(Object.assign(new Error('模型暫時無法回覆'),{status:502,diagnostic:{code:'provider_http',reason:'模型服務回傳 HTTP 503。',upstreamStatus:503},requestInspection:capture('合成失敗作答')}));await send(v,'合成失敗作答')
 expect(v.find('[data-failure-diagnostic]').exists()).toBe(staff);expect(v.find('[data-action="failed-request"]').exists()).toBe(staff);expect(v.get('textarea').element.value).toBe('合成失敗作答')
 if(staff){await v.get('[data-action="failed-request"]').trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toEqual(capture('合成失敗作答'));expect(v.getComponent(ChatPerformanceDialog).props('modelName')).toBe('captured-model');experience.student=true;await flushPromises();expect(v.find('[data-failure-diagnostic]').exists()).toBe(false)}
})
it('places model setup errors below the raw compose field instead of the page header',async()=>{
 const fallback=api.getMockImplementation();api.mockImplementation(async(p,o)=>{if(p===base+'/raw/sessions')throw new Error('模型暫時無法回應');return fallback(p,o)})
 const v=view();await flushPromises();const notice=v.get('form [data-chat-error="composer"]');expect(notice.attributes('role')).toBe('alert');expect(notice.text()).toContain('模型暫時無法回應');expect(v.find('[data-chat-error="page"]').exists()).toBe(false);expect(v.get('textarea').element.compareDocumentPosition(notice.element)&Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
})
it('keeps a failed history refresh below writing after a model failure without losing the draft',async()=>{
 const v=view();await flushPromises();await mode(v,'premorbid');const fallback=api.getMockImplementation();api.mockImplementation(async(p,o)=>{if(p.includes('/premorbid/messages'))throw new Error('模型暫時無法回應');if(p.includes('/stages/premorbid'))throw new Error('歷程暫時無法載入');return fallback(p,o)})
 await send(v,'保留測試作答');expect(v.get('form [data-reply-failed]').text()).toContain('模型暫時無法回應');expect(v.get('form [data-chat-error="composer"]').text()).toContain('歷程暫時無法載入');expect(v.find('[data-chat-error="page"]').exists()).toBe(false);expect(v.get('textarea').element.value).toBe('保留測試作答')
})
it('keeps output failures and same-attempt retry in the composer card instead of the page header',async()=>{
 config.canInspectRequests=true;const v=view();await flushPromises();await mode(v,'premorbid');const fallback=api.getMockImplementation();let failedId;
 api.mockImplementation(async(p,o)=>{
  if(p.endsWith('/premorbid/messages')&&o?.method==='POST'){failedId=o.body.requestId;history={activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:failedId,role:'user',content:o.body.content,status:'failed'}]}]};throw Object.assign(new Error('模型回覆達到輸出上限，原作答已保留。'),{status:502,diagnostic:{code:'output_limit',reason:'done_reason=length'},requestInspection:capture('合成中斷作答')})}
  if(p.endsWith(`/${failedId}/retry`)){history.sessions[0].messages[0].status='completed';history.sessions[0].messages.push({id:'retried-reply',role:'assistant',replyTo:failedId,content:'合成完整回覆'});return structuredClone(history)}
  return fallback(p,o)
 })
 await send(v,'  合成中斷作答\n');const card=v.get('form [data-reply-failed]');expect(card.text()).toContain('output_limit');expect(card.get('[data-action="failed-request"]').text()).toContain('查看失敗請求');expect(v.find('.error-box').exists()).toBe(false);expect(v.get('textarea').element.value).toBe('  合成中斷作答\n')
 await card.get('[data-action="retry-reply"]').trigger('click');await flushPromises();expect(api.mock.calls.filter(([p,o])=>p.endsWith(`/${failedId}/retry`)&&o?.method==='POST')).toHaveLength(1);expect(v.find('[data-reply-failed]').exists()).toBe(false);expect(v.find('[data-reply-completed]').exists()).toBe(true);expect(v.get('textarea').element.value).toBe('')
})
it.each(['raw','premorbid'])('shows a reply placeholder while %s inference is pending and clears it on success or failure',async(key)=>{
 const v=view();await flushPromises();if(key!=='raw')await mode(v,key)
 let finish;api.mockImplementationOnce(()=>new Promise(resolve=>{finish=resolve}))
 await v.get('textarea').setValue('pending answer');await v.get('form').trigger('submit')
 if(key!=='raw'){expect(v.get('[data-workspace-tab="view"]').element.disabled).toBe(true);await v.get('[data-workspace-tab="view"]').trigger('click');expect(v.get('[data-workspace-tab="write"]').attributes('aria-selected')).toBe('true')}
 expect(v.get('form [data-reply-loading]').attributes('role')).toBe('status');expect(v.get('[data-reply-loading]').text()).toContain(key==='raw'?'AI 助教正在回覆':'AI 助教處理中');expect(v.text()).not.toContain('尚無訊息。')
 expect(v.get('[data-current-request-hint]').attributes('data-current-request-hint')).toContain(key==='raw'?'已送出本次訊息':'A 病前功能')
 expect(v.get('[data-reply-loading]').text()).toContain('並非模型即時進度')
 const requestId=api.mock.calls.at(-1)[1].body?.requestId||'u',reply={id:'a',role:'assistant',replyTo:requestId,content:'completed response'}
 finish(key==='raw'?{sessionId:'raw-1',messages:[reply]}:{activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:requestId,role:'user',status:'completed',content:'pending answer'},reply]}]});await flushPromises()
 expect(v.find('[data-reply-loading]').exists()).toBe(false);expect(v.text()).toContain('completed response');expect(v.get('form [data-action="latest-result"]').text()).toContain('查看最新')
 if(key!=='raw'){expect(v.get('form').isVisible()).toBe(true);expect(v.get('.chat-log').isVisible()).toBe(false);expect(v.get('[data-workspace-tab="view"]').element.disabled).toBe(false)}
 let fail;api.mockImplementationOnce(()=>new Promise((_,reject)=>{fail=reject}))
 await v.get('textarea').setValue('retained on failure');await v.get('form').trigger('submit');expect(v.find('[data-reply-loading]').exists()).toBe(true)
 fail(new Error('模型暫時無法回覆'));await flushPromises();expect(v.find('[data-reply-loading]').exists()).toBe(false);expect(v.get('form [data-reply-failed]').text()).toContain('模型暫時無法回覆');expect(v.get('textarea').element.value).toBe('retained on failure');expect(v.find('[data-action="latest-result"]').exists()).toBe(false)
})
it.each([false,true])('links to the completed round from the editor and respects reduced motion (%s)',async reduced=>{
 const scroll=vi.fn();Object.defineProperty(HTMLElement.prototype,'scrollIntoView',{configurable:true,value:scroll});vi.spyOn(window,'matchMedia').mockReturnValue({matches:reduced,addEventListener(){},removeEventListener(){}})
 history=savedRounds();const v=view(true);await flushPromises();await mode(v,'premorbid');const original=api.getMockImplementation()
 api.mockImplementation(async(p,o)=>{
  if(p.endsWith('/premorbid/messages')&&o?.method==='POST'){const id=o.body.requestId;history.sessions[0].messages.push({id,role:'user',content:o.body.content,status:'completed'},{id:'new-result',role:'assistant',replyTo:id,content:'latest completed result'});return structuredClone(history)}
  return original(p,o)
 })
 await send(v,'new synthetic draft');await v.get('textarea').setValue('下一份未送出的草稿');await chooseRound(v,'u1');expect(v.get('.chat-log').text()).not.toContain('latest completed result')
 await v.get('[data-workspace-tab="write"]').trigger('click')
 await v.get('[data-action="latest-result"]').trigger('click');await flushPromises();expect(v.get('.chat-log').text()).toContain('latest completed result');expect(scroll).toHaveBeenCalledWith({behavior:reduced?'auto':'smooth',block:'start'});expect(document.activeElement.textContent).toContain('latest completed result');expect(v.get('.round-list [aria-current="true"] strong').text()).toBe('#4')
 expect(v.get('.chat-log').isVisible()).toBe(true);expect(v.get('form').isVisible()).toBe(false);expect(v.get('textarea').element.value).toBe('下一份未送出的草稿')
 await mode(v,'illnessCourse');expect(v.find('[data-action="latest-result"]').exists()).toBe(false)
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
 await chooseRound(v,first);await v.get('[data-action="performance"]').trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toEqual(capture('第一個擷取'))
 await chooseRound(v,'u3');await v.get('[data-action="performance"]').trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toBeNull()
 expect(v.get('textarea').element.value).toBe('');expect(storage).not.toHaveBeenCalled();expect(api.mock.calls.filter(([,o])=>o?.body).every(([,o])=>!JSON.stringify(o.body).includes('實際系統提示'))).toBe(true)
 await mode(v,'illnessCourse');await mode(v,'premorbid');await chooseRound(v,first);await v.get('[data-action="performance"]').trigger('click');expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toBeNull()
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
 const v=view(false,'student');await flushPromises();await send(v,'student');expect(v.find('[data-action="performance"]').exists()).toBe(false);expect(v.getComponent(ChatPerformanceDialog).props('canInspectRequests')).toBe(false);expect(v.getComponent(ChatPerformanceDialog).props('requestInspection')).toBeNull()
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
it('locks model changes while leaving Raw and waits for cleanup before selecting a phase',async()=>{
 config.canChooseModel=true;config.canReview=true;config.modelProfiles=[{key:'llama3',modelName:'llama3.1:8b'}]
 const v=view();await flushPromises();let finish
 api.mockImplementationOnce(()=>new Promise(resolve=>{finish=resolve}))
 await v.get('[data-workspace-tab="write"]').trigger('click')
 expect(v.find('[data-model-select]').exists()).toBe(false)
 finish(null);await flushPromises()
 expect(v.get('[data-mode="premorbid"]').attributes('aria-selected')).toBe('true')
 expect(v.get('[data-model-select]').element.disabled).toBe(false)
})
it('finishes loading when returning to Raw on a read-only course',async()=>{
 config.canChat=false;const v=view();await flushPromises();await mode(v,'premorbid');await mode(v,'raw')
 expect(v.get('[data-mode="raw"]').attributes('aria-selected')).toBe('true')
 expect(v.text()).not.toContain('載入對話中')
 expect(api.mock.calls.some(([p,o])=>p.endsWith('/raw/sessions')&&o?.method==='POST')).toBe(false)
})
it('checks only the paired original against the active B stage inside the feedback bubble without rewriting AI praise',async()=>{
 config.classifications=[{code:'A1a',phase:'A',label:'年齡'}]
 history={activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'u',role:'user',content:'55歲',status:'completed'},{id:'a',role:'assistant',replyTo:'u',content:'原有 AI 讚美文字',annotations:[{annotationId:'age',start:0,end:3,text:'55歲',codes:['A1a'],status:'unverified'}]},{id:'u2',createdAt:'2026-11-20T02:00:00Z',role:'user',content:'另一版',status:'completed'},{id:'a2',role:'assistant',replyTo:'u2',content:'舊回饋',annotations:null}]}]}
 const v=view();await flushPromises();await mode(v,'illnessCourse');await chooseRound(v,'u')
 expect(v.getComponent(PhaseAnnotatedAnswer).props('stage')).toBe('illnessCourse');expect(v.get('.from-assistant .message-content').text()).toBe('原有 AI 讚美文字')
 expect(v.get('.from-assistant [data-phase-alignment]').text()).toContain('目前階段：B 疾病病程');expect(v.get('.from-assistant [data-phase-alignment]').text()).toContain('A 病前功能')
 await chooseRound(v,'u2');expect(v.find('[data-phase-alignment]').exists()).toBe(false)
})
it('uses formal snapshots for teacher review and phase changes without reading private drafts',async()=>{
 config.canReview=true;config.classifications=[{code:'A1a',phase:'A',label:'年齡'}]
 const original=api.getMockImplementation(),snapshot=stage=>({id:stage,stage,studentId:'s1',studentName:'合成學員',version:1,status:'submitted',answer:'55歲',feedback:'正式回饋',annotations:[{annotationId:'age',start:0,end:3,text:'55歲',codes:['A1a'],status:'unverified'}]})
 api.mockImplementation(async(p,o)=>p===base+'/records'?{students:[{id:'s1',displayName:'合成學員'}]}:p===base+'/submissions'?{submissions:[snapshot('premorbid'),snapshot('illnessCourse')]}:original(p,o))
 const v=view();await flushPromises();await v.get('[data-action="review"]').trigger('click');await flushPromises()
 expect(v.getComponent(PhaseAnnotatedAnswer).props('stage')).toBe('premorbid');expect(v.text()).toContain('正式回饋')
 await v.get('[data-phase-navigation] [data-mode="illnessCourse"]').trigger('click');await flushPromises();expect(v.getComponent(PhaseAnnotatedAnswer).props('stage')).toBe('illnessCourse')
 expect(v.find('textarea').exists()).toBe(true);expect(v.find('form').exists()).toBe(false);expect(v.find('[data-action="revise-answer"]').exists()).toBe(false)
 expect(api.mock.calls.some(([p])=>/\/records\/[^/]+\//.test(p))).toBe(false)
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
  if(p===base+'/submissions')return {submissions:[{id:'formal-1',studentId:'student-1',stage:'premorbid',version:1,status:'returned',answer:'formal student message',feedback:'formal feedback'}]}
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
 expect(v.text()).not.toContain('late raw');expect(v.get('[data-mode="premorbid"]').attributes('aria-selected')).toBe('true')
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
 await send(v,'stage draft retained');expect(v.text()).toContain('請縮短本次作答');expect(v.text()).toContain('篩選歷史不會改變本次容量');expect(v.get('textarea').element.value).toBe('stage draft retained')
})
it('fetches saved stages again after remount and retains closed history in the unified list',async()=>{
 history={activeSessionId:'active',sessions:[{id:'closed',status:'closed',messages:[{id:'u',role:'user',content:'old saved conversation'}]},{id:'active',status:'active',messages:[]}]}
 const v=view();await flushPromises();await mode(v,'premorbid');await chooseRound(v,'u')
 expect(v.text()).toContain('old saved conversation');expect(v.find('textarea').exists()).toBe(true)
 v.unmount();const again=view();await flushPromises();await mode(again,'premorbid');await chooseRound(again,'u');expect(again.text()).toContain('old saved conversation')
 expect(api.mock.calls.filter(([p])=>p.split('?')[0]===base+'/stages/premorbid')).toHaveLength(2)
})
it('reminds before browser unload and preserves the raw conversation if clearing fails',async()=>{
 const v=view();await flushPromises();await send(v,'keep this raw')
 const event=new Event('beforeunload',{cancelable:true});window.dispatchEvent(event);expect(event.defaultPrevented).toBe(true)
 api.mockImplementationOnce(async()=>{throw new Error('clear failed')})
 await mode(v,'premorbid');expect(v.text()).toContain('clear failed');expect(v.text()).toContain('keep this raw');expect(v.get('[data-mode="raw"]').attributes('aria-selected')).toBe('true')
})
it('starts a new stage conversation when only closed history exists',async()=>{
 history={activeSessionId:null,sessions:[{id:'closed',status:'closed',messages:[{id:'u',role:'user',content:'old closed conversation'}]}]}
 const v=view();await flushPromises();await mode(v,'premorbid')
 expect(v.find('textarea').exists()).toBe(true);expect(v.get('.chat-log').text()).toContain('old closed conversation');expect(v.get('[data-history-model-filter]').element.value).toBe('')
 await v.get('[data-workspace-tab="write"]').trigger('click');expect(v.get('textarea').isVisible()).toBe(true);expect(api.mock.calls.some(([path])=>path.endsWith('/restart'))).toBe(false)
})
it('honors read-only preview and restricted TA review capabilities',async()=>{
 config.canChat=false;config.canReview=false;config.isPractice=true
 const v=mount(CaseAnalysisChat,{props:{course:{id:'c',courseRole:'ta'}}});wrappers.push(v);await flushPromises()
 expect(v.text()).toContain('目前為唯讀檢視');expect(v.find('textarea').exists()).toBe(false);expect(v.find('[data-action="review"]').exists()).toBe(false)
 expect(api.mock.calls.some(([p])=>p.includes('/records')||p.includes('/raw/sessions'))).toBe(false)
})
it('lets teaching staff preview every student writing phase without editing or sending',async()=>{
 experience.student=true;config.canChat=false;config.canReview=false;config.canUseTesting=false;config.canChooseModel=false;config.canInspectRequests=false;config.isPractice=true
 const v=mount(CaseAnalysisChat,{props:{course:{id:'c',courseRole:'student',canPractice:true}}});wrappers.push(v);await flushPromises()
 const writing=v.get('[data-workspace-tab="write"]');expect(writing.element.disabled).toBe(false);expect(writing.attributes('aria-selected')).toBe('true')
 for(const phase of stages){
  await mode(v,phase.key);await writing.trigger('click');const guide=v.get('[data-stage-guidance]');expect(guide.attributes('data-guidance-audience')).toBe('student');expect(guide.findAll('[data-guide-phase]')).toHaveLength(1);expect(guide.get('[data-guide-phase]').attributes('data-guide-phase')).toBe(phase.label[0])
  expect(v.get('#case-chat-draft').element.disabled).toBe(true);expect(v.get('form button[type="submit"]').element.disabled).toBe(true)
  await v.get('[data-workspace-tab="view"]').trigger('click');await writing.trigger('click');expect(v.get('form').isVisible()).toBe(true)
 }
 expect(v.get('[data-action="return-teaching"]').attributes('href')).toBe('/courses/c?section=case-analysis&view=teaching');expect(v.find('[data-mode="raw"]').exists()).toBe(false);expect(v.find('[data-model-select]').exists()).toBe(false);expect(v.find('[data-case-rules-editor]').exists()).toBe(false)
 await v.get('#case-chat-draft').setValue('嘗試繞過停用輸入框');await v.get('form').trigger('submit');await flushPromises();expect(api.mock.calls.filter(([,o])=>o?.method&&o.method!=='GET')).toHaveLength(0)
})
it('creates independent drafts without a restart while keeping old attempts available',async()=>{
 history={activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'u',role:'user',content:'saved before restart'}]}]}
 const v=view();await flushPromises();await mode(v,'premorbid')
 expect(v.find('[data-action="restart-stage"]').exists()).toBe(false);await v.get('[data-workspace-tab="write"]').trigger('click');await v.get('textarea').setValue('新的獨立作答')
 await v.get('[data-workspace-tab="view"]').trigger('click');expect(v.get('.chat-log').text()).toContain('saved before restart');expect(v.get('textarea').element.value).toBe('新的獨立作答')
 expect(api.mock.calls.some(([p])=>p.endsWith('/restart'))).toBe(false)
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
 {id:'u1',createdAt:'2026-11-20T01:00:00Z',role:'user',content:'  第一份原始作答 \n',status:'completed',revisesMessageId:null},{id:'a1',role:'assistant',content:'第一輪回饋',replyTo:'u1',metrics:null},
 {id:'u2',createdAt:'2026-11-20T02:00:00Z',role:'user',content:'第二份獨立作答',status:'completed',revisesMessageId:null},{id:'a2',role:'assistant',content:'第二輪回饋',replyTo:'u2',metrics:{promptTokens:20,responseTokens:10}},
 {id:'u3',createdAt:'2026-11-20T03:00:00Z',role:'user',content:'第三份修改作答',status:'completed',revisesMessageId:'u1'},{id:'a3',role:'assistant',content:'第三輪回饋',replyTo:'u3',modelName:'saved-response-model',promptVersion:'saved-v1',metrics:{promptTokens:30,responseTokens:15}}
 ]}]}}
it('retains the annotation display choice when selecting another response round without storing it',async()=>{
 config.classifications=[{code:'A1a',label:'年齡',phase:'A'}];history=savedRounds()
 const messages=history.sessions[0].messages
 for(const reply of messages.filter(m=>m.role==='assistant')){const answer=messages.find(m=>m.id===reply.replyTo);reply.annotations=[{annotationId:reply.id,start:0,end:answer.content.length,text:answer.content,codes:['A1a'],status:'unverified'}]}
 const storage=vi.spyOn(Storage.prototype,'setItem'),v=view();await flushPromises();await mode(v,'premorbid')
 await v.get('[data-annotation-mode="badges"]').trigger('click');await chooseRound(v,'u1')
 expect(v.get('[data-annotation-mode="badges"]').attributes('aria-selected')).toBe('true');expect(v.getComponent(PhaseAnnotatedAnswer).props('displayMode')).toBe('badges');expect(v.find('[data-annotation-reference]').exists()).toBe(true)
 expect(v.get('.from-assistant [data-phase-alignment]').text()).toContain('編碼徽章');expect(storage).not.toHaveBeenCalled()
 await v.get('[data-annotation-mode="underline"]').trigger('click');await chooseRound(v,'u2')
 expect(v.get('[data-annotation-mode="underline"]').attributes('aria-selected')).toBe('true');expect(v.get('[data-annotated-text]').element.textContent).toBe('第二份獨立作答');expect(v.find('[data-annotation-segment]').exists()).toBe(false);expect(v.find('[data-annotation-reference]').exists()).toBe(false);expect(v.get('.from-assistant [data-phase-alignment]').text()).toContain('原文模式（未顯示標註）');expect(storage).not.toHaveBeenCalled()
})
it('selects one feedback pair without changing the draft and labels all revisions with one number',async()=>{
 history=savedRounds();const v=view();await flushPromises();await mode(v,'premorbid')
 expect(v.findAll('[data-round]')).toHaveLength(3);expect(v.get('.chat-log').text()).toContain('第三輪回饋');expect(v.get('.chat-log').text()).not.toContain('第一輪回饋')
 expect(v.findAll('[data-round]').map(item=>item.attributes('data-round'))).toEqual(['u3','u2','u1'])
 expect(v.findAll('[data-round] strong').map(item=>item.text())).toEqual(['#3','#2','#1'])
 expect(v.findAll('[data-round] small').map(item=>item.text())).toEqual(['2026-11-20T03:00:00Z','2026-11-20T02:00:00Z','2026-11-20T01:00:00Z'].map(value=>new Date(value).toLocaleString('zh-TW')))
 expect(v.get('.round-heading h3').text()).toBe('#3')
 expect(v.findAll('[role="combobox"][aria-label="分析版本"]')).toHaveLength(0);expect(v.find('.round-history').exists()).toBe(false)
 await v.get('textarea').setValue('尚未提交的內容');await chooseRound(v,'u1')
 expect(v.get('.chat-log').text()).toContain('第一輪回饋');expect(v.get('.chat-log').text()).not.toContain('第三輪回饋');expect(v.get('textarea').element.value).toBe('尚未提交的內容')
 expect(v.get('[data-round="u2"] strong').text()).toBe('#2');expect(v.get('[data-round="u3"] strong').text()).toBe('#3')
 expect(v.get('.round-list').text()).not.toMatch(/原始作答|修改 \d+ 次/)
})
it('copies an old answer verbatim and submits a new immutable revision with its parent',async()=>{
 history=savedRounds();const v=view();await flushPromises();await mode(v,'premorbid');await chooseRound(v,'u1');await v.get('[data-action="revise-answer"]').trigger('click')
 expect(v.get('form').isVisible()).toBe(true);expect(v.get('.chat-log').isVisible()).toBe(false)
 expect(v.get('textarea').element.value).toBe('  第一份原始作答 \n');await v.get('textarea').setValue('  修改舊答案 \n')
 const original=api.getMockImplementation();api.mockImplementation(async(p,o)=>{if(p.endsWith('/premorbid/messages')&&o?.method==='POST'){history.sessions[0].messages.push({id:'u4',role:'user',content:o.body.content,revisesMessageId:o.body.revisesMessageId,status:'completed'},{id:'a4',role:'assistant',content:'新修改回饋',replyTo:'u4'});return structuredClone(history)}return original(p,o)})
 await v.get('form').trigger('submit');await flushPromises()
 const request=api.mock.calls.find(([p,o])=>p.endsWith('/premorbid/messages')&&o?.method==='POST')[1].body
 expect(request).toEqual({requestId:expect.any(String),content:'  修改舊答案 \n',revisesMessageId:'u1'});expect(history.sessions[0].messages[0].content).toBe('  第一份原始作答 \n');expect(v.get('.chat-log').text()).toContain('新修改回饋')
 expect(v.get('[data-action="next-stage"]').text()).toContain('B 疾病病程');expect(v.get('[data-mode="premorbid"]').attributes('aria-selected')).toBe('true')
})
it('preserves revision parent and request identity on ambiguous failure and clears both when switching stage',async()=>{
 history=savedRounds();const v=view();await flushPromises();await mode(v,'premorbid');await chooseRound(v,'u1');await v.get('[data-action="revise-answer"]').trigger('click')
 api.mockImplementationOnce(async()=>{throw new TypeError('network uncertain')});await send(v,' revision ');await send(v,' revision ')
 const sends=api.mock.calls.filter(([p,o])=>p.endsWith('/premorbid/messages')&&o?.method==='POST');expect(sends[0][1].body).toEqual(sends[1][1].body);expect(sends[0][1].body.revisesMessageId).toBe('u1')
 await mode(v,'illnessCourse');await send(v,'independent answer');const last=api.mock.calls.filter(([p,o])=>p.endsWith('/illnessCourse/messages')&&o?.method==='POST').at(-1)
 expect(last[1].body.revisesMessageId).toBeNull()
})
it('opens metrics for the selected response including unavailable old metrics and closes on mode switch',async()=>{
 config.canReview=true;history=savedRounds();const v=view();await flushPromises();await mode(v,'premorbid')
 await v.get('[data-action="performance"]').trigger('click');expect(v.findComponent({name:'ChatPerformanceDialog'}).props('metrics')).toEqual({promptTokens:30,responseTokens:15});expect(v.findComponent({name:'ChatPerformanceDialog'}).props('modelName')).toBe('saved-response-model');expect(v.findComponent({name:'ChatPerformanceDialog'}).props('promptVersion')).toBe('saved-v1')
 await v.findComponent({name:'ChatPerformanceDialog'}).vm.$emit('close');await chooseRound(v,'u1');await v.get('[data-action="performance"]').trigger('click')
 expect(v.findComponent({name:'ChatPerformanceDialog'}).props('metrics')).toBeNull();await mode(v,'illnessCourse');expect(v.findComponent({name:'ChatPerformanceDialog'}).props('open')).toBe(false)
})
it('compares all student inputs in the selected stage while excluding AI responses and other stages',async()=>{
 history=savedRounds();const v=view();await flushPromises();await mode(v,'premorbid')
 const revisions=v.findComponent({name:'AnswerRevisionDiff'}).props('revisions');expect(revisions.map(r=>r.id)).toEqual(['u1','u2','u3']);expect(revisions[0].content).toBe('  第一份原始作答 \n')
 history={activeSessionId:'different',sessions:[{id:'different',status:'active',messages:[{id:'other-stage',role:'user',content:'不同階段'}]}]};await mode(v,'illnessCourse')
 expect(v.findComponent({name:'AnswerRevisionDiff'}).props('revisions').map(r=>r.id)).toEqual(['other-stage'])
})
it('retries a failed revision without creating another revision and preserves its padded content',async()=>{
 history=savedRounds();const v=view();await flushPromises();await mode(v,'premorbid');await chooseRound(v,'u1');await v.get('[data-action="revise-answer"]').trigger('click')
 const content='  修改但暫時失敗 \n';api.mockImplementationOnce(async(p,o)=>{history.sessions[0].messages.push({id:'failed-revision',role:'user',content:o.body.content,status:'failed',revisesMessageId:o.body.revisesMessageId});history=structuredClone(history);throw new Error('模型失敗')})
 await send(v,content);expect(v.get('textarea').element.value).toBe(content);expect(v.get('[data-round="failed-revision"] strong').text()).toBe('#4');expect(v.get('[data-action="revise-answer"]').attributes('disabled')).toBeDefined()
 const original=api.getMockImplementation();api.mockImplementation(async(p,o)=>{if(p.endsWith('/failed-revision/retry')){history.sessions[0].messages.at(-1).status='completed';return structuredClone(history)}return original(p,o)})
 await v.get('form').trigger('submit');await flushPromises()
 expect(api.mock.calls.filter(([p,o])=>p.endsWith('/premorbid/messages')&&o?.method==='POST')).toHaveLength(1);expect(api.mock.calls.some(([p])=>p.endsWith('/failed-revision/retry'))).toBe(true);expect(v.findAll('[data-round]')).toHaveLength(4);expect(v.get('.from-user .message-content').element.textContent).toBe(content)
})
it('filters formal snapshots by selected learner and keeps private round comparisons out of teacher review',async()=>{
 config.canReview=true;const original=api.getMockImplementation();api.mockImplementation(async(p,o)=>{
  if(p===base+'/records')return {students:[{id:'s1',displayName:'學員一'},{id:'s2',displayName:'學員二'}]}
  if(p===base+'/submissions')return {submissions:[{id:'one',studentId:'s1',stage:'premorbid',version:1,status:'returned',answer:'第一位正式作答',feedback:'正式回饋一'},{id:'two',studentId:'s2',stage:'premorbid',version:1,status:'returned',answer:'第二位正式作答',feedback:'正式回饋二'}]}
  return original(p,o)
 })
 const v=view();await flushPromises();await v.get('[data-action="review"]').trigger('click');await flushPromises();await v.get('[data-student="s1"]').trigger('click');await flushPromises()
 expect(v.findComponent({name:'AnswerRevisionDiff'}).exists()).toBe(false);expect(v.text()).toContain('第一位正式作答');expect(v.text()).not.toContain('第二位正式作答')
 expect(v.find('[data-action="revise-answer"]').exists()).toBe(false);expect(v.find('form').exists()).toBe(false)
 await v.get('[data-student="s2"]').trigger('click');await flushPromises();expect(v.text()).toContain('第二位正式作答');expect(v.text()).not.toContain('第一位正式作答')
 expect(api.mock.calls.some(([p])=>/\/records\/[^/]+\//.test(p))).toBe(false)
})
it('displays ephemeral raw performance and removes it with the raw conversation',async()=>{
 const v=view();await flushPromises();expect(Boolean(v.get('.chat-log').element.compareDocumentPosition(v.get('form').element)&Node.DOCUMENT_POSITION_FOLLOWING)).toBe(true);const original=api.getMockImplementation();api.mockImplementation(async(p,o)=>{const data=await original(p,o);if(p.includes('/raw/')&&p.endsWith('/messages'))data.messages.at(-1).metrics={promptTokens:17,responseTokens:5};return data})
 await send(v,'raw metrics');await v.get('[data-action="performance"]').trigger('click');expect(v.findComponent({name:'ChatPerformanceDialog'}).props('metrics')).toEqual({promptTokens:17,responseTokens:5})
 await v.get('[data-action="clear-raw"]').trigger('click');await flushPromises();expect(v.findComponent({name:'ChatPerformanceDialog'}).props('open')).toBe(false);expect(v.findAll('[data-action="performance"]')).toHaveLength(0);expect(v.find('.round-sidebar').exists()).toBe(false)
})
it('copies the exact selected response beside annotation modes without submitting and focuses the editor',async()=>{
 history=savedRounds();const v=view(true);await flushPromises();await mode(v,'premorbid');await chooseRound(v,'u1')
 const calls=api.mock.calls.length,button=v.get('.annotation-display [data-action="revise-answer"]')
 expect(button.text()).toBe('複製到輸入框');expect(v.find('.round-heading [data-action="revise-answer"]').exists()).toBe(false)
 expect(v.find('[data-action="trash-round"]').exists()).toBe(false);expect(v.get('.annotation-actions [data-action="revise-answer"]').element).toBe(button.element)
 await button.trigger('click');await flushPromises()
 expect(v.get('textarea').element.value).toBe('  第一份原始作答 \n');expect(document.activeElement).toBe(v.get('textarea').element)
 expect(v.get('.revision-notice').text()).toContain('#1');expect(api.mock.calls).toHaveLength(calls)
})
it('confirms replacing a draft and preserves its revision parent when revising again is canceled',async()=>{
 history=savedRounds();const v=view();await flushPromises();await mode(v,'premorbid');await chooseRound(v,'u1');await v.get('textarea').setValue('  私人草稿 \n')
 window.confirm.mockReturnValueOnce(false);await v.get('[data-action="revise-answer"]').trigger('click');expect(v.get('textarea').element.value).toBe('  私人草稿 \n');expect(v.find('.revision-notice').exists()).toBe(false)
 await v.get('[data-action="revise-answer"]').trigger('click');expect(v.get('textarea').element.value).toBe('  第一份原始作答 \n');await v.get('textarea').setValue('  編輯中的修改 \n')
 window.confirm.mockReturnValueOnce(false);await v.get('[data-action="revise-answer"]').trigger('click');expect(v.get('textarea').element.value).toBe('  編輯中的修改 \n');expect(v.get('.revision-notice').text()).toContain('#1')
 await chooseRound(v,'u2');window.confirm.mockReturnValueOnce(false);await v.get('[data-action="revise-answer"]').trigger('click');expect(v.get('.revision-notice').text()).toContain('#1');expect(v.get('textarea').element.value).toBe('  編輯中的修改 \n')
 await v.get('[data-action="revise-answer"]').trigger('click');expect(v.get('.revision-notice').text()).toContain('#2');expect(v.get('textarea').element.value).toBe('第二份獨立作答')
})
it('guards next-stage departure while model filtering and history selection preserve the draft',async()=>{
 history=mixedModelHistory();const v=view();await flushPromises();await mode(v,'premorbid');await chooseRound(v,'mixed-3');await v.get('[data-action="revise-answer"]').trigger('click');await v.get('textarea').setValue('正在編輯')
 window.confirm.mockReturnValueOnce(false);await v.get('[data-action="next-stage"]').trigger('click');expect(v.get('[data-mode="premorbid"]').attributes('aria-selected')).toBe('true');expect(v.get('textarea').element.value).toBe('正在編輯')
 await v.get('[data-history-model-filter]').setValue('model:retired-model:7b');expect(v.get('.chat-log').text()).toContain('歷史模型回饋');expect(v.get('textarea').element.value).toBe('正在編輯');expect(v.get('.revision-notice').text()).toContain('#3')
 await v.get('[data-history-model-filter]').setValue('');expect(v.get('textarea').element.value).toBe('正在編輯');expect(v.get('.revision-notice').text()).toContain('#3')
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
 history={activeSessionId:'s',sessions:[{id:'s',status:'active',messages:[{id:'u1',createdAt:'2026-11-20T01:00:00Z',role:'user',content:'55歲',status:'completed'},{id:'a1',role:'assistant',replyTo:'u1',content:'回饋',annotations},{id:'u2',createdAt:'2026-11-20T02:00:00Z',role:'user',content:'另一版',status:'pending'}]}]}
 const v=view();await flushPromises();expect(v.findComponent(PhaseAnnotatedAnswer).exists()).toBe(false);await mode(v,'premorbid');expect(v.getComponent(PhaseAnnotatedAnswer).props('annotations')).toEqual([]);expect(v.find('[data-annotation-segment]').exists()).toBe(false)
 await chooseRound(v,'u1');expect(v.getComponent(PhaseAnnotatedAnswer).props('content')).toBe('55歲');expect(v.getComponent(PhaseAnnotatedAnswer).props('annotations')).toEqual(annotations)
 expect(v.find('.category-sidebar .annotation-legend').exists()).toBe(true);expect(v.find('.from-user .annotation-legend').exists()).toBe(true);expect(v.find('.category-empty').exists()).toBe(false)
 const answer=v.get('.from-user [data-annotated-text]'),categories=v.get('.from-user .category-sidebar');expect(Boolean(answer.element.compareDocumentPosition(categories.element)&Node.DOCUMENT_POSITION_FOLLOWING)).toBe(true)
 await v.get('[data-action="revise-answer"]').trigger('click');expect(v.get('textarea').element.value).toBe('55歲');await v.get('textarea').setValue('未提交修改');expect(v.getComponent(PhaseAnnotatedAnswer).props('content')).toBe('55歲')
 await chooseRound(v,'u2');expect(v.getComponent(PhaseAnnotatedAnswer).props('annotations')).toEqual([]);expect(v.find('[data-annotation-segment]').exists()).toBe(false);expect(v.get('[data-action="revise-answer"]').attributes('disabled')).toBeDefined();expect(v.find('.category-sidebar .annotation-legend').exists()).toBe(false);expect(v.find('.category-empty').exists()).toBe(true)
})
it('clears snapshot annotation inspection when selecting another formal version',async()=>{
 config.canReview=true;config.classifications=[{code:'A1a',label:'年齡',phase:'A'}]
 const annotations=[{annotationId:'age',start:0,end:3,text:'55歲',codes:['A1a'],explanation:'合成分類',status:'unverified'}]
 const original=api.getMockImplementation();api.mockImplementation(async(p,o)=>p===base+'/submissions'?{submissions:[{id:'first',studentId:'s1',stage:'premorbid',version:1,status:'returned',answer:'55歲',feedback:'第一份正式回饋',annotations},{id:'second',studentId:'s1',stage:'premorbid',version:2,status:'returned',answer:'不同正式版本',feedback:'第二份正式回饋',annotations:[]}]}:original(p,o))
 const v=view();await flushPromises();await v.get('[data-action="review"]').trigger('click');await flushPromises()
 expect(v.getComponent(PhaseAnnotatedAnswer).props('content')).toBe('不同正式版本');await v.get('.formal-version-select select').setValue('first')
 await v.get('[data-legend-code="A1a"]').trigger('click');await v.get('[data-annotation-reference]').trigger('focus');expect(v.find('[role="tooltip"]').exists()).toBe(true)
 await v.get('.formal-version-select select').setValue('second');expect(v.find('[role="tooltip"]').exists()).toBe(false);expect(v.find('[data-annotation-segment]').exists()).toBe(false)
 expect(v.get('[data-annotated-text]').element.textContent).toBe('不同正式版本');expect(v.find('.round-sidebar').exists()).toBe(false)
})
