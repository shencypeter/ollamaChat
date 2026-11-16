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

it('offers static view records even when the material has no timed learning type',async()=>{
 const view=render();await flushPromises()
 expect(view.findAll('.resource-row')[0].text()).toContain('點閱紀錄')
 expect(view.findAll('.resource-row')[1].text()).toContain('點閱紀錄')
 view.unmount()
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

let groups,classmates
function renderGroups(extra={}){
 groups=[{id:'g1',name:'第一組',members:[{id:'s1',displayName:'甲',isLeader:true},{id:'s2',displayName:'乙',isLeader:false}]},{id:'g2',name:'第二組',members:[{id:'s3',displayName:'丙',isLeader:true}]}]
 classmates=[{id:'s1',displayName:'甲'},{id:'s2',displayName:'乙'},{id:'s3',displayName:'丙'},{id:'s4',displayName:'丁'}]
 api.mockImplementation(async(path,options)=>{
  if(path.endsWith('/groups')&&!options)return structuredClone(groups)
  if(path.endsWith('/classmates'))return structuredClone(classmates)
  if(options?.method==='PUT'){const draft=options.body;groups=groups.map(item=>item.id===draft.id?{id:draft.id,name:draft.name,members:draft.members.map(id=>({id,displayName:classmates.find(m=>m.id===id).displayName,isLeader:id===draft.leaderId}))}:item)}
  if(options?.method==='DELETE')groups=groups.filter(item=>!path.endsWith('/'+item.id))
  return {}
 })
 return mount(CourseTools,{props:{course:{...course,...extra},staff:true,section:'groups'},global:{stubs:{RouterLink:true}}})
}
function button(view,label){return view.findAll('button').find(item=>item.text()===label)}
it('edits membership and clears or replaces a leader without dissolving the group',async()=>{
 const view=renderGroups();await flushPromises()
 expect(view.findAll('input[type="checkbox"]')).toHaveLength(1)
 await button(view,'編輯小組').trigger('click')
 expect(view.findAll('input[type="checkbox"]')).toHaveLength(3)
 expect(view.get('select[aria-label="組長"]').attributes('required')).toBeUndefined()
 await view.get('input[aria-label="小組名稱"]').setValue('改名第一組')
 await view.get('input[type="checkbox"][value="s1"]').setValue(false)
 expect(view.text()).toContain('原組長已移出本組')
 expect(view.get('select[aria-label="組長"]').element.value).toBe('')
 await view.get('select[aria-label="組長"]').setValue('s2')
 await view.get('form').trigger('submit');await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/course/groups/g1',{method:'PUT',body:{id:'g1',name:'改名第一組',members:['s2'],leaderId:'s2'}})
 expect(view.findAll('article')[0].text()).toContain('乙（組長）')
 expect(view.findAll('input[type="checkbox"]')).toHaveLength(2)
 await button(view,'編輯小組').trigger('click');await view.get('select[aria-label="組長"]').setValue('')
 await view.get('form').trigger('submit');await flushPromises()
 expect(view.findAll('article')[0].text()).toContain('未指定組長')
 expect(api.mock.calls.some(([,options])=>options?.method==='DELETE')).toBe(false)
 view.unmount()
})
it('keeps dissolution confirmation and reloads the available roster after success',async()=>{
 const confirm=vi.spyOn(window,'confirm').mockReturnValueOnce(false).mockReturnValueOnce(true)
 const view=renderGroups();await flushPromises();await button(view,'解散小組').trigger('click');await flushPromises()
 expect(api.mock.calls.some(([,options])=>options?.method==='DELETE')).toBe(false)
 await button(view,'解散小組').trigger('click');await flushPromises()
 expect(view.findAll('article')).toHaveLength(1);expect(view.findAll('input[type="checkbox"]')).toHaveLength(3)
 expect(view.text()).toContain('小組已解散');confirm.mockRestore();view.unmount()
})
it('does not offer group writes to a restricted assistant or an archived course',async()=>{
 for(const extra of [{taRestrictions:['assessments']},{status:'archived'}]){
  const view=renderGroups(extra);await flushPromises();expect(view.find('form').exists()).toBe(false);expect(view.find('button').exists()).toBe(false);view.unmount()
 }
})
it('keeps a failed edit available and clears its error after a successful retry',async()=>{
 const view=renderGroups();await flushPromises();await button(view,'編輯小組').trigger('click')
 await view.get('input[aria-label="小組名稱"]').setValue('重試小組')
 const normal=api.getMockImplementation();let fail=true
 api.mockImplementation(async(path,options)=>{if(options?.method==='PUT'&&fail){fail=false;throw new Error('小組已變更，請重試。')}return normal(path,options)})
 await view.get('form').trigger('submit');await flushPromises()
 expect(view.get('[role="alert"]').text()).toContain('請重試');expect(view.get('input[aria-label="小組名稱"]').element.value).toBe('重試小組')
 await view.get('form').trigger('submit');await flushPromises()
 expect(view.find('[role="alert"]').exists()).toBe(false);expect(view.findAll('article')[0].text()).toContain('重試小組')
 view.unmount()
})
it('always labels the safe original filename separately from the document title and type',async()=>{
 resources[0].title='';resources[0].fileName='C:\\private\\ratio.pdf'
 const view=render(false);await flushPromises();const row=view.findAll('.resource-row')[0]
 expect(row.find('.resource-original-name').text()).toBe('原始檔名：ratio.pdf')
 expect(row.find('.resource-title-line').text()).toContain('ratio.pdf');expect(row.text()).not.toContain('private')
 view.unmount()
})
