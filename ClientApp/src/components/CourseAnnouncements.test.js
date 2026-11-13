import {mount,flushPromises} from '@vue/test-utils'
import {beforeEach,it,expect,vi} from 'vitest'
import CourseAnnouncements from './CourseAnnouncements.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({useRoute:()=>({query:{}})}))
beforeEach(()=>{api.mockReset()})
it('persists a teacher announcement and retains the editor when saving fails',async()=>{
 api.mockImplementation(async(p,o)=>{if(o)throw new Error('儲存失敗');return {canManage:true,items:[]}})
 const v=mount(CourseAnnouncements,{props:{course:{id:'c',courseRole:'teacher'}}});await flushPromises()
 await v.get('button[aria-label="新增課程公告"]').trigger('click');await v.get('input[aria-label="公告標題"]').setValue('英文期中考範圍');await v.get('textarea').setValue('課本前三單元');await v.get('select').setValue('published');await v.get('form').trigger('submit');await flushPromises()
 expect(api).toHaveBeenCalledWith('/courses/c/announcements',{method:'POST',body:{title:'英文期中考範圍',body:'課本前三單元',status:'published'}})
 expect(v.get('input').element.value).toBe('英文期中考範圍');expect(v.text()).toContain('儲存失敗');v.unmount()
})
it('renders permitted announcements as plain text and no student management controls',async()=>{
 api.mockResolvedValue({canManage:false,items:[{id:'n',title:'上課提醒',body:'<script>danger</script>',status:'published',authorName:'王老師'}]})
 const v=mount(CourseAnnouncements,{props:{course:{id:'c',courseRole:'student'}}});await flushPromises()
 expect(v.text()).toContain('王老師');expect(v.text()).toContain('<script>danger</script>');expect(v.find('script').exists()).toBe(false);expect(v.find('form').exists()).toBe(false);expect(v.find('button[aria-label="新增課程公告"]').exists()).toBe(false);v.unmount()
})
