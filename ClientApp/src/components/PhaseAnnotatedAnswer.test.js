import {mount} from '@vue/test-utils'
import {it,expect,afterEach} from 'vitest'
import PhaseAnnotatedAnswer from './PhaseAnnotatedAnswer.vue'
const classifications=[{code:'A1',label:'人口學資料',phase:'A'},{code:'A3',label:'健康行為',phase:'A'},{code:'A1a',label:'年齡',phase:'A'},{code:'A1b',label:'性別',phase:'A'},{code:'C5',label:'日常生活活動',phase:'C'}]
const annotations=[{annotationId:'age',start:2,end:5,text:'55歲',codes:['A1a','A1b'],explanation:'年齡描述 <img src=x>',status:'unsupported'},{annotationId:'adl',start:6,end:9,text:'ADL',codes:['C5'],explanation:'功能描述',status:'misplaced'}]
const content='  55歲 ADL😀\n'
let wrapper
const view=(props={})=>wrapper=mount(PhaseAnnotatedAnswer,{attachTo:document.body,props:{content,annotations,classifications,...props}})
afterEach(()=>wrapper?.unmount())
it('switches to inline code references with safe original text and keyboard/touch inspection, then restores underlines',async()=>{
 const v=view();await v.get('[data-annotation-mode="badges"]').trigger('click')
 expect(v.get('[data-annotation-mode="badges"]').attributes('aria-pressed')).toBe('true')
 const badges=v.findAll('[data-annotation-reference]');expect(badges.map(b=>b.text())).toEqual(['A1a · A1b','C5'])
 expect(badges.flatMap(b=>b.findAll('[data-reference-phase]').map(code=>code.attributes('data-reference-phase')))).toEqual(['A','A','C'])
 const original=v.get('[data-annotated-text]').element.cloneNode(true);original.querySelectorAll('.annotation-reference').forEach(el=>el.remove());expect(original.textContent).toBe(content)
 expect(v.findAll('[data-annotation-segment]').every(span=>!span.attributes('role')&&span.element.style.backgroundImage==='')).toBe(true)
 await badges[0].trigger('focus');expect(v.get('[role="tooltip"]').text()).toContain('A1a — 年齡');expect(badges[0].attributes('aria-describedby')).toBe(v.get('[role="tooltip"]').attributes('id'));expect(v.find('img').exists()).toBe(false)
 await badges[0].trigger('click');await badges[0].trigger('keydown',{key:'Escape'});expect(v.find('[role="tooltip"]').exists()).toBe(false)
 await v.get('[data-annotation-mode="underline"]').trigger('click');expect(v.find('[data-annotation-reference]').exists()).toBe(false);expect(v.get('[data-annotated-text]').element.textContent).toBe(content);expect(v.get('[data-annotation-segment]').element.style.backgroundImage).toContain('linear-gradient')
})
it('separates adjacent codes of the same phase without changing the text or breaking identical-code continuity',async()=>{
 const text='受傷資訊病況嚴重程度',boundary=4
 const v=view({content:text,stage:'illnessCourse',classifications:[{code:'B1',label:'受傷資訊',phase:'B'},{code:'B2',label:'疾病嚴重度',phase:'B'}],annotations:[{...annotations[0],start:0,end:boundary,text:text.slice(0,boundary),codes:['B1']},{...annotations[1],start:boundary,end:text.length,text:text.slice(boundary),codes:['B2']}]})
 expect(v.get('[data-annotated-text]').element.textContent).toBe(text)
 const spans=v.findAll('[data-annotation-segment]');expect(spans).toHaveLength(2)
 expect(spans[0].element.style.backgroundSize).toBe('calc(100% - 3px) 2px');expect(spans[1].element.style.backgroundPosition).toContain('3px')
 await spans[1].trigger('focus');expect(v.get('[role="tooltip"]').text()).toContain('B2 — 疾病嚴重度')
 await v.setProps({annotations:[{...annotations[0],start:0,end:boundary,text:text.slice(0,boundary),codes:['B1']},{...annotations[1],start:boundary,end:text.length,text:text.slice(boundary),codes:['B1']}]})
 expect(v.findAll('[data-annotation-segment]').every(span=>span.element.style.backgroundSize==='calc(100% - 0px) 2px')).toBe(true)
})
it('renders the original read-only answer and hover/focus tooltip with all labels and clinical caveat safely',async()=>{
 const v=view();expect(v.get('[data-annotated-text]').element.textContent).toBe(content);expect(v.find('textarea').exists()).toBe(false)
 const trigger=v.get('[data-annotation-segment]');await trigger.trigger('mouseenter');expect(v.get('[role="tooltip"]').text()).toContain('A1a — 年齡');expect(v.get('[role="tooltip"]').text()).toContain('A1b — 性別');expect(v.get('[role="tooltip"]').text()).toContain('有待查證');expect(v.find('img').exists()).toBe(false)
 expect(v.text()).toContain('分類辨識不代表臨床正確');await trigger.trigger('focus');expect(trigger.attributes('aria-describedby')).toBe(v.get('[role="tooltip"]').attributes('id'))
 await trigger.trigger('keydown',{key:'Escape'});expect(v.find('[role="tooltip"]').exists()).toBe(false)
})
it('supports tap toggle, outside dismissal, legend emphasis and resets on answer version change',async()=>{
 const v=view(),trigger=v.get('[data-annotation-segment]');const legend=v.get('.annotation-legend');expect(legend.element.tagName).toBe('SECTION');expect(legend.find(':scope > summary').exists()).toBe(false);expect(legend.findAll('.phase-group')).toHaveLength(4);expect(v.get('[data-legend-code="A1"]').attributes('data-mentioned')).toBe('true');expect(v.get('[data-legend-code="A3"]').attributes('data-mentioned')).toBe('false');await v.get('[data-legend-code="A1"]').trigger('click');expect(v.findAll('.annotation-emphasized')).toHaveLength(1);await v.get('[data-legend-code="A1"]').trigger('click');await trigger.trigger('click');expect(v.find('[role="tooltip"]').exists()).toBe(true);await trigger.trigger('click');expect(v.find('[role="tooltip"]').exists()).toBe(false)
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
it('underlines every classified phase without recoloring the original while preserving current-stage and mixed-code caveats',async()=>{
 const v=view({stage:'functionalAssessment',annotations:[...annotations,{...annotations[0],annotationId:'mixed',codes:['C5'],status:'unverified'}]})
 const [mixed,current]=v.findAll('[data-annotation-segment]')
 expect(mixed.attributes('data-current-phase')).toBe('C');expect(mixed.attributes('data-underline-phases')).toBe('A C')
 expect(mixed.element.style.backgroundImage.match(/linear-gradient/g)).toHaveLength(2);expect(mixed.element.style.color).toBe('');expect(mixed.element.style.getPropertyValue('--annotation-highlight')).toBe('')
 expect(current.attributes('data-current-phase')).toBe('C');expect(current.attributes('data-underline-phases')).toBe('C');expect(current.element.style.backgroundImage).toContain('linear-gradient')
 await mixed.trigger('focus');expect(v.get('[role="tooltip"]').text()).toContain('目前階段相符不代表臨床正確');expect(v.get('[role="tooltip"]').text()).toContain('有待查證')
 await v.get('[data-annotation-mode="badges"]').trigger('click');expect(v.get('[data-annotation-reference]').findAll('[data-reference-phase]').map(code=>code.attributes('data-reference-phase'))).toEqual(['A','A','C']);await v.get('[data-annotation-mode="underline"]').trigger('click')
 expect(v.text()).toContain('目前階段：C 功能評估結果');expect(v.text()).toContain('原文統一以分類色底線標示')
 await v.setProps({stage:'raw'});expect(v.get('[data-annotation-segment]').attributes('data-current-phase')).toBe('');expect(v.get('[data-annotation-segment]').attributes('data-underline-phases')).toBe('A C')
})
