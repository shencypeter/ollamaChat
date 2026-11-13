import {flushPromises,mount} from '@vue/test-utils'
import {beforeEach,expect,it,vi} from 'vitest'
import GradebookPanel from './GradebookPanel.vue'
import {api} from '../api'
import {experience} from '../composables/experience'
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({onBeforeRouteLeave:vi.fn(),onBeforeRouteUpdate:vi.fn()}))
const items=[{id:'a',title:'PDF 報告',category:'assignment',sourceReportId:'r',maxScore:50,weight:1},{id:'q',title:'觀念測驗',category:'quiz',sourceAssignmentId:'q1',maxScore:100,weight:3}]
const curriculum={units:[{id:'u1',name:'基礎單元'},{id:'u2',name:'進階單元'}],activities:[{id:'r',kind:'report',unitId:'u1'},{id:'q1',kind:'quiz',unitId:'u2'}]}
function data(staff=true){return {staff,items,students:[{id:'s',displayName:'同學甲',studentNumber:'001'}],scores:[{studentId:'s',itemId:'a',score:0,publishedScore:0}],averages:[{studentId:'s',average:0}],itemAverages:[],classAverage:0,gradedStudentCount:1,finals:[],classAdjustment:0,canPublishFinal:staff}}
function render(staff=true,course={}){api.mockImplementation(async path=>path.endsWith('/curriculum')?curriculum:data(staff));return mount(GradebookPanel,{props:{course:{id:'c',status:'active',...course}},global:{stubs:{RouterLink:{props:['to'],template:'<a :href="to"><slot /></a>'}}}})}
beforeEach(()=>{api.mockReset();experience.student=false})
it('routes paper exam publication to the whole-class workspace while preserving single-student draft saves',async()=>{
 const payload={...data(),items:[{id:'mid',title:'期中筆試',category:'midterm',maxScore:100,version:1}],scores:[]}
 api.mockImplementation(async path=>path.endsWith('/curriculum')?curriculum:payload)
 const view=mount(GradebookPanel,{props:{course:{id:'c',status:'active'}},global:{stubs:{RouterLink:{props:['to'],template:'<a :href="to"><slot /></a>'}}}});await flushPromises()
 expect(view.findAll('button').some(button=>button.text()==='發布得分')).toBe(false)
 expect(view.get('a[href="/courses/c?section=assessments"]').text()).toContain('全班')
 expect(view.findAll('button').some(button=>button.text()==='儲存得分草稿')).toBe(true)
 view.unmount()
})
it('preserves a paper exam schedule when its title is edited in the general gradebook',async()=>{
 const scheduledAt='2026-11-15T02:00:00Z',payload={...data(),items:[{id:'mid',title:'期中筆試',category:'midterm',maxScore:100,version:2,scheduledAt}],scores:[]}
 api.mockImplementation(async path=>path.endsWith('/curriculum')?curriculum:payload)
 const view=mount(GradebookPanel,{props:{course:{id:'c',status:'active'}},global:{stubs:{RouterLink:true}}});await flushPromises()
 await view.findAll('button').find(button=>button.text()==='編輯項目').trigger('click')
 await view.get('form.question-editor input').setValue('期中紙本筆試');await view.get('form.question-editor').trigger('submit');await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/c/gradebook/items/mid',{method:'PUT',body:{id:'mid',title:'期中紙本筆試',category:'midterm',maxScore:100,expectedVersion:2,scheduledAt}})
 view.unmount()
})
it('removes course weights and linked-item editors while keeping manual maximum scores editable',async()=>{
 const payload=data();payload.items=[...items,{id:'manual',title:'觀察紀錄',category:'other',maxScore:50,weight:7,version:3}]
 api.mockImplementation(async path=>path.endsWith('/curriculum')?curriculum:payload)
 const view=mount(GradebookPanel,{props:{course:{id:'c',status:'active'}},global:{stubs:{RouterLink:{props:['to'],template:'<a :href="to"><slot /></a>'}}}});await flushPromises()
 expect(view.text()).not.toMatch(/權重|加權/)
 expect(view.text()).toContain('等比平均')
 const editors=view.findAll('button').filter(button=>button.text()==='編輯項目')
 expect(editors).toHaveLength(1)
 expect(editors[0].element.closest('tr').textContent).toContain('觀察紀錄')
 expect(view.get('a[href="/courses/c/reports/r"]').exists()).toBe(true)
 await editors[0].trigger('click')
 const form=view.get('form.question-editor')
 expect(form.findAll('input[type="number"]')).toHaveLength(1)
 await form.get('input[type="number"]').setValue('80')
 await form.trigger('submit');await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/c/gradebook/items/manual',{method:'PUT',body:{id:'manual',title:'觀察紀錄',category:'other',maxScore:80,expectedVersion:3}})
 view.unmount()
})
it('links ordinary online assignments to an assignment route rather than the quiz-only route',async()=>{
 const book=data(false);book.items=[{id:'online',title:'一般線上作業',category:'assignment',sourceAssignmentId:'online-source',maxScore:100,weight:1}]
 api.mockImplementation(async path=>path.endsWith('/curriculum')?{units:[],activities:[{id:'online-source',kind:'assignment',unitId:null}]}:book)
 const view=mount(GradebookPanel,{props:{course:{id:'c',status:'active'}},global:{stubs:{RouterLink:{props:['to'],template:'<a :href="to"><slot /></a>'}}}});await flushPromises()
 expect(view.get('a[href="/courses/c/assignments/online-source"]').text()).toBe('一般線上作業');expect(view.find('a[href="/courses/c/quizzes/online-source"]').exists()).toBe(false);view.unmount()
})
it('defaults staff to five fixed summary columns, with all original management progressively disclosed',async()=>{
 const view=render();await flushPromises()
 expect(view.get('[data-testid="grade-overview"] thead').findAll('th').map(x=>x.text())).toEqual(['學號／姓名','作業平均','測驗平均','平時平均','正式考試摘要'])
 expect(view.get('[data-testid="grade-overview"]').text()).toContain('0.00')
 expect(view.find('[data-testid="grade-details"]').exists()).toBe(false)
 expect(view.find('details.grade-management').exists()).toBe(true)
 expect(view.get('details.grade-management').attributes('open')).toBeUndefined()
 await view.get('button[data-view="details"]').trigger('click')
 expect(view.find('[data-testid="grade-details"]').exists()).toBe(true)
 expect(view.get('[data-testid="grade-details"] thead').findAll('th').map(x=>x.text())).toEqual(['學號／姓名','單元','活動','類型','繳交狀態','滿分','得分','評分狀態'])
 expect(view.get('[data-testid="grade-details"]').text()).toContain('前往原評量查看')
 await view.get('select[aria-label="成績單元"]').setValue('u1')
 expect(view.get('[data-testid="grade-details"]').text()).toContain('PDF 報告')
 expect(view.get('[data-testid="grade-details"]').text()).not.toContain('觀念測驗')
 view.unmount()
})
it('paginates vertical detail rows and resets the page when filters change',async()=>{
 const payload={...data(),students:Array.from({length:30},(_,i)=>({id:`s${i}`,studentNumber:String(i+1),displayName:`測試同學 ${i+1}`}))}
 api.mockImplementation(async path=>path.endsWith('/curriculum')?curriculum:payload)
 const view=mount(GradebookPanel,{props:{course:{id:'c',status:'active'}},global:{stubs:{RouterLink:true}}});await flushPromises()
 await view.get('button[data-view="details"]').trigger('click')
 expect(view.findAll('[data-testid="grade-details"] tbody tr')).toHaveLength(25)
 await view.get('button[aria-label="下一頁成績"]').trigger('click')
 expect(view.get('[data-testid="grade-details"] tbody').text()).toContain('測試同學 13')
 await view.get('select[aria-label="每頁成績筆數"]').setValue('50')
 expect(view.findAll('[data-testid="grade-details"] tbody tr')).toHaveLength(50)
 await view.get('select[aria-label="成績單元"]').setValue('u1')
 expect(view.findAll('[data-testid="grade-details"] tbody tr')).toHaveLength(30)
 expect(view.get('button[aria-label="上一頁成績"]').attributes('disabled')).toBeDefined()
 view.unmount()
})
it('shows students one grade table with actual units, zero and unreleased status, without staff controls',async()=>{
 const view=render(false);await flushPromises()
 expect(view.findAll('table')).toHaveLength(1)
 const table=view.get('table')
 expect(table.text()).toContain('基礎單元')
 expect(table.text()).toContain('已發布')
 expect(table.text()).toContain('未發布')
 expect(table.text()).not.toContain('未繳交')
 expect(table.findAll('tbody tr')[0].text()).toContain('0')
 expect(view.find('details.grade-management').exists()).toBe(false)
 expect(view.findAll('button').some(button=>button.text()==='發布課程總成績')).toBe(false)
 await view.get('select[aria-label="成績類型"]').setValue('quiz')
 expect(view.findAll('tbody tr')).toHaveLength(1)
 expect(view.get('tbody').text()).toContain('觀念測驗')
 view.unmount()
})
it('hides writes for restricted TAs and archived courses, and avoids all grade APIs in preview',async()=>{
 for(const course of [{taRestrictions:['grading']},{status:'archived'}]){const view=render(true,course);await flushPromises();expect(view.text()).not.toContain('新增評分項目');expect(view.find('input[type="number"]').exists()).toBe(false);view.unmount()}
 api.mockClear();experience.student=true
 const view=render(false,{canPractice:true});await flushPromises()
 expect(api).not.toHaveBeenCalled();expect(view.text()).toContain('沒有個人成績');view.unmount()
})
it('keeps grade viewing available if curriculum fails without inventing a unit',async()=>{
 api.mockImplementation(async path=>{if(path.endsWith('/curriculum'))throw new Error('offline');return data(false)})
 const view=mount(GradebookPanel,{props:{course:{id:'c',status:'active'}},global:{stubs:{RouterLink:true}}});await flushPromises()
 expect(view.get('[data-testid="my-grades"]').text()).toContain('單元資料未取得')
 expect(view.text()).toContain('單元資料載入失敗')
 expect(view.get('[data-testid="my-grades"]').text()).not.toContain('未歸類')
 view.unmount()
})
it('preserves versioned zero scoring, other unsaved drafts and full-course final math while filtering',async()=>{
 const payload={...data(),items:[{id:'manual',title:'線下觀察',category:'offline_assignment',maxScore:100,weight:1}],students:[{id:'s',displayName:'同學甲',studentNumber:'001'},{id:'s2',displayName:'同學乙',studentNumber:'002'}],scores:[{itemId:'manual',studentId:'s',score:10,publishedScore:null,version:3}],averages:[{studentId:'s',average:10}],classAdjustment:5,finals:[{studentId:'s',adjustment:2,publishedTotal:0}]}
 api.mockImplementation(async path=>path.endsWith('/curriculum')?curriculum:payload)
 const view=mount(GradebookPanel,{props:{course:{id:'c',status:'active'}},global:{stubs:{RouterLink:true}}});await flushPromises()
 await view.get('select[aria-label="成績類型"]').setValue('quiz')
 expect(view.get('.grade-management').text()).toContain('17.00')
 await view.get('input[aria-label="同學甲 得分"]').setValue('0')
 await view.get('input[aria-label="同學乙 得分"]').setValue('50')
 await view.findAll('button').find(button=>button.text()==='儲存得分草稿').trigger('click');await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/c/gradebook/items/manual/score',{method:'PUT',body:{publish:false,studentId:'s',score:0,expectedVersion:3}})
 expect(view.get('input[aria-label="同學乙 得分"]').element.value).toBe('50')
 expect(view.get('button[data-view="overview"]').attributes('aria-pressed')).toBe('true')
 view.unmount()
})
