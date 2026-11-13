import {flushPromises,mount} from '@vue/test-utils'
import {beforeEach,expect,it,vi} from 'vitest'
import AssessmentsView from './AssessmentsView.vue'
import {api} from '../api'
const route=vi.hoisted(()=>({meta:{},query:{}}))
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({useRoute:()=>route,useRouter:()=>({}),onBeforeRouteLeave:vi.fn(),onBeforeRouteUpdate:vi.fn()}))
vi.mock('../composables/notifications',()=>({markNotificationsRead:vi.fn()}))
beforeEach(()=>{route.query={};api.mockReset();api.mockResolvedValue({staff:true,quizzes:[],reports:[]})})
it('expands the existing online exam area for an online agenda link',async()=>{
 route.query={onlineExam:'online-id'}
 const view=mount(AssessmentsView,{props:{boundCourse:{id:'c',title:'課程',status:'active'},embedded:true},global:{stubs:{OfflineExamGrades:true,ExamManager:true,RouterLink:true}}});await flushPromises()
 expect(view.get('details.online-exams').attributes('open')).toBeDefined();view.unmount()
})
it('defaults formal exams to the paper gradebook workspace and retains online exams in a closed labeled section',async()=>{
 const view=mount(AssessmentsView,{props:{boundCourse:{id:'c',title:'課程',status:'active'},embedded:true},global:{stubs:{OfflineExamGrades:{template:'<section data-testid="paper-grades">紙本考試全班登分</section>'},ExamManager:{template:'<section data-testid="online-manager">線上考試功能</section>'},RouterLink:true}}});await flushPromises()
 expect(view.find('[data-testid="paper-grades"]').exists()).toBe(true)
 const online=view.get('details.online-exams')
 expect(online.get('summary').text()).toBe('線上正式考試（既有功能）')
 expect(online.attributes('open')).toBeUndefined()
 expect(online.find('[data-testid="online-manager"]').exists()).toBe(true)
 view.unmount()
})
