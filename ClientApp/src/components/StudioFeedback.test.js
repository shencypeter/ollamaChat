import {mount} from '@vue/test-utils'
import {describe,it,expect} from 'vitest'
import StudioFeedback from './StudioFeedback.vue'
describe('case text feedback',()=>{
 it('renders text safely without a grade or executable model HTML',()=>{const w=mount(StudioFeedback,{props:{feedback:{status:'completed',content:{summary:'<script>bad()</script>',supportedPoints:[],improvements:[],nextSteps:['核對原文']}},canAsk:true}});expect(w.text()).toContain('<script>bad()</script>');expect(w.find('script').exists()).toBe(false);expect(w.find('.score').exists()).toBe(false);expect(w.text()).toContain('文字回饋 · 不計分')})
 it('offers retry after timeout while explaining saved answers survive',async()=>{const w=mount(StudioFeedback,{props:{feedback:{status:'failed',errorCode:'timeout'},canAsk:true}});expect(w.get('[role="alert"]').text()).toContain('S3a');expect(w.text()).toContain('已儲存答案仍保留');await w.findAll('button').find(b=>b.text()==='重試 AI 回饋').trigger('click');expect(w.emitted('ask')).toHaveLength(1)})
 it('keeps teacher feedback read only',()=>{const w=mount(StudioFeedback,{props:{readOnly:true,feedback:{status:'completed',content:{summary:'文字',supportedPoints:[],improvements:[],nextSteps:['核對']}}}});expect(w.find('button').exists()).toBe(false)})
})
