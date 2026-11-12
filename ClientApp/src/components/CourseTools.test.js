import {mount,flushPromises} from '@vue/test-utils'
import {it,expect,vi,beforeEach} from 'vitest'
import CourseTools from './CourseTools.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({onBeforeRouteLeave:vi.fn(),onBeforeRouteUpdate:vi.fn()}))

const course={id:'course',status:'active',taRestrictions:[]}
const units=[{id:'u1',name:'基礎概念',description:'先建立共同語言'},{id:'u2',name:'案例應用',description:'練習分析'}]
let resources,curriculum
beforeEach(()=>{
 resources=[
  {id:'a',unitId:'u1',fileName:'ratio.pdf',title:'百分比講義',titleVersion:1,size:2048,downloadCount:3,createdAt:'2026-11-06T00:00:00Z',teachingDate:'2026-11-05',summary:'百分比與估算',metadataVersion:1,resourceType:'file',previewType:'application/pdf'},
  {id:'b',unitId:null,fileName:'photo.png',title:'實驗照片',titleVersion:1,size:1024,downloadCount:0,createdAt:'2026-11-07T00:00:00Z',summary:'觀察紀錄',metadataVersion:1,resourceType:'file',previewType:'image/png'}
 ];curriculum={units,activities:[]};api.mockReset();api.mockImplementation(async(path,options)=>{
  if(path.endsWith('/resources')&&!options)return structuredClone(resources)
  if(path.endsWith('/curriculum'))return structuredClone(curriculum)
  return {}
 })
})
function render(staff=true,extra={}){return mount(CourseTools,{props:{course:{...course,...extra},staff},global:{stubs:{RouterLink:{template:'<a><slot/></a>'},ResourceVisual:{template:'<span class="visual-stub" />'},FilePreview:true,ResourceLearningStats:true}}})}

it('renders compact resources by unit with metadata and a legacy section without tags',async()=>{
 const view=render(false);await flushPromises()
 expect(view.findAll('.resource-unit-section')).toHaveLength(2)
 expect(view.findAll('.resource-unit-heading')[0].text()).toContain('基礎概念');expect(view.findAll('.resource-unit-heading')[0].text()).toContain('1 份教材')
 expect(view.findAll('.resource-unit-heading')[1].text()).toContain('未歸類教材')
 const row=view.findAll('.resource-row')[0];expect(row.text()).toContain('百分比講義');expect(row.text()).toContain('PDF');expect(row.text()).toContain('2 KB');expect(row.text()).toContain('3 次下載');expect(row.text()).toContain('百分比與估算')
 expect(view.text()).not.toContain('標籤');expect(view.text()).not.toContain('未標記');expect(view.find('.resource-move').exists()).toBe(false);view.unmount()
})

it('requires a unit for new resources and guides teachers when no unit exists',async()=>{
 const view=render();await flushPromises();await view.get('details.add-resource summary').trigger('click')
 expect(view.get('select[aria-label="新增教材所屬單元"]').attributes('required')).toBeDefined()
 expect(view.get('button[type="submit"]').attributes('disabled')).toBeDefined();await view.get('select[aria-label="新增教材所屬單元"]').setValue('u1')
 expect(view.get('button[type="submit"]').attributes('disabled')).toBeDefined()
 view.unmount();curriculum={units:[],activities:[]};const empty=render();await flushPromises();expect(empty.text()).toContain('請先建立課程單元');expect(empty.find('details.add-resource').exists()).toBe(false);empty.unmount()
})

it('lets authorized staff move a material to another unit',async()=>{
 const view=render();await flushPromises();const select=view.get('select[aria-label="百分比講義 所屬單元"]');await select.setValue('u2');await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/course/units/activities/material/a',{method:'PUT',body:{unitId:'u2'}});view.unmount()
})
