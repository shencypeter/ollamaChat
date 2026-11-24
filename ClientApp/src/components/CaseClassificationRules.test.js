import {mount,flushPromises} from '@vue/test-utils'
import {afterEach,beforeEach,expect,it,vi} from 'vitest'
import CaseClassificationRules from './CaseClassificationRules.vue'
import {api} from '../api'
import {auth} from '../composables/auth'
import {experience} from '../composables/experience'
vi.mock('../api',()=>({api:vi.fn()}))
const base='/courses/c/case-analysis/rules'
const rules=()=>({version:2,definitions:['A','B','C','D'].map(phase=>({code:phase+'1',phase,label:phase+'分類',description:'原說明',keywords:['關鍵詞'],enabled:true})),updatedAt:'2026-11-20T00:00:00Z',updatedBy:'合成教師'})
let wrappers=[]
function view(extra={}){const v=mount(CaseClassificationRules,{attachTo:document.body,props:{course:{id:'c',courseRole:'teacher'},config:{canMaintainRules:true},...extra}});wrappers.push(v);return v}
async function open(v){await v.get('[data-action="edit-rules"]').trigger('click');await flushPromises()}
async function edit(v,code='A1'){await v.get(`[data-edit-rule="${code}"]`).trigger('click');await flushPromises()}
async function create(v,code='A1a',label='合成子分類'){await v.get('[data-action="new-rule"]').trigger('click');const fields=v.get('.new-rule').findAll('input');await fields[0].setValue(code);await fields[1].setValue(label);await v.get('[data-action="add-rule"]').trigger('click')}
beforeEach(()=>{auth.user=null;experience.student=false;api.mockReset().mockResolvedValue(rules())})
afterEach(()=>{wrappers.forEach(v=>v.unmount());wrappers=[];vi.restoreAllMocks()})
it('loads only when the teacher opens the modal and shows the saved version attribution',async()=>{
 const v=view();await flushPromises();expect(api).not.toHaveBeenCalled();await open(v);expect(api.mock.calls[0][0]).toBe(base);expect(v.text()).toContain('規則版本：2');expect(v.text()).toContain('合成教師')
})
it('keeps established classifications immutable in identity and allows disabling instead of removing them',async()=>{
 const data=rules();data.definitions.forEach(item=>{item.enabled=item.code==='A1'});api.mockResolvedValueOnce(data)
 const v=view();await open(v);await edit(v);expect(v.get('[data-rule-code="A1"]').find('[data-action="remove-rule"]').exists()).toBe(false)
 await v.get('[data-rule-code="A1"] input[type="checkbox"]').setValue(false);expect(v.get('[data-action="save-rules"]').element.disabled).toBe(true)
})
it('saves names, descriptions, keyword lists and enabled flags with the expected version',async()=>{
 const v=view();await open(v);await edit(v);const item=v.get('[data-rule-code="A1"]');await item.get('input:not([type="checkbox"])').setValue(' 新名稱 ');await item.findAll('textarea')[0].setValue(' 新說明 ');await item.findAll('textarea')[1].setValue('功能\n日常生活，照護')
 api.mockResolvedValueOnce({...rules(),version:3});await v.get('[data-action="save-rules"]').trigger('click');await flushPromises()
 expect(api.mock.calls.at(-1)[1]).toMatchObject({method:'PUT',body:{expectedVersion:2,definitions:expect.arrayContaining([expect.objectContaining({code:'A1',label:'新名稱',description:'新說明',keywords:['功能','日常生活','照護']})])}})
 expect(api.mock.calls.at(-1)[1].body.definitions[0]).not.toHaveProperty('keywordsText');expect(v.text()).toContain('後續 AI 分析使用新版本');expect(v.emitted('saved')[0][0].version).toBe(3)
})
it('adds a valid subcode and can remove it before the first save without altering saved identities',async()=>{
 const v=view();await open(v);await create(v)
 expect(v.get('[data-rule-code="A1a"] input:not([type="checkbox"])').element.value).toBe('合成子分類');await v.get('[data-rule-code="A1a"] [data-action="remove-rule"]').trigger('click');expect(v.find('[data-rule-code="A1a"]').exists()).toBe(false)
})
it('rejects subcodes with missing parent identities and preserves the pending new rule fields',async()=>{
 const v=view();await open(v);await v.get('[data-action="new-rule"]').trigger('click');const fields=v.get('.new-rule').findAll('input');await fields[0].setValue('A9a');await fields[1].setValue('缺少父分類');await v.get('[data-action="add-rule"]').trigger('click')
 expect(v.find('[data-rule-code="A9a"]').exists()).toBe(false);expect(v.get('[role="alert"]').text()).toContain('父分類');expect(fields[0].element.value).toBe('A9a')
})
it('removes only the pending new category while preserving existing parent and independent codes',async()=>{
 const data=rules();data.definitions.push(...['A7','A7a'].map(code=>({code,phase:'A',label:code,enabled:true,keywords:[]})));api.mockResolvedValueOnce(data)
 const v=view();await open(v);await create(v,'A70','新分類 A70');await v.get('[data-rule-code="A70"] [data-action="remove-rule"]').trigger('click')
 expect(v.find('[data-edit-rule="A7"]').exists()).toBe(true);expect(v.find('[data-edit-rule="A7a"]').exists()).toBe(true);expect(v.find('[data-edit-rule="A70"]').exists()).toBe(false)
})
it('keeps existing local edits after a version conflict and reloads only on an explicit action',async()=>{
 const v=view();await open(v);await edit(v);await v.get('[data-rule-code="A1"] input:not([type="checkbox"])').setValue('我的修改');api.mockRejectedValueOnce(Object.assign(new Error('規則版本已更新，請重新載入。'),{status:409}))
 await v.get('[data-action="save-rules"]').trigger('click');await flushPromises();expect(v.get('[role="alert"]').text()).toContain('版本已更新');expect(v.get('[data-rule-code="A1"] input:not([type="checkbox"])').element.value).toBe('我的修改')
 await v.get('[data-action="reload-rules"]').trigger('click');await flushPromises();await edit(v);expect(v.get('[data-rule-code="A1"] input:not([type="checkbox"])').element.value).toBe('A分類')
})
it('keeps the saved editor open when the parent refreshes classifications without changing authority',async()=>{
 const v=view();await open(v);await edit(v);api.mockResolvedValueOnce({...rules(),version:3});await v.get('[data-action="save-rules"]').trigger('click');await flushPromises()
 await v.setProps({config:{canMaintainRules:true,classifications:rules().definitions}})
 expect(v.get('dialog').element.open).toBe(true);expect(v.text()).toContain('規則版本：3');expect(v.find('[data-edit-rule="A1"]').exists()).toBe(true);expect(v.find('[data-rule-code]').exists()).toBe(false)
})
it.each([{course:{id:'c',courseRole:'ta'}},{course:{id:'c',courseRole:'student'}},{config:{canMaintainRules:false}}])('does not render maintenance for a forbidden role or flag (%j)',async extra=>{
 const v=view(extra);await flushPromises();expect(v.find('details').exists()).toBe(false);expect(api).not.toHaveBeenCalled()
})
it('clears loaded rules immediately when entering student experience and ignores a late update',async()=>{
 const v=view();await open(v);await edit(v);let finish;api.mockImplementationOnce(()=>new Promise(resolve=>{finish=resolve}));await v.get('[data-action="save-rules"]').trigger('click');experience.student=true;await flushPromises();expect(v.find('dialog').exists()).toBe(false)
 finish({...rules(),version:3});await flushPromises();expect(v.emitted('saved')).toBeUndefined()
})
it('keeps the editor list in a named modal with lazy loading and returns focus on cancel',async()=>{
 const v=view();await flushPromises();expect(v.find('[data-rule-code]').exists()).toBe(false);expect(v.get('dialog').element.open).toBe(false)
 const opener=v.get('[data-action="edit-rules"]');opener.element.focus();await open(v)
 const dialog=v.get('dialog');expect(dialog.element.open).toBe(true);expect(dialog.attributes('aria-modal')).toBe('true');expect(dialog.attributes('aria-labelledby')).toBe(v.get('h2').attributes('id'));expect(dialog.find('[data-edit-rule="A1"]').exists()).toBe(true);expect(dialog.find('textarea').exists()).toBe(false)
 expect(document.activeElement).toBe(v.get('[data-action="close-rules"]').element)
 await edit(v);await v.get('[data-rule-code="A1"] textarea').setValue('未儲存說明');await v.get('[data-action="cancel-rules"]').trigger('click')
 expect(dialog.element.open).toBe(false);expect(document.activeElement).toBe(opener.element);expect(api).toHaveBeenCalledTimes(1)
 await open(v);await edit(v);expect(v.get('[data-rule-code="A1"] textarea').element.value).toBe('原說明');expect(api).toHaveBeenCalledTimes(1)
 await v.get('[data-action="save-rules"]').trigger('click');await flushPromises();expect(api.mock.calls.at(-1)[1].signal.aborted).toBe(false)
})
it('discards a pending new rule on Escape but retains successfully saved rules on reopening',async()=>{
 const v=view();await open(v);await edit(v);api.mockResolvedValueOnce({...rules(),version:3});await v.get('[data-action="save-rules"]').trigger('click');await flushPromises()
 await create(v,'A7','未儲存分類')
 await v.get('dialog').trigger('cancel');expect(v.get('dialog').element.open).toBe(false);await open(v)
 expect(v.find('[data-edit-rule="A7"]').exists()).toBe(false);expect(v.find('.new-rule').exists()).toBe(false);expect(v.text()).toContain('規則版本：3')
})
it('aborts a dismissed load and ignores its late result before a fresh open',async()=>{
 let finish;api.mockImplementationOnce(()=>new Promise(resolve=>{finish=resolve}));const v=view();await open(v)
 const signal=api.mock.calls[0][1].signal;await v.get('[data-action="cancel-rules"]').trigger('click');expect(signal.aborted).toBe(true)
 await open(v);finish({...rules(),version:99});await flushPromises();expect(v.text()).toContain('規則版本：2');expect(v.text()).not.toContain('規則版本：99')
})
it('keeps an in-flight save open until its outcome is known',async()=>{
 const v=view();await open(v);await edit(v);let finish;api.mockImplementationOnce(()=>new Promise(resolve=>{finish=resolve}));await v.get('[data-action="save-rules"]').trigger('click')
 expect(v.get('[data-action="close-rules"]').element.disabled).toBe(true);expect(v.get('[data-action="cancel-rules"]').element.disabled).toBe(true)
 await v.get('dialog').trigger('cancel');expect(v.get('dialog').element.open).toBe(true);finish({...rules(),version:3});await flushPromises()
 await v.get('[data-action="close-rules"]').trigger('click');expect(v.get('dialog').element.open).toBe(false);expect(v.emitted('saved')[0][0].version).toBe(3)
})
it('shows a compact list and edits only one category, discarding canceled changes before switching',async()=>{
 const v=view();await open(v);expect(v.findAll('[data-edit-rule]')).toHaveLength(4);expect(v.find('textarea').exists()).toBe(false);expect(v.find('[data-action="save-rules"]').exists()).toBe(false)
 await edit(v);expect(v.findAll('[data-rule-code]')).toHaveLength(1);expect(v.get('[data-rule-code]').attributes('data-rule-code')).toBe('A1');expect(v.find('[data-edit-rule]').exists()).toBe(false)
 await v.get('[data-rule-code="A1"] textarea').setValue('取消的說明');await v.get('[data-action="back-rules"]').trigger('click');await flushPromises();expect(document.activeElement).toBe(v.get('[data-edit-rule="A1"]').element)
 await edit(v,'B1');await v.get('[data-rule-code="B1"] textarea').setValue('只改B1')
 api.mockImplementationOnce(async(path,options)=>({...rules(),version:3,definitions:options.body.definitions}));await v.get('[data-action="save-rules"]').trigger('click');await flushPromises()
 const payload=api.mock.calls.at(-1)[1].body;expect(payload.definitions.find(item=>item.code==='A1').description).toBe('原說明');expect(payload.definitions.find(item=>item.code==='B1').description).toBe('只改B1');expect(payload.definitions.filter(item=>item.code!=='B1')).toEqual(rules().definitions.filter(item=>item.code!=='B1'))
 expect(v.find('textarea').exists()).toBe(false);expect(v.findAll('[data-edit-rule]')).toHaveLength(4);await edit(v,'B1');expect(v.get('[data-rule-code="B1"] textarea').element.value).toBe('只改B1')
})
