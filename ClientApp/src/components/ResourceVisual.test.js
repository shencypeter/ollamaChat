import {mount,flushPromises} from '@vue/test-utils'
import {beforeEach,afterEach,it,expect,vi} from 'vitest'
import ResourceVisual from './ResourceVisual.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))

beforeEach(()=>{
 api.mockReset();api.mockResolvedValue(new Blob(['image'],{type:'image/png'}))
 vi.stubGlobal('URL',{createObjectURL:vi.fn(()=> 'blob:preview'),revokeObjectURL:vi.fn()})
 vi.stubGlobal('IntersectionObserver',undefined)
})
afterEach(()=>vi.unstubAllGlobals())

it('loads an authenticated image thumbnail and falls back to the image icon on failure',async()=>{
 const item={id:'image',title:'實驗照片',fileName:'photo.png',previewType:'image/png',resourceType:'file'}
 const wrapper=mount(ResourceVisual,{props:{item,endpoint:'/preview/image'}});await flushPromises()
 expect(api).toHaveBeenCalledWith('/preview/image',{download:true})
 expect(wrapper.get('img').attributes()).toMatchObject({src:'blob:preview',alt:'實驗照片',loading:'lazy'})
 await wrapper.get('img').trigger('error');expect(wrapper.find('img').exists()).toBe(false)
 expect(wrapper.get('[aria-label="圖片文件"]')).toBeTruthy();wrapper.unmount()
 expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:preview')
})

it.each([
 [{id:'pdf',fileName:'notes.pdf',previewType:'application/pdf',resourceType:'file'},'PDF 文件'],
 [{id:'video',fileName:'YouTube 課程回放',resourceType:'youtube'},'YouTube 影片']
])('uses a format icon without fetching a thumbnail for %s',async(item,label)=>{
 const wrapper=mount(ResourceVisual,{props:{item,endpoint:'/unused'}});await flushPromises()
 expect(api).not.toHaveBeenCalled();expect(wrapper.get(`[aria-label="${label}"]`)).toBeTruthy();wrapper.unmount()
})
