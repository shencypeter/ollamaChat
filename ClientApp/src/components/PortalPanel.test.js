import {mount} from '@vue/test-utils'
import {nextTick} from 'vue'
import {beforeEach,it,expect,vi} from 'vitest'
import PortalPanel from './PortalPanel.vue'
import {auth} from '../composables/auth'
vi.mock('../composables/auth',()=>({auth:{user:{id:'one'}}}))
beforeEach(()=>{localStorage.clear();auth.user={id:'one'};window.location.hash='';vi.restoreAllMocks()})
function render(scope='student'){return mount(PortalPanel,{props:{id:'tasks',panelKey:'tasks',scope,title:'學習待辦',count:'3 項'},slots:{default:'<input value="keep my input"/>',actions:'<a href="/learning-history">全部待辦</a>'}})}
it('starts expanded with native keyboard control and keeps the count and form state when folded',async()=>{
 const v=render();expect(v.get('details').element.open).toBe(true);expect(v.get('summary').text()).toContain('學習待辦');expect(v.get('summary').text()).toContain('3 項')
 v.get('details').element.open=false;await v.get('details').trigger('toggle');await nextTick()
 expect(v.get('details').element.open).toBe(false);expect(v.get('input').element.value).toBe('keep my input');expect(v.find('summary a').exists()).toBe(false)
 v.unmount();const restored=render();expect(restored.get('details').element.open).toBe(false);restored.unmount()
})
it('isolates preferences by account and perspective and opens a folded section for an anchor',async()=>{
 let v=render();v.get('details').element.open=false;await v.get('details').trigger('toggle');v.unmount()
 v=render('teaching');expect(v.get('details').element.open).toBe(true);v.unmount()
 auth.user={id:'two'};v=render();expect(v.get('details').element.open).toBe(true);v.unmount()
 auth.user={id:'one'};v=render();expect(v.get('details').element.open).toBe(false)
 window.location.hash='#tasks';window.dispatchEvent(new Event('hashchange'));await nextTick();expect(v.get('details').element.open).toBe(true);v.unmount()
})
it('works when browser storage is unavailable and does not persist an anonymous preference',async()=>{
 vi.spyOn(Storage.prototype,'getItem').mockImplementation(()=>{throw new Error('blocked')});const write=vi.spyOn(Storage.prototype,'setItem').mockImplementation(()=>{throw new Error('blocked')})
 const v=render();v.get('details').element.open=false;await v.get('details').trigger('toggle');expect(v.get('details').element.open).toBe(false);v.unmount()
 write.mockClear();auth.user=null;const anonymous=render();anonymous.get('details').element.open=false;await anonymous.get('details').trigger('toggle');expect(write).not.toHaveBeenCalled();anonymous.unmount()
})
