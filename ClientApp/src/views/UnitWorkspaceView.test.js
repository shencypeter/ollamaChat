import {mount,flushPromises} from '@vue/test-utils'
import {beforeEach,expect,it,vi} from 'vitest'
import UnitWorkspaceView from './UnitWorkspaceView.vue'
import {api} from '../api'

vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({useRoute:()=>({params:{courseId:'c',unitId:'u'}})}))

const detail={id:'u',name:'資料探索',description:'從教材走到實作',sortOrder:2,version:1,canManage:true,activities:[
 {id:'m',unitId:'u',kind:'material',title:'探索講義',status:'file',sortOrder:1},
 {id:'q',unitId:'u',kind:'quiz',title:'概念檢核',status:'published',dueAt:'2026-11-20T00:00:00Z',sortOrder:2},
 {id:'r',unitId:'u',kind:'report',title:'分析報告',status:'draft',sortOrder:3}
]}

beforeEach(()=>{api.mockReset();api.mockImplementation(async path=>path.endsWith('/curriculum')?{units:[{id:'u',name:'資料探索'},{id:'u2',name:'延伸應用'}]}:path==='/courses/c'?{id:'c',status:'active',courseRole:'teacher',taRestrictions:[]}:structuredClone(detail))})
function render(){return mount(UnitWorkspaceView,{global:{stubs:{RouterLink:{name:'RouterLink',props:['to'],template:'<a><slot/></a>'},CourseTools:{name:'CourseTools',props:['creatorOnly','lockedUnitId'],template:'<div />'},UnitAssessmentCreator:{name:'UnitAssessmentCreator',template:'<div />'}}}})}

it('shows a mixed activity path and canonical links',async()=>{
 const view=render();await flushPromises();expect(view.text()).toContain('資料探索');expect(view.text()).toContain('探索講義');expect(view.text()).toContain('概念檢核');expect(view.text()).toContain('分析報告')
 expect(view.findAllComponents({name:'RouterLink'}).map(x=>x.props('to'))).toEqual(expect.arrayContaining(['/courses/c','/courses/c/materials/m','/courses/c/quizzes/q','/courses/c/reports/r']))
})

it('reorders the complete activity list for a teacher',async()=>{
 const view=render();await flushPromises();await view.get('button[aria-label="概念檢核 上移"]').trigger('click');await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/c/units/u/activities/reorder',{method:'PUT',body:{items:[{kind:'quiz',id:'q'},{kind:'material',id:'m'},{kind:'report',id:'r'}]}})
})

it('moves an assigned activity to another course unit',async()=>{
 const view=render();await flushPromises();await view.get('select[aria-label="分析報告 所屬單元"]').setValue('u2');await flushPromises();expect(api).toHaveBeenCalledWith('/courses/c/units/activities/report/r',{method:'PUT',body:{unitId:'u2'}})
})

it('keeps a student unit read only',async()=>{
 api.mockResolvedValue({...structuredClone(detail),canManage:false});const view=render();await flushPromises();expect(view.find('button[aria-label="概念檢核 上移"]').exists()).toBe(false);expect(view.text()).not.toContain('新增教材')
})

it('preserves unrestricted assistant activity creation without unit reordering',async()=>{
 api.mockImplementation(async path=>path.endsWith('/units/u')?{...structuredClone(detail),canManage:false}:path==='/courses/c'?{id:'c',status:'active',courseRole:'ta',taRestrictions:[]}:{});const view=render();await flushPromises();expect(view.text()).toContain('新增教材');expect(view.text()).toContain('新增測驗／作業');expect(view.find('button[aria-label="概念檢核 上移"]').exists()).toBe(false)
 await view.get('button').trigger('click');await flushPromises();expect(view.findComponent({name:'CourseTools'}).props('creatorOnly')).toBeFalsy()
})

it('shows a safe return when the unit cannot be loaded',async()=>{
 api.mockRejectedValue(new Error('找不到單元'));const view=render();await flushPromises();expect(view.text()).toContain('找不到單元');expect(view.findComponent({name:'RouterLink'}).props('to')).toBe('/courses/c')
})
