import {mount,flushPromises} from '@vue/test-utils'
import {it,expect,vi} from 'vitest'
import {api} from '../api'
import ReportEvaluation from './ReportEvaluation.vue'
vi.mock('../api',()=>({api:vi.fn()}))

it.each([
  [['agree','agree','agree'],'全組同意同分'],
  [['agree','review'],'需要教師確認'],
  [['agree','pending'],'尚未完成確認']
])('computes consensus independently of incomplete reviews',async(choices,status)=>{
  const members=['a','b','c'].map(id=>({id,name:id}))
  api.mockResolvedValue({reviews:[{reviewer:'a',subject:'b',ratings:[5],comment:''}],votes:choices.map((choice,i)=>({choice,voter:members[i].id}))})
  const w=mount(ReportEvaluation,{props:{base:'/reports/1',submission:{id:'s',members},report:{evaluationMode:'consensus',dimensions:['合作']},staff:true}})
  await flushPromises()
  expect(w.text()).toContain(status)
  expect(w.text()).toContain('尚未完成：a、b、c')
  expect(w.text()).toContain('合作 5.0')
  w.unmount()
})

it('retries failed feedback loading without presenting an empty completion summary',async()=>{
 api.mockRejectedValueOnce(new Error('無法讀取回覆')).mockResolvedValueOnce({reviews:[],votes:[]})
 const w=mount(ReportEvaluation,{props:{base:'/reports/1',submission:{id:'s',members:[{id:'a',name:'甲'}]},report:{evaluationMode:'simple',dimensions:['合作']},staff:true}})
 await flushPromises();expect(w.get('[role="alert"]').text()).toContain('無法讀取回覆');expect(w.text()).not.toContain('尚未完成：')
 await w.get('button[aria-label="重新載入組員回饋"]').trigger('click');await flushPromises()
 expect(w.text()).toContain('單人小組');expect(w.text()).toContain('不會自動換算或改動成績');w.unmount()
})

it('loads the newly selected submission and saves only the current student feedback',async()=>{
 api.mockResolvedValue({reviews:[],votes:[]})
 const report={evaluationMode:'simple',dimensions:['合作']}
 const w=mount(ReportEvaluation,{props:{base:'/reports/1',submission:{id:'s1',members:[{id:'a',name:'甲',self:true},{id:'b',name:'乙',self:false}]},report,staff:false}})
 await flushPromises();await w.get('select').setValue(4);await w.get('form').trigger('submit');await flushPromises()
 expect(api).toHaveBeenCalledWith('/reports/1/submissions/s1/reviews/b',{method:'PUT',body:{ratings:[4],comment:''}})
 await w.setProps({submission:{id:'s2',members:[{id:'a',name:'甲',self:true},{id:'c',name:'丙',self:false}]}});await flushPromises()
 expect(api).toHaveBeenCalledWith('/reports/1/submissions/s2/evaluation');expect(w.get('legend').text()).toBe('丙');w.unmount()
})
