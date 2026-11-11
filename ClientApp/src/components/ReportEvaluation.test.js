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
