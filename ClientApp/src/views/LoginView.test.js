import {mount,flushPromises} from '@vue/test-utils'
import {beforeEach,it,expect,vi} from 'vitest'
import LoginView from './LoginView.vue'
const state=vi.hoisted(()=>({query:{next:'/courses/c/materials/r'},replace:vi.fn(),signIn:vi.fn()}))
vi.mock('vue-router',()=>({useRoute:()=>({query:state.query}),useRouter:()=>({replace:state.replace})}))
vi.mock('../composables/auth',()=>({auth:{needsSetup:false,error:''},loadSession:vi.fn(),signIn:state.signIn}))
vi.mock('../composables/theme',()=>({theme:'light',toggleTheme:vi.fn()}))
beforeEach(()=>{state.replace.mockReset();state.signIn.mockReset();state.query={next:'/courses/c/materials/r'}})
it('uses the CSMU identity and returns to the authorized material after signing in',async()=>{
 const v=mount(LoginView,{global:{stubs:{RouterLink:true}}});expect(v.text()).toContain('中山醫學大學');expect(v.text()).not.toMatch(/NSYSU|國立中山大學/)
 await v.get('input[type="email"]').setValue('student@csmu.edu.tw');await v.get('input[type="password"]').setValue('test-only-password');await v.get('form').trigger('submit');await flushPromises()
 expect(state.replace).toHaveBeenCalledWith('/courses/c/materials/r');v.unmount()
})
it('ignores an external return destination',async()=>{
 state.query={next:'https://example.com'};const v=mount(LoginView,{global:{stubs:{RouterLink:true}}});await v.get('form').trigger('submit');await flushPromises();expect(state.replace).toHaveBeenCalledWith('/');v.unmount()
})
