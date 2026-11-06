import {mount,flushPromises} from '@vue/test-utils'
import {it,expect,vi,beforeEach} from 'vitest'
import MembersView from './MembersView.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
beforeEach(()=>{api.mockReset();api.mockResolvedValue([{id:'synthetic',displayName:'Synthetic',email:'test@example.invalid',role:'Member',canCreateCourses:false,studentNumber:null}])})
it('shows empty student number as read-only text and saves role with original role',async()=>{
 const w=mount(MembersView);await flushPromises();expect(w.text()).toContain('(空)');expect(w.find('input[aria-label="Synthetic 學號"]').exists()).toBe(false)
 await w.get('select[aria-label="Synthetic 平台管理權限"]').setValue('Administrator');api.mockResolvedValueOnce(null)
 await w.findAll('button').find(b=>b.text()==='儲存權限').trigger('click');await flushPromises()
 expect(api).toHaveBeenLastCalledWith('/accounts/synthetic/permissions',{method:'PUT',body:{role:'Administrator',canCreateCourses:false,expectedRole:'Member',expectedCanCreateCourses:false}})
 expect(w.text()).toContain('須重新登入')
})
it('reports role conflicts without pretending the role was saved',async()=>{
 const w=mount(MembersView);await flushPromises();await w.get('select').setValue('Administrator');api.mockRejectedValueOnce(new Error('角色已更新'))
 await w.findAll('button').find(b=>b.text()==='儲存權限').trigger('click');await flushPromises();expect(w.get('[role="alert"]').text()).toContain('角色已更新');expect(w.find('[role="status"]').exists()).toBe(false)
})
