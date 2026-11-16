import {mount,flushPromises} from '@vue/test-utils'
import {it,expect,vi,afterEach} from 'vitest'
import ShareResource from './ShareResource.vue'
afterEach(()=>vi.unstubAllGlobals())
it('shares the specific material and falls back to copying that same deep link',async()=>{
 const share=vi.fn().mockRejectedValue(new Error('unavailable')),writeText=vi.fn().mockResolvedValue()
 vi.stubGlobal('navigator',{share,clipboard:{writeText}})
 const wrapper=mount(ShareResource,{props:{courseId:'course',resourceId:'material',title:'教材'}})
 await wrapper.find('button').trigger('click');await flushPromises()
 const url=new URL('/courses/course/materials/material',window.location.origin).href
 expect(share).toHaveBeenCalledWith({title:'教材',url});expect(writeText).toHaveBeenCalledWith(url)
})
it('keeps the link selectable when copying is unavailable and treats share cancellation as cancellation',async()=>{
 const share=vi.fn().mockRejectedValue(Object.assign(new Error('cancelled'),{name:'AbortError'})),writeText=vi.fn().mockRejectedValue(new Error('denied'))
 vi.stubGlobal('navigator',{share,clipboard:{writeText}})
 const wrapper=mount(ShareResource,{props:{courseId:'c',resourceId:'r',title:'教材'}})
 await wrapper.find('button').trigger('click');await flushPromises();expect(writeText).not.toHaveBeenCalled()
 await wrapper.findAll('button')[1].trigger('click');await flushPromises()
 expect(wrapper.find('input').element.value).toBe(new URL('/courses/c/materials/r',window.location.origin).href)
})
