import {Home,ClipboardList,MessagesSquare,Users,ChartColumn,GraduationCap,Settings} from '@lucide/vue'
export function courseSections(course) {
  const staff=['teacher','ta'].includes(course.courseRole)
  return [
    {key:'home',label:'課程內容',icon:Home,badge:'materials'},
    {key:'assessments',label:'正式考試',icon:ClipboardList,badge:'assignments'},
    ...(staff?[{key:'students',label:'學員管理',icon:Users}]:[]),
    {key:'groups',label:staff?'課程分組':'我的小組',icon:Users},
    {key:'questions',label:'課程討論',icon:MessagesSquare,badge:'questions'},
    {key:'grades',label:staff?'課程總成績':'我的成績',icon:ChartColumn},
    ...(!staff||!course.taRestrictions?.includes('statistics')?[{key:'history',label:staff?'學習統計':'我的學習歷程',icon:GraduationCap}]:[]),
    ...(staff?[{key:'settings',label:'課程設定',icon:Settings}]:[])
  ]
}
