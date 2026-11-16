import {mount,flushPromises} from '@vue/test-utils'
import {beforeEach,it,expect,vi} from 'vitest'
import PortalActivity from './PortalActivity.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
const global={stubs:{RouterLink:{props:['to'],template:'<a :href="to"><slot/></a>'}}}
beforeEach(()=>{api.mockReset()})
it('applies the selected academic scope to both inbox and agenda and reloads on change',async()=>{
 api.mockResolvedValue({items:[],hasMore:false})
 const v=mount(PortalActivity,{props:{term:'115-1',year:'115',semester:'1'},global});await flushPromises()
 for(const [path] of api.mock.calls){const q=new URLSearchParams(path.split('?')[1]);expect(q.get('term')).toBe('115-1');expect(q.get('year')).toBe('115');expect(q.get('semester')).toBe('1')}
 api.mockClear();await v.setProps({term:'',year:'114',semester:''});await flushPromises()
 expect(api.mock.calls).toHaveLength(2)
 for(const [path] of api.mock.calls){const q=new URLSearchParams(path.split('?')[1]);expect(q.get('year')).toBe('114');expect(q.has('term')).toBe(false);expect(q.has('semester')).toBe(false)}
 v.unmount()
})
it('shows course and author context in the inbox and formal exams on the agenda',async()=>{
 api.mockImplementation(async path=>path.includes('/agenda')?{items:[{id:'exam',kind:'exam',courseId:'c',courseTitle:'英文',title:'期中考',dueAt:'2026-11-20T02:00:00Z',url:'/courses/c?section=assessments'}]}:{items:[{id:'n',kind:'announcement',courseId:'c',courseTitle:'英文',title:'考試範圍',authorName:'王老師',occurredAt:'2026-11-13T01:00:00Z',url:'/courses/c?section=announcements'}],hasMore:true})
 const view=mount(PortalActivity,{props:{teaching:false,courses:[{id:'c',title:'英文'}]},global});await flushPromises()
 expect(view.text()).toContain('跨課訊息');expect(view.text()).toContain('王老師');expect(view.get('a[href="/courses/c?section=assessments"]').text()).toContain('期中考')
 await view.get('select[aria-label="訊息與行程課程"]').setValue('c');await flushPromises()
 expect(api.mock.calls.some(([p])=>p.includes('courseId=c')&&p.includes('view=student'))).toBe(true)
 await view.get('button[aria-label="跨課訊息 下一頁"]').trigger('click');await flushPromises()
 expect(api.mock.calls.some(([p])=>p.includes('page=2'))).toBe(true);view.unmount()
})
it('retains the agenda while reporting inbox failure and supports retry',async()=>{
 api.mockImplementation(async path=>{if(!path.includes('/agenda'))throw new Error('訊息連線失敗');return {items:[]}})
 const view=mount(PortalActivity,{props:{teaching:true},global});await flushPromises()
 expect(view.text()).toContain('訊息連線失敗');expect(view.text()).toContain('目前沒有已排定的行程');expect(view.text()).not.toContain('目前沒有課程訊息')
 api.mockResolvedValue({items:[],hasMore:false});await view.get('button[aria-label="重新載入訊息與行程"]').trigger('click');await flushPromises();expect(view.text()).toContain('目前沒有課程訊息');view.unmount()
})
it('defaults server messages and local agenda to five items and resets the server page on resize',async()=>{
 const events=Array.from({length:12},(_,i)=>({id:`a${i}`,title:`行程 ${i+1}`,dueAt:'2026-11-20T02:00:00Z',url:'/exam'}))
 api.mockImplementation(async path=>{if(path.includes('/agenda'))return {items:events};const query=new URLSearchParams(path.split('?')[1]);return {items:events.slice(0,Number(query.get('pageSize'))),hasMore:true}})
 const v=mount(PortalActivity,{global});await flushPromises()
 expect(api.mock.calls.some(([path])=>path.includes('/portal-feed?')&&path.includes('pageSize=5'))).toBe(true)
 expect(v.findAll('.agenda-days li')).toHaveLength(5)
 await v.get('select[aria-label="即將到來的行程 每頁筆數"]').setValue('10');expect(v.findAll('.agenda-days li')).toHaveLength(10)
 await v.get('button[aria-label="跨課訊息 下一頁"]').trigger('click');await flushPromises()
 await v.get('select[aria-label="跨課訊息 每頁筆數"]').setValue('20');await flushPromises()
 expect(api.mock.calls.at(-1)[0]).toContain('page=1&pageSize=20');expect(v.findAll('.agenda-days li')).toHaveLength(10);v.unmount()
})
