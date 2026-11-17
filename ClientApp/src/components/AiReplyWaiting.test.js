import {mount} from '@vue/test-utils'
import {nextTick} from 'vue'
import {afterEach,beforeEach,expect,it,vi} from 'vitest'
import AiReplyWaiting from './AiReplyWaiting.vue'

let wrapper,media,change
beforeEach(()=>{
 vi.useFakeTimers();media={matches:false,addEventListener:vi.fn((_,listener)=>{change=listener}),removeEventListener:vi.fn()};vi.stubGlobal('matchMedia',vi.fn(()=>media))
})
afterEach(()=>{wrapper?.unmount();wrapper=null;vi.useRealTimers();vi.unstubAllGlobals()})
it('types and rotates request requirements with a disclosure, and supports pause, next and cleanup',async()=>{
 wrapper=mount(AiReplyWaiting,{props:{hints:['第一項要求','第二項要求']}})
 expect(wrapper.text()).toContain('AI 助教正在回覆');expect(wrapper.text()).toContain('並非模型即時進度')
 await vi.advanceTimersByTimeAsync(120);const initial=wrapper.get('[data-request-hint-text]').text();expect(initial.length).toBeGreaterThan(0);expect(initial.length).toBeLessThan(5)
 await wrapper.get('[data-action="pause-hints"]').trigger('click');await vi.advanceTimersByTimeAsync(5000);expect(wrapper.get('[data-request-hint-text]').text()).toBe(initial)
 await wrapper.get('[data-action="next-hint"]').trigger('click');expect(wrapper.get('[data-request-hint-text]').text()).toBe('第二項要求')
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
