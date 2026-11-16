import {mount,flushPromises} from '@vue/test-utils'
import {beforeEach,expect,it,vi} from 'vitest'
import CoursesView from './CoursesView.vue'
import {api} from '../api'

vi.mock('../composables/auth',()=>({auth:{user:{canTeach:true,canCreateCourses:true}},loadSession:vi.fn()}))
vi.mock('../api',()=>({api:vi.fn()}))
const rows=[
  {id:'a',code:'A',title:'指導課程',termCode:'115-1',classLabel:'',courseRole:'teacher',status:'active',isOwner:true,version:2},
  {id:'b',code:'B',title:'進修課程',termCode:'115-1',classLabel:'',courseRole:'student',status:'active',isOwner:false,version:1},
  {id:'draft',code:'D',title:'備課中',termCode:'115-1',classLabel:'甲班',courseRole:'teacher',status:'draft',isOwner:true,version:3},
  {id:'c',code:'C',title:'協同助教課程',termCode:'115-1',classLabel:'',courseRole:'ta',status:'active',isOwner:false,version:1}
]
const global={stubs:{RouterLink:{name:'RouterLink',props:['to'],template:'<a><slot/></a>'}}}
beforeEach(()=>{api.mockReset().mockImplementation(async(path,options)=>options?undefined:rows)})

it.each([
  [true,['指導課程','進修課程','備課中'],false],
  [false,['指導課程','備課中','協同助教課程'],true]
])('shows the same professor the courses for their selected portal',async(enrolled,titles,canCreate)=>{
  const view=mount(CoursesView,{props:{enrolled},global});await flushPromises()
  expect(view.findAll('.course-card h2').map(h=>h.text())).toEqual(titles)
  expect(view.find('.page-heading button').exists()).toBe(canCreate)
  if(enrolled){
    const links=view.findAllComponents({name:'RouterLink'}).filter(l=>l.classes('course-open-link'))
    expect(links.map(l=>l.props('to'))).toEqual([{path:'/courses/a',query:{view:'preview'}},{path:'/courses/b',query:{}},{path:'/courses/draft',query:{view:'preview'}}])
    expect(view.find('.course-quick-actions').exists()).toBe(false)
  }
  view.unmount()
})

it('shows owner quick actions and preserves course fields when renaming',async()=>{
  const view=mount(CoursesView,{props:{enrolled:false},global});await flushPromises()
  const cards=view.findAll('.course-card'),active=cards[0],draft=cards[1],ta=cards[2]
  expect(active.text()).toContain('修改名稱');expect(active.text()).not.toContain('刪除課程')
  expect(draft.text()).toContain('修改名稱');expect(draft.text()).toContain('公開課程');expect(draft.text()).toContain('刪除課程')
  expect(ta.find('.course-quick-actions').exists()).toBe(false)
  await draft.findAll('button').find(b=>b.text().includes('修改名稱')).trigger('click')
  await draft.get('input').setValue('新課程名稱');await draft.get('form').trigger('submit');await flushPromises()
  expect(api).toHaveBeenCalledWith('/courses/draft',{method:'PUT',body:{code:'D',title:'新課程名稱',termCode:'115-1',classLabel:'甲班',expectedVersion:3}})
  view.unmount()
})

it('publishes drafts and requires a second explicit delete action',async()=>{
  const view=mount(CoursesView,{props:{enrolled:false},global});await flushPromises();let draft=view.findAll('.course-card')[1]
  await draft.findAll('button').find(b=>b.text().includes('公開課程')).trigger('click');await flushPromises()
  expect(api).toHaveBeenCalledWith('/courses/draft/activate',{method:'POST'})
  draft=view.findAll('.course-card')[1]
  await draft.findAll('button').find(b=>b.text().includes('刪除課程')).trigger('click')
  expect(api.mock.calls.some(([,o])=>o?.method==='DELETE')).toBe(false);expect(draft.text()).toContain('刪除不可復原')
  await draft.findAll('button').find(b=>b.text()==='確認刪除').trigger('click');await flushPromises()
  expect(api).toHaveBeenCalledWith('/courses/draft',{method:'DELETE'})
  view.unmount()
})
it('identifies teaching courses with YouTube materials without extra course requests',async()=>{
 api.mockResolvedValue(rows.map(row=>({...row,youTubeCount:row.id==='a'?3:0})))
 const view=mount(CoursesView,{global});await flushPromises()
 expect(view.findAll('.course-video-badge')).toHaveLength(1);expect(view.get('.course-video-badge').text()).toBe('YouTube 影音教材・3 部')
 expect(view.findAll('.course-card')[1].find('.course-video-badge').exists()).toBe(false);expect(api).toHaveBeenCalledTimes(2);view.unmount()
})
it('defaults to the configured term, supports historical queries and hides student management badges',async()=>{
 api.mockImplementation(async path=>path.startsWith('/academic-calendar')?{currentTerm:'115-2',terms:['115-2','114-1']}:[{...rows[1],id:'now',title:'當學期課程',termCode:'115-2'},{...rows[1],id:'old',title:'歷年課程',termCode:'114-1',status:'archived'},{...rows[1],id:'hidden',title:'未公開學生課程',status:'draft',termCode:'115-2'}])
 const view=mount(CoursesView,{props:{enrolled:true},global});await flushPromises()
 expect(view.findAll('.course-card h2').map(x=>x.text())).toEqual(['當學期課程'])
 expect(view.find('.course-card-top .status-badge').exists()).toBe(false);expect(view.text()).not.toContain('未公開學生課程')
 await view.get('select[aria-label="學年度"]').setValue('114');await view.get('select[aria-label="學期"]').setValue('1')
 expect(view.findAll('.course-card h2').map(x=>x.text())).toEqual(['歷年課程']);expect(view.find('.course-card-top .status-badge').exists()).toBe(false);view.unmount()
})
it('shows all accessible years with an explicit explanation when current term is unset',async()=>{
 api.mockImplementation(async path=>path.startsWith('/academic-calendar')?{currentTerm:null,terms:['115-1','114-2']}:[rows[1],{...rows[1],id:'old',termCode:'114-2'}])
 const view=mount(CoursesView,{props:{enrolled:true},global});await flushPromises();expect(view.text()).toContain('未設定當學期');expect(view.findAll('.course-card')).toHaveLength(2);view.unmount()
})
it('explains that past courses are filtered when no current-term course exists',async()=>{
 api.mockImplementation(async path=>path.startsWith('/academic-calendar')?{currentTerm:'115-1',terms:['114-2']}:[{...rows[1],termCode:'114-2',status:'archived'}])
 const view=mount(CoursesView,{props:{enrolled:true},global});await flushPromises()
 expect(view.text()).toContain('目前篩選沒有符合的課程');expect(view.text()).toContain('查看歷年課程');expect(view.text()).not.toContain('目前沒有已選修的課程')
 await view.get('select[aria-label="學年度"]').setValue('');await view.get('select[aria-label="學期"]').setValue('');expect(view.findAll('.course-card')).toHaveLength(1);view.unmount()
})
