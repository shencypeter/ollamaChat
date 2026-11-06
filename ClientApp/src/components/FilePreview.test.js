import {mount,flushPromises} from '@vue/test-utils'
import {it,expect,vi,beforeEach} from 'vitest'
import FilePreview from './FilePreview.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
beforeEach(()=>{api.mockReset();URL.createObjectURL=vi.fn(()=> 'blob:synthetic');URL.revokeObjectURL=vi.fn()})
it('automatically loads an image thumbnail and releases its private blob on unmount',async()=>{
 api.mockResolvedValue(new Blob(['synthetic'],{type:'image/png'}));const w=mount(FilePreview,{props:{endpoint:'/synthetic/preview',fileName:'sample.png',contentType:'image/png'}});await flushPromises()
 expect(w.get('img').attributes('src')).toBe('blob:synthetic');await w.get('button').trigger('click');expect(w.get('img').classes()).toContain('expanded');w.unmount();expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:synthetic')
})
it('loads PDF on demand and provides a download fallback message',async()=>{
 api.mockResolvedValue(new Blob(['%PDF-'],{type:'application/pdf'}));const w=mount(FilePreview,{props:{endpoint:'/synthetic/preview',fileName:'sample.pdf',contentType:'application/pdf'}})
 expect(api).not.toHaveBeenCalled();expect(w.find('.pdf-document-glyph').exists()).toBe(true);await w.get('button').trigger('click');await flushPromises();expect(w.get('object').attributes('type')).toBe('application/pdf');expect(w.text()).toContain('請使用下載按鈕');expect(w.find('.pdf-document-glyph').exists()).toBe(false);await w.get('button').trigger('click');expect(w.find('.pdf-document-glyph').exists()).toBe(true);w.unmount()
})
it('reports authorization failures and ignores a response after the target changes',async()=>{
 let finish;api.mockReturnValueOnce(new Promise(resolve=>finish=resolve));const w=mount(FilePreview,{props:{endpoint:'/old',contentType:'image/png'}});await w.setProps({endpoint:'/new',contentType:null});finish(new Blob(['x'],{type:'image/png'}));await flushPromises();expect(URL.createObjectURL).not.toHaveBeenCalled()
 api.mockRejectedValueOnce(new Error('你沒有權限'));await w.setProps({contentType:'image/png'});await flushPromises();expect(w.get('[role="alert"]').text()).toContain('你沒有權限');w.unmount()
})
