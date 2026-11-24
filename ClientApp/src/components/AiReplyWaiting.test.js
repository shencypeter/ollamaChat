import {mount} from '@vue/test-utils'
import {nextTick} from 'vue'
import {afterEach,beforeEach,expect,it,vi} from 'vitest'
import AiReplyWaiting from './AiReplyWaiting.vue'

let wrapper,media,change
beforeEach(()=>{
 vi.useFakeTimers();media={matches:false,addEventListener:vi.fn((_,listener)=>{change=listener}),removeEventListener:vi.fn()};vi.stubGlobal('matchMedia',vi.fn(()=>media))
})
afterEach(()=>{wrapper?.unmount();wrapper=null;vi.useRealTimers();vi.unstubAllGlobals()})
it('keeps an interrupted reply in the card, stops progress and can restart cleanly',async()=>{
 wrapper=mount(AiReplyWaiting,{props:{hints:['正在閱讀','正在分類'],formal:true},slots:{failure:'<p>合成輸出上限，作答已保留。</p>'}})
 await vi.advanceTimersByTimeAsync(120);await wrapper.setProps({failed:true})
 expect(wrapper.get('[data-reply-failed]').attributes('role')).toBe('alert');expect(wrapper.text()).toContain('AI 回覆未完成');expect(wrapper.text()).toContain('合成輸出上限')
 expect(wrapper.find('[data-reply-loading],[data-reply-completed],.reply-spinner,.request-summary,.hint-controls,[data-action="latest-result"]').exists()).toBe(false);expect(vi.getTimerCount()).toBe(0)
 await wrapper.setProps({hints:['新的作答流程']});expect(vi.getTimerCount()).toBe(0);await wrapper.setProps({failed:false});expect(wrapper.find('[data-reply-loading]').exists()).toBe(true);expect(vi.getTimerCount()).toBe(1)
})
it('finishes inside the carousel with a result action, stops timers and can start another request',async()=>{
 wrapper=mount(AiReplyWaiting,{props:{hints:['正在閱讀','正在分類'],formal:true}})
 await vi.advanceTimersByTimeAsync(120)
 await wrapper.get('[data-action="pause-hints"]').trigger('click')
 await wrapper.setProps({completed:true,resultHref:'#latest-reply',resultLabel:'查看最新回饋（#4）'})
 expect(wrapper.find('[data-reply-loading]').exists()).toBe(false);expect(wrapper.get('[data-reply-completed]').text()).toContain('AI 助教已回覆')
 expect(wrapper.find('.reply-spinner').exists()).toBe(false);expect(wrapper.find('[data-action="pause-hints"]').exists()).toBe(false);expect(wrapper.find('[data-action="next-hint"]').exists()).toBe(false)
 expect(wrapper.get('.hint-indicators').attributes('aria-label')).toBe('AI 回覆已完成');expect(wrapper.findAll('[data-status-block].is-current')).toHaveLength(2)
 expect(vi.getTimerCount()).toBe(0)
 const result=wrapper.get('[data-action="latest-result"]');expect(result.attributes('href')).toBe('#latest-reply');expect(result.text()).toBe('查看最新回饋（#4）')
 await result.trigger('click');expect(wrapper.emitted('view-result')).toHaveLength(1)
 await wrapper.setProps({completed:false,resultHref:''});expect(wrapper.find('[data-reply-loading]').exists()).toBe(true);expect(wrapper.find('[data-action="latest-result"]').exists()).toBe(false);expect(vi.getTimerCount()).toBe(1)
})
it('types and rotates request requirements with a disclosure, and supports pause, next and cleanup',async()=>{
 wrapper=mount(AiReplyWaiting,{props:{hints:['第一項要求','第二項要求']}})
 expect(wrapper.text()).toContain('AI 助教正在回覆');expect(wrapper.text()).toContain('並非模型即時進度')
 expect(wrapper.find('.hint-count').exists()).toBe(false);expect(wrapper.findAll('[data-status-block]')).toHaveLength(2);expect(wrapper.findAll('[data-status-block].is-current')).toHaveLength(1)
 await vi.advanceTimersByTimeAsync(120);const initial=wrapper.get('[data-request-hint-text]').text();expect(initial.length).toBeGreaterThan(0);expect(initial.length).toBeLessThan(5)
 await wrapper.get('[data-action="pause-hints"]').trigger('click');await vi.advanceTimersByTimeAsync(5000);expect(wrapper.get('[data-request-hint-text]').text()).toBe(initial)
 await wrapper.get('[data-action="next-hint"]').trigger('click');expect(wrapper.get('[data-request-hint-text]').text()).toBe('第二項要求');expect(wrapper.findAll('[data-status-block]')[1].classes()).toContain('is-current')
 await wrapper.get('[data-action="pause-hints"]').trigger('click');await vi.advanceTimersByTimeAsync(2000);expect(wrapper.get('[data-current-request-hint]').attributes('data-current-request-hint')).toBe('第一項要求')
 wrapper.unmount();wrapper=null;expect(vi.getTimerCount()).toBe(0);expect(media.removeEventListener).toHaveBeenCalled()
})
it('respects reduced motion by revealing the whole hint without autoplay, and responds to preference changes',async()=>{
 media.matches=true;wrapper=mount(AiReplyWaiting,{props:{hints:['完整教學要求','下一項要求']}})
 await nextTick();expect(wrapper.get('[data-request-hint-text]').text()).toBe('完整教學要求');expect(vi.getTimerCount()).toBe(0)
 await vi.advanceTimersByTimeAsync(10000);expect(wrapper.get('[data-request-hint-text]').text()).toBe('完整教學要求')
 await wrapper.get('[data-action="next-hint"]').trigger('click');expect(wrapper.get('[data-request-hint-text]').text()).toBe('下一項要求')
 change({matches:false});await nextTick();expect(vi.getTimerCount()).toBe(1)
})
it('resets on new requirements and never interprets hint text as HTML',async()=>{
 wrapper=mount(AiReplyWaiting,{props:{hints:['第一組']}});await vi.advanceTimersByTimeAsync(100)
 await wrapper.setProps({hints:['<img src=x>😀']});await vi.advanceTimersByTimeAsync(1000)
 expect(wrapper.get('[data-request-hint-text]').text()).toBe('<img src=x>😀');expect(wrapper.find('img').exists()).toBe(false)
})
