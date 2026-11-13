import {mount,flushPromises} from '@vue/test-utils'
import {ref} from 'vue'
import {afterEach,beforeEach,expect,it,vi} from 'vitest'
import {api} from '../api'
import ReportAssignmentView from './ReportAssignmentView.vue'
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({useRoute:()=>({params:{courseId:'c',reportId:'r'}}),onBeforeRouteLeave:vi.fn(),onBeforeRouteUpdate:vi.fn()}))
const report={id:'r',courseId:'c',title:'觀察報告',instructions:'說明',status:'published',submissionMode:'group',evaluationMode:'off',dimensions:[],groupIds:['g1','g2'],courseRole:'teacher',maxScore:100,weight:1,dueAt:null,opensAt:null,version:1,isPractice:false}
const submission={id:'s',studentName:'第一組',studentNumber:null,submittedBy:'甲',fileName:'report.pdf',submittedAt:'2026-11-13T00:00:00Z',revision:1,returned:false,score:80,feedback:'回饋',publishedScore:null,version:1,history:[],members:[{id:'a',name:'甲',isLeader:true,self:false,score:null,reason:'',publishedScore:null},{id:'b',name:'乙',isLeader:false,self:false,score:90,reason:'貢獻較多',publishedScore:null}]}
let view
beforeEach(()=>{api.mockReset();vi.spyOn(window,'confirm').mockReturnValue(true);api.mockImplementation(async path=>path==='/reports/r'?structuredClone(report):path.endsWith('/roster')?[{groupId:'g2',submissionId:null,name:'第二組',submitted:false,late:null,dissolved:false},{groupId:'g1',submissionId:'s',name:'第一組',submitted:true,late:false,dissolved:false}]:path.endsWith('/submissions')?[structuredClone(submission)]:undefined)})
afterEach(()=>{view?.unmount();vi.restoreAllMocks()})
function render(restrictions=[]){view=mount(ReportAssignmentView,{global:{provide:{selectedCourse:ref({taRestrictions:restrictions})},stubs:{RouterLink:{props:['to'],template:'<a><slot/></a>'},FilePreview:true,ReportEvaluation:true,ReportModeFields:true}}});return view}
it('edits PDF maximum scores without displaying or resending a legacy course weight',async()=>{
 api.mockImplementation(async path=>path==='/reports/r'?{...structuredClone(report),status:'draft',weight:9}:path.endsWith('/roster')?[]:path.endsWith('/submissions')?[]:undefined)
 render();await flushPromises()
 expect(view.text()).not.toContain('權重')
 await view.findAll('button').find(button=>button.text()==='編輯作業草稿').trigger('click')
 expect(view.text()).not.toContain('權重')
 const form=view.get('form');expect(form.findAll('input[type="number"]')).toHaveLength(1)
 await form.get('input[type="number"]').setValue('80');await form.trigger('submit');await flushPromises()
 const body=api.mock.calls.find(([path,options])=>path==='/reports/r'&&options?.method==='PUT')[1].body
 expect(body.maxScore).toBe(80);expect(body).not.toHaveProperty('weight')
})
it('shows every assigned group, keeps missing groups read only and derives member final drafts',async()=>{
 render();await flushPromises()
 expect(view.get('.report-review-sidebar').text()).toContain('第二組');expect(view.get('.report-review-sidebar').text()).toContain('未繳交')
 const groups=view.findAll('.report-group-select');expect(groups.map(group=>group.text())).toEqual([expect.stringContaining('第一組'),expect.stringContaining('第二組')])
 expect(view.findAll('.report-grading')).toHaveLength(1)
 const members=view.findAll('.report-member-grade');expect(members[0].text()).toContain('個人成績草稿：80');expect(members[1].text()).toContain('個人成績草稿：90')
 await view.get('input[aria-label="第一組 小組基礎成績"]').setValue(85)
 expect(members[0].text()).toContain('個人成績草稿：85');expect(members[1].text()).toContain('個人成績草稿：90')
 await view.get('.report-grading').trigger('submit');await flushPromises()
 expect(api).toHaveBeenCalledWith('/reports/r/submissions/s/grade',{method:'PUT',body:{adjustments:[{studentId:'a',score:null,reason:''},{studentId:'b',score:90,reason:'貢獻較多'}],publish:false,score:85,feedback:'回饋',expectedVersion:1}})
 await groups[1].trigger('click')
 expect(view.get('.report-missing-submission').text()).toContain('第二組');expect(view.get('.report-missing-submission').text()).toContain('尚未繳交')
 expect(view.find('.report-grading').exists()).toBe(false);expect(view.findComponent({name:'FilePreview'}).exists()).toBe(false)
 expect(api.mock.calls.filter(([,options])=>options?.method==='POST')).toHaveLength(0)
})
it('requires a reason when clearing an existing member override',async()=>{
 render();await flushPromises();await view.get('.report-adjustments').trigger('toggle');await view.get('input[aria-label="乙 個別成績"]').setValue('')
 expect(view.get('input[aria-label="乙 調整原因"]').attributes('required')).toBeDefined()
 expect(view.get('.report-adjustments').attributes('open')).toBeDefined()
})
it('preserves restricted assistant grading visibility',async()=>{
 render(['grading']);await flushPromises();expect(view.find('.report-grading').exists()).toBe(false)
 expect(api).not.toHaveBeenCalledWith('/reports/r/submissions');expect(view.text()).toContain('第二組')
})
it('requires complete saved drafts before offering whole assignment publication',async()=>{
 render();await flushPromises()
 expect(view.find('button[value="publish"]').exists()).toBe(false)
 expect(view.get('[data-testid="publish-all"]').attributes('disabled')).toBeDefined()
 expect(view.text()).toContain('尚有未繳交、退回或未評分的小組')
})
it('publishes all saved group versions together and preserves collapsed individual adjustments',async()=>{
 api.mockImplementation(async path=>path==='/reports/r'?structuredClone(report):path.endsWith('/roster')?[{groupId:'g1',submissionId:'s',name:'第一組',submitted:true}]:path.endsWith('/submissions')?[structuredClone(submission)]:undefined)
 render();await flushPromises()
 const details=view.get('.report-adjustments');expect(details.attributes('open')).toBeDefined();expect(details.get('summary').text()).toContain('已有 1 位個別調整')
 details.element.open=false
 await view.get('input[aria-label="第一組 小組基礎成績"]').setValue(85)
 expect(view.get('[data-testid="publish-all"]').attributes('disabled')).toBeDefined()
 await view.get('.report-grading').trigger('submit');await flushPromises()
 expect(api.mock.calls.find(([path])=>path.endsWith('/grade'))[1].body.adjustments[1]).toEqual({studentId:'b',score:90,reason:'貢獻較多'})
 await view.get('[data-testid="publish-all"]').trigger('click');await flushPromises()
 expect(api).toHaveBeenCalledWith('/reports/r/grades/publish',{method:'POST',body:{expectedVersion:1,submissions:[{id:'s',expectedVersion:1}]}})
})
it('keeps optional adjustments and contribution feedback collapsed for an unadjusted group',async()=>{
 const plain=structuredClone(submission);plain.members[1].score=null;plain.members[1].reason=''
 api.mockImplementation(async path=>path==='/reports/r'?{...structuredClone(report),evaluationMode:'consensus'}:path.endsWith('/roster')?[{groupId:'g1',submissionId:'s',name:'第一組',submitted:true}]:path.endsWith('/submissions')?[plain]:undefined)
 render();await flushPromises()
 expect(view.get('.report-adjustments').attributes('open')).toBeUndefined()
 expect(view.get('.report-feedback-details').attributes('open')).toBeUndefined()
 expect(view.get('.report-adjustments summary').text()).toContain('個別成績調整（選用）')
 expect(view.text()).toContain('全組同分')
})
it('keeps student results read only and displays only the published score received from the API',async()=>{
 api.mockImplementation(async path=>path==='/reports/r'?{...structuredClone(report),courseRole:'student'}:path.endsWith('/roster')?[{name:'第一組',submitted:true,late:false,dissolved:false}]:path.endsWith('/submissions')?[{...structuredClone(submission),score:72,publishedScore:72,feedback:'已發布評語',members:[{id:'a',name:'甲',isLeader:true,self:true},{id:'b',name:'乙',isLeader:false,self:false}]}]:undefined)
 render();await flushPromises();expect(view.text()).toContain('我的已發布成績：72');expect(view.find('.report-grading').exists()).toBe(false);expect(view.find('input[type="number"]').exists()).toBe(false)
})
it('retries a failed roster load without claiming that no groups submitted',async()=>{
 api.mockResolvedValueOnce(structuredClone(report)).mockRejectedValueOnce(new Error('無法載入名單'))
 render();await flushPromises();expect(view.get('[role="alert"]').text()).toContain('無法載入名單');expect(view.find('.report-roster-table').exists()).toBe(false)
 await view.get('button').trigger('click');await flushPromises();expect(view.get('.report-review-sidebar').text()).toContain('第二組')
})
