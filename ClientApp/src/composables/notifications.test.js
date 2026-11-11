import {it,expect,vi,beforeEach} from 'vitest'
import {mount} from '@vue/test-utils'
import NotificationBadge from '../components/NotificationBadge.vue'
import {api} from '../api'
import {notifications,resetNotifications,refreshNotifications,markNotificationsRead} from './notifications'
vi.mock('../api',()=>({api:vi.fn()}))
beforeEach(()=>{api.mockReset();resetNotifications()})
it('hides zero counts and shows an accessible red badge capped visually at 99+',()=>{
 const w=mount(NotificationBadge,{props:{count:0,label:'新提問'}});expect(w.find('span').exists()).toBe(false)
 w.unmount();const full=mount(NotificationBadge,{props:{count:120,label:'新提問'}});expect(full.text()).toBe('99+');expect(full.get('span').attributes('aria-label')).toBe('新提問 120 筆');full.unmount()
})
it('ignores delayed responses from the previous account or view',async()=>{
 let finish;api.mockImplementationOnce(()=>new Promise(resolve=>finish=resolve));const pending=refreshNotifications();resetNotifications();finish({total:42,assignments:42,questions:0,courses:[]});await pending;expect(notifications.total).toBe(0)
})
it('acknowledges only the supplied rendered items before refreshing counts',async()=>{
 api.mockResolvedValueOnce(null).mockResolvedValueOnce({total:1,assignments:0,questions:1,courses:[]})
 await markNotificationsRead('question',['shown']);expect(api.mock.calls[0]).toEqual(['/notifications/read',{method:'POST',body:{kind:'question',ids:['shown']}}]);expect(notifications.total).toBe(1)
})
