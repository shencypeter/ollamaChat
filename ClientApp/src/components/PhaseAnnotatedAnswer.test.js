import {mount} from '@vue/test-utils'
import {it,expect,afterEach} from 'vitest'
import PhaseAnnotatedAnswer from './PhaseAnnotatedAnswer.vue'
const classifications=[{code:'A1a',label:'年齡',phase:'A'},{code:'A1b',label:'性別',phase:'A'},{code:'C5',label:'日常生活活動',phase:'C'}]
const annotations=[{annotationId:'age',start:2,end:5,text:'55歲',codes:['A1a','A1b'],explanation:'年齡描述 <img src=x>',status:'unsupported'},{annotationId:'adl',start:6,end:9,text:'ADL',codes:['C5'],explanation:'功能描述',status:'misplaced'}]
const content='  55歲 ADL😀\n'
let wrapper
const view=(props={})=>wrapper=mount(PhaseAnnotatedAnswer,{attachTo:document.body,props:{content,annotations,classifications,...props}})
afterEach(()=>wrapper?.unmount())
it('renders the original read-only answer and hover/focus tooltip with all labels and clinical caveat safely',async()=>{
 const v=view();expect(v.get('[data-annotated-text]').element.textContent).toBe(content);expect(v.find('textarea').exists()).toBe(false)
 const trigger=v.get('[data-annotation-segment]');await trigger.trigger('mouseenter');expect(v.get('[role="tooltip"]').text()).toContain('A1a — 年齡');expect(v.get('[role="tooltip"]').text()).toContain('A1b — 性別');expect(v.get('[role="tooltip"]').text()).toContain('有待查證');expect(v.find('img').exists()).toBe(false)
 expect(v.text()).toContain('分類辨識不代表臨床正確');await trigger.trigger('focus');expect(trigger.attributes('aria-describedby')).toBe(v.get('[role="tooltip"]').attributes('id'))
 await trigger.trigger('keydown',{key:'Escape'});expect(v.find('[role="tooltip"]').exists()).toBe(false)
})
it('supports tap toggle, outside dismissal, legend emphasis and resets on answer version change',async()=>{
 const v=view(),trigger=v.get('[data-annotation-segment]');await trigger.trigger('click');expect(v.find('[role="tooltip"]').exists()).toBe(true);await trigger.trigger('click');expect(v.find('[role="tooltip"]').exists()).toBe(false)
 await trigger.trigger('click');document.body.dispatchEvent(new Event('pointerdown',{bubbles:true}));await v.vm.$nextTick();expect(v.find('[role="tooltip"]').exists()).toBe(false)
 await v.get('[data-legend-code="C5"]').trigger('click');expect(v.get('[data-legend-code="C5"]').attributes('aria-pressed')).toBe('true');expect(v.findAll('.annotation-muted')).toHaveLength(1)
 await v.get('[data-annotation-segment]').trigger('click');await v.setProps({content:'另一版',annotations:[]});expect(v.find('[role="tooltip"]').exists()).toBe(false);expect(v.find('[data-annotation-segment]').exists()).toBe(false);expect(v.get('[data-annotated-text]').element.textContent).toBe('另一版')
})
it('does not invent underlines or show an empty legend for legacy or invalid annotations',()=>{
 const v=view({annotations:[]});expect(v.find('[data-annotation-segment]').exists()).toBe(false);expect(v.find('.annotation-legend').exists()).toBe(false);expect(v.get('[data-annotated-text]').element.textContent).toBe(content)
})
it('moves keyboard inspection to the next segment even after pinning a tooltip and keeps misplaced status',async()=>{
 const v=view(),triggers=v.findAll('[data-annotation-segment]');await triggers[0].trigger('click');await triggers[1].trigger('focus')
 expect(v.get('[role="tooltip"]').text()).toContain('C5 — 日常生活活動');expect(v.get('[role="tooltip"]').text()).toContain('階段可能不符');expect(v.get('[role="tooltip"]').text()).not.toContain('A1a — 年齡')
})
it('keeps keyboard tooltip visible during browser focus scrolling',async()=>{
 const v=view();await v.get('[data-annotation-segment]').trigger('focus');document.body.dispatchEvent(new Event('scroll'));await v.vm.$nextTick();expect(v.find('[role="tooltip"]').exists()).toBe(true)
})
it('highlights current-stage classifications and underlines other phases while preserving unsupported and mixed-code caveats',async()=>{
 const v=view({stage:'functionalAssessment',annotations:[...annotations,{...annotations[0],annotationId:'mixed',codes:['C5'],status:'unverified'}]})
 const [mixed,current]=v.findAll('[data-annotation-segment]')
 expect(mixed.attributes('data-current-phase')).toBe('C');expect(mixed.attributes('data-underline-phases')).toBe('A')
 expect(mixed.element.style.backgroundImage).toContain('linear-gradient');expect(mixed.element.style.getPropertyValue('--annotation-highlight')).toContain('--current-phase-highlight')
 expect(current.attributes('data-current-phase')).toBe('C');expect(current.attributes('data-underline-phases')).toBe('');expect(current.element.style.backgroundImage).toBe('none')
 await mixed.trigger('focus');expect(v.get('[role="tooltip"]').text()).toContain('目前階段相符不代表臨床正確');expect(v.get('[role="tooltip"]').text()).toContain('有待查證')
 expect(v.text()).toContain('目前階段：C 功能評估結果');expect(v.text()).toContain('其他階段的分類以彩色底線標示')
 await v.setProps({stage:'raw'});expect(v.get('[data-annotation-segment]').attributes('data-current-phase')).toBe('');expect(v.get('[data-annotation-segment]').attributes('data-underline-phases')).toBe('A C')
})
