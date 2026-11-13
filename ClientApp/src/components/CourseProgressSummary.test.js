import {mount,flushPromises} from '@vue/test-utils'
import {beforeEach,it,expect,vi} from 'vitest'
import CourseProgressSummary from './CourseProgressSummary.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
const global={stubs:{RouterLink:{template:'<a><slot/></a>'}}}
beforeEach(()=>{api.mockReset()})
it('shows only the current course student progress including a real zero',async()=>{
 api.mockResolvedValue([{id:'other',title:'別的課程',submitted:99},{id:'c',required:4,submitted:2,pending:1,publishedFinal:0}])
 const v=mount(CourseProgressSummary,{props:{course:{id:'c',courseRole:'student'}},global});await flushPromises()
 expect(v.text()).toContain('2 / 4');expect(v.text()).toContain('1 份待公布');expect(v.text()).not.toContain('99');v.unmount()
})
it('does not request statistics for restricted TAs or personal data for preview',async()=>{
 const v=mount(CourseProgressSummary,{props:{course:{id:'c',courseRole:'ta',taRestrictions:['statistics']}},global});await flushPromises();expect(api).not.toHaveBeenCalled();v.unmount()
 const p=mount(CourseProgressSummary,{props:{course:{id:'c',courseRole:'student',canPractice:true}},global});await flushPromises();expect(api).not.toHaveBeenCalled();p.unmount()
})
it('loads teacher statistics even when the account is capable of student preview',async()=>{
 api.mockResolvedValue({enrolled:12,submitters:8,submissions:20})
 const v=mount(CourseProgressSummary,{props:{course:{id:'c',courseRole:'teacher',canPractice:true}},global});await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/c/statistics');expect(v.text()).toContain('20');v.unmount()
})
