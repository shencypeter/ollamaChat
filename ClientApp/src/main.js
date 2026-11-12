import {createApp} from 'vue'
import {createRouter,createWebHistory} from 'vue-router'
import App from './App.vue'
import LoginView from './views/LoginView.vue'
import AccountView from './views/AccountView.vue'
import CoursesView from './views/CoursesView.vue'
import CourseDetailView from './views/CourseDetailView.vue'
import UnitWorkspaceView from './views/UnitWorkspaceView.vue'
import AssessmentsView from './views/AssessmentsView.vue'
import ReportAssignmentView from './views/ReportAssignmentView.vue'
import AssignmentView from './views/AssignmentView.vue'
import LearningHistoryView from './views/LearningHistoryView.vue'
import LearningResourceView from './views/LearningResourceView.vue'
import InteractionProfileView from './views/InteractionProfileView.vue'
import StudentProfileView from './views/StudentProfileView.vue'
import StudentCaseView from './views/StudentCaseView.vue'
import TeachingStudioView from './views/TeachingStudioView.vue'
import StudioAdminView from './views/StudioAdminView.vue'
import {auth,loadSession} from './composables/auth'
import {experience,bindExperience,setStudentExperience,setCourseExperience} from './composables/experience'
import {api,resetCsrf} from './api'
import './style.css'
const router=createRouter({history:createWebHistory(),routes:[
 {path:'/login',component:LoginView,meta:{public:true,title:'S1 · 登入'}},
 {path:'/',component:StudentCaseView},
 {path:'/student/profile',component:StudentProfileView,meta:{profile:true,title:'學員檔案'}},
 {path:'/student/cases/:caseId?',component:StudentCaseView,meta:{learner:true,title:'S3 · 案例作答'}},
 {path:'/teaching',component:TeachingStudioView,meta:{staff:true,studioMode:'students',title:'T1 · 學生清單'}},
 {path:'/teaching/students/:studentId',component:TeachingStudioView,meta:{staff:true,studioMode:'history',title:'T2 · 學習歷程'}},
 {path:'/teaching/statistics',component:TeachingStudioView,meta:{staff:true,studioMode:'statistics',title:'T3 · 全班統計'}},
 {path:'/teaching/export',component:TeachingStudioView,meta:{staff:true,studioMode:'export',title:'T4 · 匯出'}},
 ...[{path:'/admin',mode:'overview',title:'A1 · 管理後台首頁'},{path:'/admin/accounts',mode:'accounts',title:'A2 · 帳號管理'},{path:'/admin/cases',mode:'cases',title:'A3 · 案例維護'},{path:'/admin/references',mode:'references',title:'A4 · 標準答案／重點清單'},{path:'/admin/templates',mode:'templates',title:'A5 · AI 回饋模板'},{path:'/admin/settings',mode:'settings',title:'A6 · 系統參數＋稽核軌跡'}].map(r=>({path:r.path,component:StudioAdminView,meta:{professor:!['cases','references'].includes(r.mode),staff:['cases','references'].includes(r.mode),adminMode:r.mode,title:r.title}})),
 {path:'/student/courses',component:CoursesView,props:{enrolled:true},meta:{title:'我的修課'}},
 {path:'/courses',component:CoursesView,meta:{title:'我的課程'}},
 {path:'/learning-history',component:LearningHistoryView,meta:{title:'我的學習歷程'}},
 {path:'/courses/:courseId/materials/:resourceId',component:LearningResourceView,meta:{title:'教材學習'}},
 {path:'/courses/:courseId/units/:unitId',component:UnitWorkspaceView,meta:{title:'單元工作區'}},
 {path:'/courses/:courseId',component:CourseDetailView,meta:{title:'課程工作區'}},
 ...['assessments','assessment-grades'].map(page=>({path:'/'+page,redirect:to=>to.query.course?{path:'/courses/'+to.query.course,query:{section:page==='assessments'?'assessments':'grades'}}:'/courses'})),
 {path:'/courses/:courseId/quizzes/:assignmentId',component:AssignmentView,meta:{quiz:true,title:'線上測驗'}},
 {path:'/courses/:courseId/assignments/:assignmentId',component:AssignmentView,meta:{title:'線上作業'}},
 {path:'/courses/:courseId/reports/:reportId',component:ReportAssignmentView,meta:{title:'PDF 報告作業'}},
 ...['quizzes','reports'].map(kind=>({path:'/'+kind+'/:id',component:CoursesView,beforeEnter:async to=>{
   try {const item=await api('/'+(kind==='quizzes'?'assignments':'reports')+'/'+to.params.id);return '/courses/'+item.courseId+to.path}
   catch{return '/courses'}
 }})),
 {path:'/learners/:studentId?',component:InteractionProfileView,meta:{title:'學員檔案'}},
 {path:'/account',component:AccountView,meta:{title:'個人資料'}},
 {path:'/account/password',component:AccountView,props:{password:true},meta:{title:'更改密碼'}},
 {path:'/:pathMatch(.*)*',redirect:'/'}
]})
router.beforeEach(async to=>{
 try{await loadSession()}catch{return to.path==='/login'?true:'/login'}
 if(!auth.user)return to.meta.public?true:'/login'
 if(to.path==='/login')return '/'
 const learner=!auth.user.canTeach||experience.student
 if(auth.user.hasStudentCourses && !experience.student && to.meta.learner){
  if(auth.profileComplete==null){try{auth.profileComplete=(await api('/studio/profile')).completed}catch{return '/login'}}
  if(!auth.profileComplete)return '/student/profile'
 }
 if(to.path==='/')return '/courses'
 if(to.meta.staff && (!auth.user.canTeach||experience.student))return '/courses'
 if(to.meta.professor && (auth.user.role!=='Administrator'||experience.student))return '/courses'

})
router.beforeResolve(async to=>{
 const scope=to.params.courseId
 if(to.query.view==='preview' && scope){
   try{const c=await api('/courses/'+scope);if(!c.canPractice || (c.status==='draft' && c.courseRole==='ta'))return {path:to.path,query:{section:'home',view:'teaching'}}}catch{return '/courses'}
   sessionStorage.setItem('preview-course:'+auth.user.id,scope)}
 if(to.query.view==='teaching'||!scope||sessionStorage.getItem('preview-course:'+auth.user?.id)!==scope){sessionStorage.removeItem('preview-course:'+auth.user?.id)}
 setStudentExperience(!!scope && sessionStorage.getItem('preview-course:'+auth.user?.id)===scope)
})
router.afterEach(()=>setCourseExperience(null))
window.addEventListener('portal-session-expired',()=>{auth.user=null;auth.profileComplete=null;bindExperience(null);resetCsrf();router.replace('/login')})
createApp(App).use(router).mount('#app')
