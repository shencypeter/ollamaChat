import {mount,flushPromises} from '@vue/test-utils'
import {it,expect,vi,beforeEach} from 'vitest'
import ResourceInteractions from './ResourceInteractions.vue'
import CourseQuestions from './CourseQuestions.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({useRoute:()=>({query:{}})}))
beforeEach(()=>api.mockReset())
it('shows counts and plain text comments while archived/practice interactions are read only',async()=>{
 api.mockResolvedValue({likes:2,liked:true,total:25,page:1,writable:false,items:[{id:'x',author:'合成',text:'<b>留言</b>',createdAt:'2026-11-09'}]})
 const w=mount(ResourceInteractions,{props:{courseId:'c',resourceId:'r'}});await flushPromises()
 expect(w.text()).toContain('已按讚 2');expect(w.text()).toContain('留言 25');expect(w.text()).toContain('<b>留言</b>');expect(w.find('b').exists()).toBe(false);expect(w.find('textarea').exists()).toBe(false)
 expect(w.findAll('.interaction-count')).toHaveLength(2);expect(w.findAll('.interaction-count').every(item=>item.classes().includes('button')&&item.classes().includes('secondary'))).toBe(true)
 expect(w.get('[aria-pressed]').attributes('disabled')).toBeDefined();w.unmount()
})
it('keeps the same request id after an uncertain comment write fails',async()=>{
 api.mockResolvedValue({likes:0,liked:false,total:0,page:1,writable:true,items:[]})
 const w=mount(ResourceInteractions,{props:{courseId:'c',resourceId:'r'}});await flushPromises();await w.get('textarea').setValue('合成留言')
 api.mockRejectedValueOnce(new Error('連線中斷'));await w.get('form').trigger('submit');await flushPromises()
 const first=api.mock.calls.find(c=>c[1]?.method==='POST')[1].body
 await w.get('form').trigger('submit');await flushPromises();const writes=api.mock.calls.filter(c=>c[1]?.method==='POST')
 expect(writes[1][1].body.id).toBe(first.id);expect(writes[1][1].body.text).toBe(first.text);w.unmount()
})
it('shows the discussion sidebar, moderator badges and preserves reply drafts on failures',async()=>{
 const threads={total:2,page:1,canAsk:true,canReply:true,canModerate:false,items:[
  {id:'q2',title:'第二篇',author:'合成學生',authorRole:'student',question:'未答問題',response:'',answeredAt:null,createdAt:'2026-11-10',locked:false,likes:2,liked:false,replyCount:1},
  {id:'q1',title:'第一篇',author:'合成教師',authorRole:'teacher',question:'課程說明',response:'',answeredAt:null,createdAt:'2026-11-09',locked:false,likes:0,liked:false,replyCount:0}
 ]}
 const replies={items:[{id:'r',author:'合成助教',authorRole:'ta',body:'助教回覆',createdAt:'2026-11-10',likes:1,liked:false}]}
 api.mockImplementation(url=>Promise.resolve(String(url).includes('/replies?')?replies:threads))
 const w=mount(CourseQuestions,{props:{course:{id:'c'}}});await flushPromises()
 expect(w.text()).toContain('所有討論串');expect(w.text()).toContain('新增討論');expect(w.text()).toContain('第二篇');expect(w.text()).toContain('第一篇');expect(w.text()).toContain('助教 · Moderator');expect(w.text()).toContain('後一篇')
 const reply=w.get('.reply-form textarea');await reply.setValue('合成答覆');api.mockRejectedValueOnce(new Error('連線中斷'));await w.get('.reply-form').trigger('submit');await flushPromises()
 expect(reply.element.value).toBe('合成答覆');expect(w.text()).toContain('連線中斷');w.unmount()
})
