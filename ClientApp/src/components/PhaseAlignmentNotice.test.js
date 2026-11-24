import {mount} from '@vue/test-utils'
import {it,expect} from 'vitest'
import PhaseAlignmentNotice from './PhaseAlignmentNotice.vue'
const classifications=[{code:'A1a',phase:'A',label:'年齡'},{code:'B1',phase:'B',label:'疾病診斷'}]
const annotation=(start,end,text,codes,status='unverified')=>({annotationId:`${start}`,start,end,text,codes,status})
const props={content:'55歲病程',stage:'illnessCourse',classifications,annotations:[annotation(0,3,'55歲',['A1a'])]}
it('describes hidden marks accurately in none mode while retaining the phase check',()=>{
 const v=mount(PhaseAlignmentNotice,{props:{...props,displayMode:'none'}})
 expect(v.text()).toContain('原文標註已隱藏');expect(v.text()).not.toContain('彩色底線');expect(v.text()).not.toContain('編碼徽章');expect(v.text()).toContain('A 病前功能')
})
it('does not claim underlines are already visible before categories have been selected',()=>{
 const v=mount(PhaseAlignmentNotice,{props})
 expect(v.text()).toContain('原文模式（未顯示標註）');expect(v.text()).not.toContain('彩色底線');expect(v.text()).not.toContain('分類色底線')
})
it('adds an explicitly application-authored check when old AI praise has only foreign-stage classifications',()=>{
 const v=mount(PhaseAlignmentNotice,{props});expect(v.text()).toContain('階段核對（依 AI 分類標註）');expect(v.text()).toContain('系統依分類標註整理');expect(v.text()).toContain('目前階段：B 疾病病程');expect(v.text()).toContain('A 病前功能');expect(v.text()).toContain('請核對是否放錯階段');expect(v.text()).toContain('不代表臨床正確');expect(v.text()).not.toContain('答錯')
})
it('does not infer missing current-stage content from a partially annotated answer',()=>{
 const v=mount(PhaseAlignmentNotice,{props:{...props,content:'55歲，本次疾病病程持續追蹤。'}})
 expect(v.text()).toContain('請核對是否放錯階段');expect(v.text()).toContain('若它是主要作答');expect(v.text()).toContain('未標記內容不代表缺漏');expect(v.text()).toContain('必要背景引用可以保留');expect(v.text()).not.toContain('補充目前階段');expect(v.text()).not.toContain('未辨識到本階段')
})
it('allows legitimate cross-stage context and preserves stronger model misplaced status separately',async()=>{
 const v=mount(PhaseAlignmentNotice,{props:{...props,annotations:[...props.annotations,annotation(3,5,'病程',['B1'],'unsupported')]}})
 expect(v.text()).toContain('必要的跨階段背景可以保留');expect(v.text()).toContain('有待查證');expect(v.text()).not.toContain('請核對是否放錯階段')
 await v.setProps({annotations:[annotation(0,3,'55歲',['A1a'],'misplaced')]});expect(v.text()).toContain('AI 另標示階段可能不符');expect(v.text()).toContain('仍需核對原文與教學要求')
})
it('does not infer alignment from invalid spans, absent annotations or unknown stages and resets for another original',async()=>{
 const v=mount(PhaseAlignmentNotice,{props});await v.setProps({content:'另一版'});expect(v.find('[data-phase-alignment]').exists()).toBe(false)
 await v.setProps({...props,stage:'raw'});expect(v.find('[data-phase-alignment]').exists()).toBe(false)
 await v.setProps({...props,annotations:[]});expect(v.find('[data-phase-alignment]').exists()).toBe(false)
})
