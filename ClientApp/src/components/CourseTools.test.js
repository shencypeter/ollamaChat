import {mount,flushPromises} from '@vue/test-utils'
import {it,expect,vi,beforeEach} from 'vitest'
import CourseTools from './CourseTools.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({onBeforeRouteLeave:vi.fn(),onBeforeRouteUpdate:vi.fn()}))
const course={id:'course',status:'active'}
let resources
beforeEach(()=>{resources=[{id:'a',fileName:'a.txt',title:'第一份',titleVersion:1,tags:['第1週'],tagsVersion:2,size:1,downloadCount:0,createdAt:'2026-11-06T00:00:00Z'},{id:'b',fileName:'b.txt',title:'第二份',titleVersion:1,tags:['第2週'],tagsVersion:4,size:1,downloadCount:0,createdAt:'2026-11-06T00:00:00Z'}];api.mockReset();api.mockImplementation(async(path,options)=>{if(path.endsWith('/resources')&&!options)return structuredClone(resources);return {}})})
function render(staff=true){return mount(CourseTools,{props:{course,staff},global:{stubs:{RouterLink:true,FilePreview:true}}})}
it('lets students filter course materials while hiding authoring controls',async()=>{
 const w=render(false);await flushPromises();expect(w.findAll('.resource-card')).toHaveLength(2);await w.findAll('.tag-chip').find(b=>b.text()==='第1週（1）').trigger('click');expect(w.findAll('.resource-card')).toHaveLength(1);expect(w.find('.resource-card').text()).toContain('第一份');expect(w.text()).not.toContain('編輯標籤');expect(w.find('input[type=file]').exists()).toBe(false);w.unmount()
})
it('keeps unsaved tags and their original version when another resource reloads',async()=>{
 const w=render();await flushPromises();for(const card of w.findAll('.resource-card'))await card.findAll('button').find(b=>b.text()==='編輯標籤').trigger('click')
 await w.get('input[aria-label="第一份 標籤"]').setValue('第3週');await w.get('input[aria-label="第二份 標籤"]').setValue('第4週');resources[1].tagsVersion=5
 await w.findAll('.resource-card').find(c=>c.text().includes('第一份')).find('form').trigger('submit');await flushPromises();expect(w.get('input[aria-label="第二份 標籤"]').element.value).toBe('第4週')
 api.mockRejectedValueOnce(Object.assign(new Error('標籤已被其他操作更新'),{status:409}));await w.findAll('.resource-card').find(c=>c.text().includes('第二份')).find('form').trigger('submit');await flushPromises();expect(api).toHaveBeenCalledWith('/courses/course/resources/b/tags',{method:'PUT',body:{tags:['第4週'],expectedVersion:4}});expect(w.get('[role="alert"]').text()).toContain('標籤已被其他操作更新');expect(w.get('input[aria-label="第二份 標籤"]').element.value).toBe('第4週');w.unmount()
})

