import {mount,flushPromises} from '@vue/test-utils'
import {it,expect,vi,beforeEach} from 'vitest'
import ResourceTranscript from './ResourceTranscript.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({onBeforeRouteLeave:vi.fn(),onBeforeRouteUpdate:vi.fn()}))
beforeEach(()=>{api.mockReset();api.mockResolvedValue({transcript:'00:00 <script>text only</script>',transcriptVersion:2})})
it('renders student transcripts as text without authoring controls',async()=>{
 const w=mount(ResourceTranscript,{props:{courseId:'c',resourceId:'r',editable:false}});await flushPromises();expect(w.get('.transcript-text').text()).toContain('<script>text only</script>');expect(w.find('script').exists()).toBe(false);expect(w.find('button').exists()).toBe(false);w.unmount()
})
it('preserves draft and captured version on conflict, and allows clearing',async()=>{
 const w=mount(ResourceTranscript,{props:{courseId:'c',resourceId:'r',editable:true}});await flushPromises();await w.get('button').trigger('click');await w.get('textarea').setValue('');api.mockRejectedValueOnce(new Error('逐字稿已被其他操作更新'));await w.get('form').trigger('submit');await flushPromises();expect(w.get('textarea').element.value).toBe('');expect(w.get('[role=alert]').text()).toContain('已被其他操作更新');expect(api).toHaveBeenLastCalledWith('/courses/c/resources/r/transcript',{method:'PUT',body:{transcript:'',expectedVersion:2}});w.unmount()
})
