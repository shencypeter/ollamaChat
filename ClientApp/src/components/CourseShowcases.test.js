import {mount,flushPromises} from '@vue/test-utils'
import {it,expect,vi,beforeEach} from 'vitest'
import CourseShowcases from './CourseShowcases.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({onBeforeRouteLeave:vi.fn(),onBeforeRouteUpdate:vi.fn()}))
beforeEach(()=>{api.mockReset()})
it('renders only the public copy for students with no source or authoring requests',async()=>{
 api.mockResolvedValue({staff:false,canPublish:false,items:[{id:'p',title:'匿名範例',background:'題幹',answer:'<b>pure text</b>',notes:'說明',publishedAt:'2026-11-09',hasAttachment:false}]})
 const w=mount(CourseShowcases,{props:{course:{id:'c',status:'active'},staff:false}});await flushPromises();await w.get('.showcase-choice').trigger('click');expect(w.get('.showcase-public').text()).toContain('<b>pure text</b>');expect(w.find('.showcase-public b').exists()).toBe(false);expect(w.find('select').exists()).toBe(false);expect(w.find('textarea').exists()).toBe(false);expect(api).toHaveBeenCalledTimes(1);w.unmount()
})
it('lets TAs prepare drafts but does not offer final publication',async()=>{
 api.mockImplementation(async(path)=>path.endsWith('/sources')?[]:{staff:true,canPublish:false,items:[{id:'d',version:2,draftRevision:2,publishedRevision:null,status:'draft',kind:'essay',content:{title:'草稿',background:'',answer:'待去識別',notes:''},hasAttachment:false}]})
 const w=mount(CourseShowcases,{props:{course:{id:'c',status:'active'},staff:true}});await flushPromises();await w.get('.showcase-choice').trigger('click');await w.get('textarea[aria-label="觀摩展示作答"]').setValue('匿名新稿');api.mockRejectedValueOnce(new Error('觀摩版本已變更'));await w.findAll('form').find(f=>f.find('textarea').exists()).trigger('submit');await flushPromises();expect(w.get('[role=alert]').text()).toContain('版本已變更');expect(w.get('textarea[aria-label="觀摩展示作答"]').element.value).toBe('匿名新稿');expect(w.text()).not.toContain('發布匿名觀摩');expect(api).toHaveBeenCalledWith('/courses/c/showcases/d',{method:'PUT',body:{title:'草稿',background:'',answer:'匿名新稿',notes:'',expectedVersion:2}});w.unmount()
})
