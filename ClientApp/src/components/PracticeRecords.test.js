import {mount,flushPromises} from '@vue/test-utils'
import {it,expect,vi} from 'vitest'
import PracticeRecords from './PracticeRecords.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({onBeforeRouteLeave:vi.fn(),onBeforeRouteUpdate:vi.fn()}))
const row={kind:'quiz',id:'snapshot',title:'Synthetic',maxScore:100,weight:1,submittedAt:'2026-10-30',score:null,feedback:'',version:0,expectedGradeId:null}
it('reads practice results without allowing grading in student mode',async()=>{
 api.mockReset();api.mockResolvedValue({canGrade:false,records:[{...row,score:75,weight:9}],average:75});const w=mount(PracticeRecords,{props:{course:{id:'c'}}});await flushPromises();expect(w.text()).toContain('我的練習成績');expect(w.text()).toContain('75.00');expect(w.text()).toContain('等比平均');expect(w.text()).not.toMatch(/權重|加權/);expect(w.text()).toContain('得分 75 / 100');expect(w.find('form').exists()).toBe(false);w.unmount()
})
it('saves independent practice grades with original grading identity',async()=>{
 api.mockReset();api.mockResolvedValue({canGrade:true,records:[row],average:null});const w=mount(PracticeRecords,{props:{course:{id:'c',status:'active'}}});await flushPromises();await w.get('input').setValue('80');await w.get('textarea').setValue('Synthetic feedback');await w.get('form').trigger('submit');await flushPromises();expect(api).toHaveBeenCalledWith('/courses/c/practice/quiz/snapshot/grade',{method:'PUT',body:{score:80,essayScores:{},feedback:'Synthetic feedback',expectedVersion:0,expectedGradeId:null}});expect(w.text()).toContain('正式成績不受影響');w.unmount()
})
