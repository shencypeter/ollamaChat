import {mount} from '@vue/test-utils'
import {describe,it,expect} from 'vitest'
import StudioFeedback from './StudioFeedback.vue'
describe('case text feedback',()=>{
 const answer={id:'answer-1',text:'原文目前需協助'}
 const annotations=[{annotationId:'ann-1',start:0,end:2,text:'原文',codes:['C5a'],explanation:'分類提示',status:'unverified'}]
 const classifiedFeedback={answerVersionId:'answer-1',status:'completed',content:{summary:'文字',supportedPoints:[],improvements:[],nextSteps:['核對'],annotations}}
 it('annotates only the exact saved answer while keeping unsaved editing separate',()=>{
  const w=mount(StudioFeedback,{props:{answer,feedback:classifiedFeedback,stale:true,unsaved:true,classifications:[{code:'C5a',phase:'C',label:'日常功能'}]},global:{stubs:{PhaseAnnotatedAnswer:{name:'PhaseAnnotatedAnswer',props:['content','annotations','classifications'],template:'<div class="annotated-stub">{{content}}</div>'}}}})
  const view=w.findComponent({name:'PhaseAnnotatedAnswer'})
  expect(view.exists()).toBe(true);expect(view.props('content')).toBe(answer.text);expect(view.props('annotations')).toEqual(annotations);expect(view.props('classifications')).toHaveLength(1)
  expect(w.text()).toContain('已儲存版本');expect(w.text()).toContain('未儲存修改')
 })
 it('does not apply annotations to a different answer revision or unfinished feedback',async()=>{
  const w=mount(StudioFeedback,{props:{answer:{...answer,id:'answer-2'},feedback:classifiedFeedback}})
  expect(w.findComponent({name:'PhaseAnnotatedAnswer'}).exists()).toBe(false)
  await w.setProps({answer,feedback:{...classifiedFeedback,status:'pending'}})
  expect(w.findComponent({name:'PhaseAnnotatedAnswer'}).exists()).toBe(false)
 })
 it('shows a saved-version stage check beside unchanged feedback and never reuses it for another revision',async()=>{
  const w=mount(StudioFeedback,{props:{answer:{...answer,stage:'illnessCourse'},feedback:classifiedFeedback,classifications:[{code:'C5a',phase:'C',label:'日常功能'}]}})
  expect(w.getComponent({name:'PhaseAnnotatedAnswer'}).props('stage')).toBe('illnessCourse')
  expect(w.get('[data-phase-alignment]').text()).toContain('目前階段：B 疾病病程');expect(w.get('[data-phase-alignment]').text()).toContain('C 功能評估結果')
  expect(w.text()).toContain('文字');await w.setProps({answer:{...answer,id:'another',stage:'premorbid'}});expect(w.find('[data-phase-alignment]').exists()).toBe(false)
 })
 it('keeps old feedback without annotations compatible and uses the professor feedback labels',()=>{
  const {annotations:_,...content}=classifiedFeedback.content
  const w=mount(StudioFeedback,{props:{answer,feedback:{...classifiedFeedback,content}}})
  expect(w.findComponent({name:'PhaseAnnotatedAnswer'}).exists()).toBe(false)
  for(const label of ['已掌握的部分','可能遺漏的資料','需要更具體的地方','可能分類錯置的內容','修改建議'])expect(w.text()).toContain(label)
 })
 it('renders text safely without a grade or executable model HTML',()=>{const w=mount(StudioFeedback,{props:{feedback:{status:'completed',content:{summary:'<script>bad()</script>',supportedPoints:[],improvements:[],nextSteps:['核對原文']}},canAsk:true}});expect(w.text()).toContain('<script>bad()</script>');expect(w.find('script').exists()).toBe(false);expect(w.find('.score').exists()).toBe(false);expect(w.text()).toContain('文字回饋 · 不計分')})
 it('offers retry after timeout while explaining saved answers survive',async()=>{const w=mount(StudioFeedback,{props:{feedback:{status:'failed',errorCode:'timeout'},canAsk:true}});expect(w.get('[role="alert"]').text()).toContain('S3a');expect(w.text()).toContain('已儲存答案仍保留');await w.findAll('button').find(b=>b.text()==='重試 AI 回饋').trigger('click');expect(w.emitted('ask')).toHaveLength(1)})
 it('keeps teacher feedback read only',()=>{const w=mount(StudioFeedback,{props:{readOnly:true,feedback:{status:'completed',content:{summary:'文字',supportedPoints:[],improvements:[],nextSteps:['核對']}}}});expect(w.find('button').exists()).toBe(false)})
})
