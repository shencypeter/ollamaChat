import {mount,flushPromises} from '@vue/test-utils'
import {beforeEach,it,expect,vi} from 'vitest'
import CourseQuestions from './CourseQuestions.vue'
import {api} from '../api'
const route=vi.hoisted(()=>({query:{thread:'second'}}))
vi.mock('vue-router',()=>({useRoute:()=>route}))
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('../composables/notifications',()=>({markNotificationsRead:vi.fn()}))
beforeEach(()=>{api.mockReset();api.mockImplementation(async path=>path.includes('/replies')?{items:[]}:{items:[{id:'first',title:'第一串',question:'第一個內容'},{id:'second',title:'第二串',question:'首頁選擇的內容'}]})})
it('opens a dashboard-linked thread only from the authorized course result',async()=>{
 const view=mount(CourseQuestions,{props:{course:{id:'c'}}});await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/c/questions/second/replies?all=true');expect(view.text()).toContain('首頁選擇的內容');view.unmount()
})
it('does not request a thread outside the returned course threads',async()=>{
 route.query.thread='other-course';const view=mount(CourseQuestions,{props:{course:{id:'c'}}});await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/c/questions/first/replies?all=true');expect(api.mock.calls.some(([path])=>path.includes('other-course'))).toBe(false);view.unmount();route.query.thread='second'
})
