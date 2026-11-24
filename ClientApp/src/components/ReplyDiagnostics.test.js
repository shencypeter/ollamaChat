import {mount} from '@vue/test-utils'
import {afterEach,it,expect,vi} from 'vitest'
import ReplyDiagnostics from './ReplyDiagnostics.vue'
let wrapper
afterEach(()=>{wrapper?.unmount();vi.restoreAllMocks()})
it('shows only performance to students and describes its saved model on keyboard focus',async()=>{
 wrapper=mount(ReplyDiagnostics,{attachTo:document.body,props:{modelName:'saved-model'}})
 expect(wrapper.findAll('button')).toHaveLength(1)
 const button=wrapper.get('[data-action="performance"]');await button.trigger('focus')
 expect(wrapper.get('[role="tooltip"]').text()).toContain('saved-model');expect(button.attributes('aria-describedby')).toBe(wrapper.get('[role="tooltip"]').attributes('id'))
 document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));await wrapper.vm.$nextTick();expect(wrapper.find('[role="tooltip"]').exists()).toBe(false)
 await button.trigger('click');expect(wrapper.emitted('performance')).toHaveLength(1)
})
it('keeps request access conditional and clears a visible request tooltip after permissions change',async()=>{
 wrapper=mount(ReplyDiagnostics,{attachTo:document.body,props:{modelName:'staff-model',canInspectRequests:true}})
 const request=wrapper.get('[data-action="request-inspection"]');await request.trigger('mouseenter');expect(wrapper.get('[role="tooltip"]').text()).toContain('Ollama 請求')
 await request.trigger('click');expect(wrapper.emitted('request')).toHaveLength(1);expect(wrapper.find('[role="tooltip"]').exists()).toBe(false)
 await request.trigger('focus');await wrapper.setProps({canInspectRequests:false});expect(wrapper.find('[data-action="request-inspection"]').exists()).toBe(false);expect(wrapper.find('[role="tooltip"]').exists()).toBe(false)
})
it('does not guess a model for older responses without model metadata',async()=>{
 wrapper=mount(ReplyDiagnostics,{attachTo:document.body})
 await wrapper.get('[data-action="performance"]').trigger('focus');expect(wrapper.get('[role="tooltip"]').text()).toContain('此回覆未記錄模型')
 await wrapper.setProps({modelName:'new-model'});expect(wrapper.find('[role="tooltip"]').exists()).toBe(false)
})
it('keeps an expanded tooltip clear of its icon after a short viewport grows',async()=>{
 let height=200
 vi.spyOn(window,'innerWidth','get').mockReturnValue(390);vi.spyOn(window,'innerHeight','get').mockImplementation(()=>height)
 wrapper=mount(ReplyDiagnostics,{attachTo:document.body,props:{modelName:'saved-model'}})
 const button=wrapper.get('[data-action="performance"]')
 vi.spyOn(button.element,'getBoundingClientRect').mockImplementation(()=>({left:300,right:340,top:height-120,bottom:height-76,height:44,width:40}))
 await button.trigger('focus')
 const tooltip=wrapper.get('[role="tooltip"]')
 vi.spyOn(tooltip.element,'getBoundingClientRect').mockImplementation(()=>({height:Math.min(120,parseFloat(tooltip.element.style.maxHeight))}))
 window.dispatchEvent(new Event('resize'));await wrapper.vm.$nextTick();await wrapper.vm.$nextTick()
 height=1000;window.dispatchEvent(new Event('resize'));await wrapper.vm.$nextTick();await wrapper.vm.$nextTick()
 expect(parseFloat(tooltip.element.style.top)+tooltip.element.getBoundingClientRect().height).toBeLessThanOrEqual(button.element.getBoundingClientRect().top-10)
})
