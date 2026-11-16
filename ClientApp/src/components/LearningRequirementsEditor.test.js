import {mount,flushPromises} from '@vue/test-utils'
import {it,expect,vi} from 'vitest'
import LearningRequirementsEditor from './LearningRequirementsEditor.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
it('offers only videos for new gates and preserves an existing static gate without seconds editing',async()=>{
 api.mockResolvedValue([{id:'video',title:'影片',learningType:'video/mp4'},{id:'old',title:'舊講義',learningType:null},{id:'new',title:'新講義',learningType:'application/pdf'}])
 const wrapper=mount(LearningRequirementsEditor,{props:{courseId:'c',modelValue:[{resourceId:'old',minimumSeconds:120}]}});await flushPromises()
 expect(wrapper.text()).toContain('影片');expect(wrapper.text()).toContain('舊講義');expect(wrapper.text()).not.toContain('新講義')
 expect(wrapper.text()).toContain('此舊教材門檻以點閱確認');expect(wrapper.text()).toContain('120 秒');expect(wrapper.find('input[type=number]').exists()).toBe(false)
 await wrapper.find('input[type=checkbox]').setValue(true)
 expect(wrapper.emitted('update:modelValue')[0][0]).toEqual([{resourceId:'old',minimumSeconds:120},{resourceId:'video',minimumSeconds:60}])
})
