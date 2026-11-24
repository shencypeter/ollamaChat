import {mount,flushPromises} from '@vue/test-utils'
import {afterEach,beforeEach,expect,it,vi} from 'vitest'
import CaseFormalSubmissions from './CaseFormalSubmissions.vue'
import PhaseAnnotatedAnswer from './PhaseAnnotatedAnswer.vue'
import AnswerRevisionDiff from './AnswerRevisionDiff.vue'
import {api} from '../api'
import {auth} from '../composables/auth'
import {experience} from '../composables/experience'
vi.mock('../api',()=>({api:vi.fn()}))
const base='/courses/c/case-analysis/submissions'
const snapshot=(extra={})=>({id:'f1',studentId:'student',studentName:'合成學生',stage:'premorbid',version:1,status:'submitted',submittedAt:'2026-11-20T00:00:00Z',analysisMessageId:'u1',answer:'合成答案',feedback:'合成回饋',annotations:[],...extra})
let wrappers=[]
function view(extra={}){const v=mount(CaseFormalSubmissions,{props:{course:{id:'c',courseRole:'student'},config:{canChat:true,canSubmitFormal:true},stage:'premorbid',selectedRound:{id:'u1',status:'completed',content:'合成答案'},...extra}});wrappers.push(v);return v}
beforeEach(()=>{experience.student=false;auth.user=null;vi.spyOn(window,'confirm').mockReturnValue(true);api.mockReset().mockResolvedValue({submissions:[],assignments:[]})})
afterEach(()=>{wrappers.forEach(v=>v.unmount());wrappers=[];vi.restoreAllMocks()})
it.each(['teacher','ta'])('hides student submission reminders in personal teaching practice (%s)',async role=>{
 const v=view({course:{id:'c',courseRole:role},config:{canChat:true,isPractice:true}});await flushPromises();expect(v.find('.formal-submissions').exists()).toBe(false);expect(v.text()).not.toMatch(/尚.*正式提交|正式提交與版本/);expect(api).not.toHaveBeenCalled()
})
it('does not enable formal submission when the existing version envelope failed to load',async()=>{
 api.mockRejectedValueOnce(new Error('載入失敗'));const v=view();await flushPromises();expect(v.get('[role="alert"]').text()).toContain('載入失敗');expect(v.get('[data-action="submit-formal"]').element.disabled).toBe(true)
})
it('confirms an immutable selected analysis with its assignment and keeps retry identity after ambiguous failure',async()=>{
 api.mockResolvedValueOnce({submissions:[],assignments:[{id:'assignment',title:'合成個人作業'}]});const v=view();await flushPromises();await v.get('.assignment-select select').setValue('assignment')
 window.confirm.mockReturnValueOnce(false);await v.get('[data-action="submit-formal"]').trigger('click');expect(api.mock.calls.filter(([,o])=>o?.method==='POST')).toHaveLength(0)
 api.mockRejectedValueOnce(new TypeError('連線中斷'));await v.get('[data-action="submit-formal"]').trigger('click');await flushPromises();expect(v.get('[role="alert"]').text()).toContain('連線中斷')
 const body=api.mock.calls.at(-1)[1].body;expect(body).toEqual({requestId:expect.stringMatching(/^[0-9a-f-]{36}$/),messageId:'u1',assignmentId:'assignment',expectedPreviousId:null})
 api.mockResolvedValueOnce({submissions:[snapshot()],assignments:[]});await v.get('[data-action="submit-formal"]').trigger('click');await flushPromises()
 expect(api.mock.calls.at(-1)[1].body).toEqual(body);expect(v.get('[data-submission="f1"]').text()).toContain('正式第 1 版');expect(v.get('[data-action="submit-formal"]').element.disabled).toBe(true)
 expect(v.emitted('loaded').at(-1)[0].submissions[0].answer).toBe('合成答案')
})
it('requires an exactly matching analyzed draft and a completed nontrashed round',async()=>{
 const v=view({draft:'變更後答案'});await flushPromises();expect(v.get('[data-formal-draft-warning]').text()).toContain('先取得新的 AI 分析');expect(v.get('[data-action="submit-formal"]').element.disabled).toBe(true)
 await v.setProps({draft:'合成答案'});expect(v.get('[data-action="submit-formal"]').element.disabled).toBe(false)
 await v.setProps({draft:'合成答案 '});expect(v.get('[data-action="submit-formal"]').element.disabled).toBe(true)
 await v.setProps({draft:'',selectedRound:{id:'u1',status:'failed',content:'合成答案'}});expect(v.get('[data-action="submit-formal"]').element.disabled).toBe(true)
 await v.setProps({selectedRound:{id:'u1',status:'completed',content:'合成答案',trashedAt:'2026-11-20'}});expect(v.get('[data-action="submit-formal"]').element.disabled).toBe(true)
})
it('clears an unavailable assignment after refresh so a rejected link cannot trap subsequent submissions',async()=>{
 api.mockResolvedValueOnce({submissions:[],assignments:[{id:'closed',title:'即將關閉的作業'}]});const v=view();await flushPromises();await v.get('.assignment-select select').setValue('closed')
 api.mockRejectedValueOnce(new Error('所選作業已截止'));await v.get('[data-action="submit-formal"]').trigger('click');await flushPromises()
 api.mockResolvedValueOnce({submissions:[],assignments:[]});await v.get('[role="alert"] button').trigger('click');await flushPromises();expect(v.find('.assignment-select').exists()).toBe(false)
 api.mockResolvedValueOnce({submissions:[snapshot()],assignments:[]});await v.get('[data-action="submit-formal"]').trigger('click');await flushPromises();expect(api.mock.calls.at(-1)[1].body.assignmentId).toBeNull()
})
it.each([{course:{id:'c',courseRole:'teacher'}},{course:{id:'c',courseRole:'ta'}},{config:{canChat:true,canSubmitFormal:false}},{config:{canChat:true,isPractice:true}},{config:{canChat:false}}])('keeps practice and unavailable membership out of formal writes (%j)',async extra=>{
 const v=view(extra);await flushPromises();expect(v.find('[data-action="submit-formal"]').exists()).toBe(false)
})
it('makes student experience read only even if a permissive config arrives',async()=>{
 experience.student=true;const v=view();await flushPromises();expect(v.find('[data-action="submit-formal"]').exists()).toBe(false)
})
it('uses the latest returned version as the explicit predecessor and retains older immutable snapshots',async()=>{
 api.mockResolvedValueOnce({submissions:[snapshot({id:'old',status:'returned',returnReason:'舊理由',version:1}),snapshot({id:'latest',status:'returned',returnReason:'補充依據',returnedAt:'2026-11-20T01:00:00Z',version:2})]});const v=view();await flushPromises()
 expect(v.get('.formal-version-select select').element.value).toBe('latest');expect(v.get('[data-submission]').text()).toContain('補充依據');expect(v.get('.formal-snapshot>h4').text()).toBe(new Date('2026-11-20T00:00:00Z').toLocaleString('zh-TW'));expect(v.get('.formal-snapshot').text()).toContain('正式第 2 版')
 await v.get('.formal-version-select select').setValue('old');expect(v.get('[data-submission]').text()).toContain('舊理由')
 api.mockResolvedValueOnce({submissions:[snapshot({id:'v3',version:3})]});await v.get('[data-action="submit-formal"]').trigger('click');await flushPromises();expect(api.mock.calls.at(-1)[1].body.expectedPreviousId).toBe('latest')
})
it('returns only the current formal version with a required reason and keeps teacher annotations on the snapshot catalog',async()=>{
 const catalog=[{code:'A1a',phase:'A',label:'快照名稱'}];api.mockResolvedValueOnce({submissions:[snapshot({ruleSnapshot:catalog})]})
 const v=view({course:{id:'c',courseRole:'teacher'},config:{canReview:true},review:true});await flushPromises();expect(v.getComponent(PhaseAnnotatedAnswer).props('classifications')).toEqual(catalog)
 expect(v.get('[data-action="return-formal"]').element.disabled).toBe(true);await v.get('textarea').setValue('   ');expect(v.get('[data-action="return-formal"]').element.disabled).toBe(true)
 await v.get('textarea').setValue(' 請補充依據 ');api.mockResolvedValueOnce({submissions:[snapshot({status:'returned',returnReason:'請補充依據',returnedAt:'2026-11-20T01:00:00Z'})]})
 await v.get('[data-action="return-formal"]').trigger('click');await flushPromises();expect(api.mock.calls.at(-1)).toEqual([base+'/f1/return',expect.objectContaining({method:'POST',body:{reason:'請補充依據'}})])
 expect(v.text()).toContain('請補充依據');expect(v.find('[data-action="return-formal"]').exists()).toBe(false);expect(v.find('[data-action="submit-formal"]').exists()).toBe(false)
})
it('filters formal snapshots by stage and student while keeping older submitted versions unreturnable',async()=>{
 api.mockResolvedValueOnce({submissions:[snapshot({id:'old',version:1}),snapshot({id:'latest',version:2}),snapshot({id:'other-stage',stage:'illnessCourse',version:8}),snapshot({id:'other-student',studentId:'s2',version:9})]})
 const v=view({course:{id:'c',courseRole:'teacher'},config:{canReview:true},review:true,studentId:'student'});await flushPromises();expect(v.get('.formal-version-select select').findAll('option')).toHaveLength(2)
 expect(v.get('.formal-version-select select').element.value).toBe('latest');await v.get('.formal-version-select select').setValue('old');expect(v.find('[data-action="return-formal"]').exists()).toBe(false)
})
it('compares only the selected learner formal snapshots without adding other learners or private draft rounds',async()=>{
 api.mockResolvedValueOnce({submissions:[snapshot({id:'one',version:1,status:'returned'}),snapshot({id:'two',version:2}),snapshot({id:'other',studentId:'s2',version:9})]})
 const v=view({course:{id:'c',courseRole:'teacher'},config:{canReviewFormal:true},review:true,studentId:'student'});await flushPromises()
 expect(v.getComponent(AnswerRevisionDiff).props('revisions')).toEqual([{id:'one',label:new Date('2026-11-20T00:00:00Z').toLocaleString('zh-TW'),content:'合成答案'},{id:'two',label:new Date('2026-11-20T00:00:00Z').toLocaleString('zh-TW'),content:'合成答案'}])
})
it('defaults course review to the most recently submitted snapshot across students',async()=>{
 api.mockResolvedValueOnce({submissions:[snapshot({id:'old-high',version:10,submittedAt:'2026-11-19T00:00:00Z'}),snapshot({id:'new-student',studentId:'s2',version:1,submittedAt:'2026-11-20T00:00:00Z'})]})
 const v=view({course:{id:'c',courseRole:'teacher'},config:{canReviewFormal:true},review:true});await flushPromises();expect(v.get('.formal-version-select select').element.value).toBe('new-student')
})
it('lets grading-authorized TA return formal work independently of statistics permission',async()=>{
 api.mockResolvedValueOnce({submissions:[snapshot()]});const v=view({course:{id:'c',courseRole:'ta'},config:{canReview:false,canReviewFormal:true},review:true});await flushPromises();expect(v.find('[data-action="return-formal"]').exists()).toBe(true)
})
it('keeps archived formal work readable while hiding return writes',async()=>{
 api.mockResolvedValueOnce({submissions:[snapshot()]});const v=view({course:{id:'c',courseRole:'teacher'},config:{canChat:false,canReviewFormal:true},review:true});await flushPromises();expect(v.find('[data-submission="f1"]').exists()).toBe(true);expect(v.find('[data-action="return-formal"]').exists()).toBe(false)
})
it.each([{course:{id:'c',courseRole:'teacher'},review:false},{course:{id:'c',courseRole:'ta'},review:true,config:{canReview:true,canReviewFormal:false}}])('does not fetch course formal work in practice or restricted grading (%j)',async extra=>{
 const v=view(extra);await flushPromises();expect(api).not.toHaveBeenCalled();expect(v.find('[data-submission]').exists()).toBe(false)
})
it('drops private snapshots and ignores late responses after account or experience changes',async()=>{
 let finish;api.mockImplementationOnce(()=>new Promise(resolve=>{finish=resolve}));const v=view();await flushPromises();auth.user={id:'different'};api.mockResolvedValueOnce({submissions:[]});await flushPromises()
 finish({submissions:[snapshot({answer:'舊帳號內容'})]});await flushPromises();expect(v.text()).not.toContain('舊帳號內容')
})
