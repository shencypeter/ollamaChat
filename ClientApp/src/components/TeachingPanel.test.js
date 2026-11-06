import {mount,flushPromises} from '@vue/test-utils'
import {it,expect,vi} from 'vitest'
import TeachingPanel from './TeachingPanel.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
async function setup(){
 let grade={id:'g1',score:90,feedback:'',essayScores:{e:60},criteria:[],graderName:'Synthetic'}
 api.mockReset();api.mockImplementation(async(path,options)=>{
  if(path.endsWith('/revisions'))return [{id:'r',revisionNo:1,answers:[]}]
  if(path.endsWith('/rubric'))return null
  if(path.endsWith('/quiz-grading'))return {objectiveScore:30,pendingEssays:0,marks:[{questionId:'o',questionType:'single',points:30,autoScore:30,correct:true},{questionId:'e',questionType:'essay',points:70}]}
  if(options?.method==='POST' && path.endsWith('/grades')){grade={...grade,id:'g2',score:options.body.score,essayScores:options.body.essayScores};return grade}
  if(path.endsWith('/release')){grade={...grade,releasedAt:'2026-10-30'};return grade}
  if(path.endsWith('/grades'))return [grade]
 })
 const w=mount(TeachingPanel,{props:{assignment:{id:'a',isQuiz:true,maxScore:100,questions:[{id:'e',questionType:'essay',sortOrder:2}]},submissionId:'s',staff:true}});await flushPromises();return w
}
it('confirms the newly edited total instead of releasing an older saved grade',async()=>{
 const w=await setup();await w.get('input[aria-label="人工分數"]').setValue('65');expect(w.text()).toContain('即時暫算：95 / 100')
 const confirm=vi.spyOn(window,'confirm').mockReturnValue(true);await w.findAll('button').find(b=>b.text()==='確認成績並寫入正式成績簿').trigger('click');await flushPromises()
 expect(api).toHaveBeenCalledWith('/assignments/a/revisions/r/grades',expect.objectContaining({body:expect.objectContaining({score:95,expectedGradeId:'g1',essayScores:{e:65}})}));expect(api).toHaveBeenCalledWith('/assignments/a/revisions/r/grades/g2/release',{method:'POST'});confirm.mockRestore();w.unmount()
})
it('does not write or publish any grade when teacher cancels confirmation',async()=>{
 const w=await setup();const confirm=vi.spyOn(window,'confirm').mockReturnValue(false);await w.get('input[aria-label="人工分數"]').setValue('65');await w.findAll('button').find(b=>b.text()==='確認成績並寫入正式成績簿').trigger('click');await flushPromises();expect(api.mock.calls.some(([,o])=>o?.method==='POST')).toBe(false);confirm.mockRestore();w.unmount()
})

