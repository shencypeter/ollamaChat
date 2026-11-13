import {flushPromises,mount} from '@vue/test-utils'
import {beforeEach,expect,it,vi} from 'vitest'
import OfflineExamGrades from './OfflineExamGrades.vue'
import {api} from '../api'
import {experience} from '../composables/experience'
vi.mock('../api',()=>({api:vi.fn()}))
const route=vi.hoisted(()=>({query:{}}))
vi.mock('vue-router',()=>({onBeforeRouteLeave:vi.fn(),onBeforeRouteUpdate:vi.fn(),useRoute:()=>route}))
function book(staff=true){return {staff,items:[{id:'exam',title:'期中筆試',category:'midterm',maxScore:100,version:2},{id:'ordinary',title:'一般作業',category:'offline_assignment',maxScore:100}],students:[{id:'a',studentNumber:'001',displayName:'甲'},{id:'b',studentNumber:'002',displayName:'乙'}],scores:[{itemId:'exam',studentId:'a',score:70,publishedScore:60,version:3}]}}
function render(payload=book(),course={}){api.mockImplementation(async()=>payload);return mount(OfflineExamGrades,{props:{course:{id:'c',status:'active',...course}}})}
beforeEach(()=>{api.mockReset();route.query={};experience.student=false;vi.stubGlobal('confirm',vi.fn(()=>true))})
it('opens an authorized agenda exam and ignores foreign exam ids without making another lookup',async()=>{
 const payload=book();payload.items.push({id:'final',title:'期末筆試',category:'final',maxScore:50,version:1})
 route.query={exam:'final'};let view=render(payload);await flushPromises();expect(view.get('h3').text()).toBe('期末筆試');view.unmount()
 api.mockClear();route.query={exam:'foreign'};view=render(payload);await flushPromises();expect(view.get('h3').text()).toBe('期中筆試');expect(api).toHaveBeenCalledTimes(1);view.unmount()
})
it('lists only paper midterm/final grade items and saves the full roster with blank distinct from zero',async()=>{
 const view=render();await flushPromises()
 expect(view.text()).toContain('期中筆試');expect(view.text()).not.toContain('一般作業')
 expect(view.get('input[aria-label="乙 得分"]').element.value).toBe('')
 expect(view.get('[data-action="publish"]').attributes('disabled')).toBeDefined()
 await view.get('input[aria-label="甲 得分"]').setValue('0')
 await view.get('[data-action="save"]').trigger('click');await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/c/gradebook/items/exam/scores',{method:'PUT',body:{expectedItemVersion:2,scores:[{studentId:'a',score:0,expectedVersion:3},{studentId:'b',score:null,expectedVersion:0}]}})
 expect(api.mock.calls.some(([path])=>path.endsWith('/score'))).toBe(false)
 view.unmount()
})
it('publishes one whole exam with versioned roster after all saved scores exist',async()=>{
 const payload=book();payload.scores.push({itemId:'exam',studentId:'b',score:0,publishedScore:null,version:1})
 const view=render(payload);await flushPromises()
 expect(view.get('[data-action="publish"]').attributes('disabled')).toBeUndefined()
 await view.get('[data-action="publish"]').trigger('click');await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/c/gradebook/items/exam/publish-scores',{method:'POST',body:{expectedItemVersion:2,scores:[{studentId:'a',expectedVersion:3},{studentId:'b',expectedVersion:1}]}})
 expect(confirm).toHaveBeenCalled()
 view.unmount()
})
it('preserves unsaved grades on conflict and blocks publication until drafts are saved',async()=>{
 const view=render();await flushPromises()
 api.mockImplementation(async(path,options)=>{if(options)throw new Error('得分已被修改');return book()})
 await view.get('input[aria-label="乙 得分"]').setValue('0')
 await view.get('[data-action="save"]').trigger('click');await flushPromises()
 expect(view.get('[role="alert"]').text()).toContain('已被修改')
 expect(view.get('input[aria-label="乙 得分"]').element.value).toBe('0')
 expect(view.get('[data-action="publish"]').attributes('disabled')).toBeDefined()
 view.unmount()
})
it('creates paper exam settings through existing grade items with optional exam time',async()=>{
 const view=render();await flushPromises()
 await view.get('[data-action="create"]').trigger('click')
 await view.get('input[aria-label="考試名稱"]').setValue('期末筆試')
 await view.get('select[aria-label="考試類型"]').setValue('final')
 await view.get('input[aria-label="考試滿分"]').setValue('50')
 await view.get('form').trigger('submit');await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/c/gradebook/items',{method:'POST',body:{title:'期末筆試',category:'final',maxScore:50,scheduledAt:null,expectedVersion:0}})
 view.unmount()
})
it('students see only their published marks and no teacher controls',async()=>{
 const payload=book(false);payload.students=[payload.students[0]];payload.scores=[{itemId:'exam',studentId:'a',score:0,publishedScore:0,version:1}]
 const view=render(payload);await flushPromises()
 expect(view.text()).toContain('已發布');expect(view.text()).not.toContain('甲');expect(view.text()).not.toContain('乙')
 expect(view.find('input').exists()).toBe(false);expect(view.find('[data-action="publish"]').exists()).toBe(false)
 view.unmount()
})
it('restricted TAs and archived courses are read only, preview sends no grade request',async()=>{
 for(const course of [{taRestrictions:['grading']},{status:'archived'}]){const view=render(book(),course);await flushPromises();expect(view.find('input').exists()).toBe(false);expect(view.find('[data-action="create"]').exists()).toBe(false);view.unmount()}
 api.mockClear();experience.student=true;const view=render(book(false),{canPractice:true});await flushPromises();expect(api).not.toHaveBeenCalled();view.unmount()
})
