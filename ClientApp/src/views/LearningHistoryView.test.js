import {mount,flushPromises} from '@vue/test-utils'
import {beforeEach,it,expect,vi} from 'vitest'
import LearningHistoryView from './LearningHistoryView.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('../composables/experience',()=>({setCourseExperience:vi.fn()}))
const global={stubs:{RouterLink:{props:['to'],template:'<a><slot/></a>'}}}
beforeEach(()=>{api.mockReset();api.mockImplementation(async path=>path.endsWith('/courses')?[{id:'c',title:'課程',term:'115-1',required:1,submitted:0,pending:0}]:{items:[],total:0})})
it('provides cross-course and cross-term filters at personal level',async()=>{
 const view=mount(LearningHistoryView,{global});await flushPromises()
 expect(view.get('h1').text()).toBe('我的學習歷程');expect(view.find('select[aria-label="課程"]').exists()).toBe(true);expect(view.find('select[aria-label="學期"]').exists()).toBe(true);view.unmount()
})
it('keeps the course progress page scoped and hides cross-course controls',async()=>{
 const view=mount(LearningHistoryView,{props:{boundCourse:'c'},global});await flushPromises()
 expect(view.get('h1').text()).toBe('課程學習進度');expect(view.find('select[aria-label="課程"]').exists()).toBe(false);expect(view.find('select[aria-label="學期"]').exists()).toBe(false)
 expect(api.mock.calls.filter(([path])=>path.includes('?')).every(([path])=>path.includes('course=c'))).toBe(true);view.unmount()
})
