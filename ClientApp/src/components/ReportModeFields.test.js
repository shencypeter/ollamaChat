import {mount,flushPromises} from '@vue/test-utils'
import {reactive} from 'vue'
import {beforeEach,expect,it,vi} from 'vitest'
import {api} from '../api'
import ReportModeFields from './ReportModeFields.vue'
vi.mock('../api',()=>({api:vi.fn()}))
beforeEach(()=>api.mockReset())
function render(){
 const form=reactive({submissionMode:'group',allowLate:false,groupIds:[],allowDelegate:false,evaluationMode:'off',dimensions:['合作']})
 return {form,view:mount(ReportModeFields,{props:{form,course:'c'}})}
}
it('keeps grading policy separate from optional feedback and preserves the combined consensus mode',async()=>{
 api.mockResolvedValue([{id:'g',name:'第一組'}]);const {form,view}=render();await flushPromises()
 expect(view.text()).toContain('成績分配');expect(view.text()).toContain('個別調整')
 const feedback=view.get('select[aria-label="組員回饋方式"]');await feedback.setValue('consensus')
 expect(form.evaluationMode).toBe('consensus');expect(view.text()).toContain('同時收集組員互評與同分意願')
 const add=view.get('button[aria-label="新增評量面向"]');await add.trigger('click');await add.trigger('click');await add.trigger('click')
 expect(form.dimensions).toHaveLength(4);expect(view.get('button[aria-label="新增評量面向"]').attributes('disabled')).toBeDefined()
})
it('distinguishes loading, failure and a genuinely empty group list',async()=>{
 let reject;api.mockImplementation(()=>new Promise((_,r)=>{reject=r}));const {view}=render()
 expect(view.text()).toContain('載入課程小組');expect(view.text()).not.toContain('請先到課程分組建立小組')
 reject(new Error('暫時無法讀取'));await flushPromises();expect(view.get('[role="alert"]').text()).toContain('暫時無法讀取')
 expect(view.text()).not.toContain('請先到課程分組建立小組')
 api.mockResolvedValue([]);await view.get('button[aria-label="重新載入課程小組"]').trigger('click');await flushPromises()
 expect(view.text()).toContain('請先到課程分組建立小組')
})
