import {mount,flushPromises} from '@vue/test-utils'
import {beforeEach,it,expect,vi} from 'vitest'
import PortalActivity from './PortalActivity.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
const global={stubs:{RouterLink:{props:['to'],template:'<a :href="to"><slot/></a>'}}}
beforeEach(()=>{api.mockReset()})
it('shows course and author context in the inbox and formal exams on the agenda',async()=>{
 api.mockImplementation(async path=>path.includes('/agenda')?{items:[{id:'exam',kind:'exam',courseId:'c',courseTitle:'英文',title:'期中考',dueAt:'2026-11-20T02:00:00Z',url:'/courses/c?section=assessments'}]}:{items:[{id:'n',kind:'announcement',courseId:'c',courseTitle:'英文',title:'考試範圍',authorName:'王老師',occurredAt:'2026-11-13T01:00:00Z',url:'/courses/c?section=announcements'}],hasMore:true})
 const view=mount(PortalActivity,{props:{teaching:false,courses:[{id:'c',title:'英文'}]},global});await flushPromises()
 expect(view.text()).toContain('跨課訊息');expect(view.text()).toContain('王老師');expect(view.get('a[href="/courses/c?section=assessments"]').text()).toContain('期中考')
 await view.get('select[aria-label="訊息與行程課程"]').setValue('c');await flushPromises()
 expect(api.mock.calls.some(([p])=>p.includes('courseId=c')&&p.includes('view=student'))).toBe(true)
 await view.get('button[aria-label="下一頁訊息"]').trigger('click');await flushPromises()
 expect(api.mock.calls.some(([p])=>p.includes('page=2'))).toBe(true);view.unmount()
})
it('retains the agenda while reporting inbox failure and supports retry',async()=>{
 api.mockImplementation(async path=>{if(!path.includes('/agenda'))throw new Error('訊息連線失敗');return {items:[]}})
 const view=mount(PortalActivity,{props:{teaching:true},global});await flushPromises()
 expect(view.text()).toContain('訊息連線失敗');expect(view.text()).toContain('目前沒有已排定的行程');expect(view.text()).not.toContain('目前沒有課程訊息')
 api.mockResolvedValue({items:[],hasMore:false});await view.get('button[aria-label="重新載入訊息與行程"]').trigger('click');await flushPromises();expect(view.text()).toContain('目前沒有課程訊息');view.unmount()
})
