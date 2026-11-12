import {Home,Library,ClipboardList,MessagesSquare,Users,ChartColumn,GraduationCap,Settings,ListTree} from '@lucide/vue'
export function courseSections(course) {
  const staff=['teacher','ta'].includes(course.courseRole)
  return [
    {key:'home',label:'課程首頁',icon:Home},
    {key:'units',label:course.courseRole==='teacher'?'單元管理':'課程單元',icon:ListTree},
    {key:'resources',label:staff?'教材管理':'教材',icon:Library,badge:'materials'},
    {key:'assessments',label:'正式考試',icon:ClipboardList,badge:'assignments'},
    ...(staff?[{key:'students',label:'學員管理',icon:Users}]:[]),
    {key:'groups',label:staff?'課程分組':'我的小組',icon:Users},
    {key:'questions',label:'課程討論',icon:MessagesSquare,badge:'questions'},
    {key:'grades',label:staff?'課程總成績':'我的成績',icon:ChartColumn},
    ...(!staff||!course.taRestrictions?.includes('statistics')?[{key:'history',label:staff?'學習統計':'我的學習歷程',icon:GraduationCap}]:[]),
    ...(staff?[{key:'settings',label:'課程設定',icon:Settings}]:[])
  ]
}
