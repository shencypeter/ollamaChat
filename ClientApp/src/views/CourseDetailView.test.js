import {mount,flushPromises} from '@vue/test-utils'
import {beforeEach,it,expect,vi} from 'vitest'
import CourseDetailView from './CourseDetailView.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({useRoute:()=>({params:{courseId:'c'},path:'/courses/c',fullPath:'/courses/c?section=settings',query:{section:'settings'}}),useRouter:()=>({replace:vi.fn()})}))
beforeEach(()=>{api.mockReset()})
const course={id:'c',courseRole:'teacher',isOwner:true,status:'active',caseAnalysisEnabled:false}
it('lets only the owner enable case analysis and reloads the persisted course setting',async()=>{
 api.mockResolvedValue({...course})
 const v=mount(CourseDetailView,{global:{stubs:{RouterLink:true}}});await flushPromises()
 api.mockImplementation(async(p,o)=>o?null:{...course,caseAnalysisEnabled:true})
 await v.get('input[aria-label="啟用個案分析 AI 助教"]').setValue(true);await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/c/case-analysis/settings',{method:'PUT',body:{enabled:true}})
 expect(v.get('input[aria-label="啟用個案分析 AI 助教"]').element.checked).toBe(true);v.unmount()
})
it('does not expose the owner setting to teaching assistants',async()=>{
 api.mockResolvedValue({...course,courseRole:'ta',isOwner:false})
 const v=mount(CourseDetailView,{global:{stubs:{RouterLink:true}}});await flushPromises()
 expect(v.find('input[aria-label="啟用個案分析 AI 助教"]').exists()).toBe(false);v.unmount()
})
