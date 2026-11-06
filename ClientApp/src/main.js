import {createApp} from 'vue'
import {createRouter,createWebHistory} from 'vue-router'
import App from './App.vue'
import LoginView from './views/LoginView.vue'
import AccountView from './views/AccountView.vue'
import CoursesView from './views/CoursesView.vue'
import CourseDetailView from './views/CourseDetailView.vue'
import AssessmentsView from './views/AssessmentsView.vue'
import ReportAssignmentView from './views/ReportAssignmentView.vue'
import AssignmentView from './views/AssignmentView.vue'
import LearningHistoryView from './views/LearningHistoryView.vue'
import LearningResourceView from './views/LearningResourceView.vue'
import StudentProfileView from './views/StudentProfileView.vue'
import StudentCaseView from './views/StudentCaseView.vue'
import TeachingStudioView from './views/TeachingStudioView.vue'
import StudioAdminView from './views/StudioAdminView.vue'
import {auth,loadSession} from './composables/auth'
import {experience,bindExperience,setCourseExperience} from './composables/experience'
import {api,resetCsrf} from './api'
import './style.css'
const router=createRouter({history:createWebHistory(),routes:[
 {path:'/login',component:LoginView,meta:{public:true,title:'S1 · 登入'}},
 {path:'/',component:StudentCaseView},
 {path:'/student/profile',component:StudentProfileView,meta:{profile:true,title:'S2 · 基本資料'}},
 {path:'/student/cases/:caseId?',component:StudentCaseView,meta:{learner:true,title:'S3 · 案例作答'}},
 {path:'/teaching',component:TeachingStudioView,meta:{staff:true,studioMode:'students',title:'T1 · 學生清單'}},
 {path:'/teaching/students/:studentId',component:TeachingStudioView,meta:{staff:true,studioMode:'history',title:'T2 · 學習歷程'}},
 {path:'/teaching/statistics',component:TeachingStudioView,meta:{staff:true,studioMode:'statistics',title:'T3 · 全班統計'}},
 {path:'/teaching/export',component:TeachingStudioView,meta:{staff:true,studioMode:'export',title:'T4 · 匯出'}},
 ...[{path:'/admin',mode:'overview',title:'A1 · 管理後台首頁'},{path:'/admin/accounts',mode:'accounts',title:'A2 · 帳號管理'},{path:'/admin/cases',mode:'cases',title:'A3 · 案例維護'},{path:'/admin/references',mode:'references',title:'A4 · 標準答案／重點清單'},{path:'/admin/templates',mode:'templates',title:'A5 · AI 回饋模板'},{path:'/admin/settings',mode:'settings',title:'A6 · 系統參數＋稽核軌跡'}].map(r=>({path:r.path,component:StudioAdminView,meta:{professor:!['cases','references'].includes(r.mode),staff:['cases','references'].includes(r.mode),adminMode:r.mode,title:r.title}})),
 {path:'/courses',component:CoursesView,meta:{title:'課程管理'}},
 {path:'/learning-history',component:LearningHistoryView,meta:{title:'我的學習歷程'}},
 {path:'/courses/:courseId/materials/:resourceId',component:LearningResourceView,meta:{title:'教材學習'}},
 {path:'/courses/:courseId',component:CourseDetailView,meta:{title:'課程工作區'}},
 {path:'/assessments',component:AssessmentsView,meta:{title:'線上測驗與 PDF 作業'}},
 {path:'/assessment-grades',component:AssessmentsView,meta:{assessmentGrades:true,title:'測驗／作業成績管理'}},
 {path:'/quizzes/:assignmentId',component:AssignmentView,meta:{quiz:true,title:'線上／課後測驗'}},
 {path:'/reports/:reportId',component:ReportAssignmentView,meta:{title:'PDF 報告作業'}},
 {path:'/account',component:AccountView,meta:{title:'帳號設定'}},
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
router.afterEach(()=>setCourseExperience(null))
window.addEventListener('portal-session-expired',()=>{auth.user=null;auth.profileComplete=null;bindExperience(null);resetCsrf();router.replace('/login')})
createApp(App).use(router).mount('#app')
