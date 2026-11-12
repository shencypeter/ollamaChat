import {mount,flushPromises} from '@vue/test-utils'
import {beforeEach,expect,it,vi} from 'vitest'
import CourseUnits from './CourseUnits.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
const course={id:'course',title:'單元測試課程'}
const curriculum={canManage:true,units:[{id:'u1',name:'概念',description:'先建立共同語言',sortOrder:1,version:1},{id:'u2',name:'應用',description:'案例練習',sortOrder:2,version:1}],activities:[{id:'m1',unitId:'u1',kind:'material',title:'閱讀材料',status:'file'},{id:'q1',unitId:'u2',kind:'quiz',title:'概念測驗',status:'published'}]}
beforeEach(()=>{api.mockReset();api.mockImplementation(async path=>path.endsWith('/curriculum')?structuredClone(curriculum):{})})
function render(manage=true){return mount(CourseUnits,{props:{course,manage},global:{stubs:{RouterLink:{props:['to'],template:'<a><slot/></a>'}}}})}
it('renders curriculum in unit order and lets teachers reorder and reassign activities',async()=>{
 const view=render();await flushPromises();expect(view.findAll('.unit-card h2').map(x=>x.text())).toEqual(['概念','應用'])
 await view.get('button[aria-label="概念 下移"]').trigger('click');await flushPromises();expect(api).toHaveBeenCalledWith('/courses/course/units/reorder',{method:'PUT',body:{unitIds:['u2','u1']}})
 const select=view.get('select[aria-label="閱讀材料 所屬單元"]');await select.setValue('u2');await flushPromises();expect(api).toHaveBeenCalledWith('/courses/course/units/activities/material/m1',{method:'PUT',body:{unitId:'u2'}});view.unmount()
})
it('keeps the student curriculum read only',async()=>{
 curriculum.canManage=false;const view=render(false);await flushPromises();expect(view.text()).toContain('閱讀材料');expect(view.text()).toContain('概念測驗');expect(view.text()).not.toContain('新增單元');expect(view.find('select').exists()).toBe(false);view.unmount();curriculum.canManage=true
})
it('keeps ordinary assessment grading actions inside their unit for staff',async()=>{
 const view=render(true);await flushPromises();expect(view.text()).toContain('批改作答');view.unmount()
})
