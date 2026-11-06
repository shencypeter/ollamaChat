<script setup>
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { LayoutDashboard, Sparkles, BookOpen, Users, Settings, LogOut, Moon, Sun, PanelLeftClose, GraduationCap, ArrowUpRight, Menu } from '@lucide/vue'
import { auth, roleLabels, signOut } from './composables/auth'
import { experience, setStudentExperience } from './composables/experience'
import { givenName } from './utils/names'
import { theme, toggleTheme } from './composables/theme'
const route = useRoute(), router = useRouter(), mobileOpen = ref(false), error = ref('')
const learner=computed(()=>!auth.user?.canTeach||experience.student)
const pages=computed(()=>{const links=learner.value?[
 {path:'/courses',label:'課程管理',icon:BookOpen,caption:'Courses'},
 {path:'/student/cases',label:'案例作答',icon:BookOpen,caption:'Cases'},
 {path:'/assessments',label:'測驗與 PDF 作業',icon:BookOpen,caption:'Assessments'},{path:'/assessment-grades',label:'測驗／作業成績',icon:LayoutDashboard,caption:'Grades'},
 ...(auth.user?.hasStudentCourses?[{path:'/student/profile',label:'基本資料',icon:Users,caption:'Profile'}]:[])
]:[
 {path:'/courses',label:'課程管理',icon:BookOpen,caption:'Courses'},
 {path:'/teaching',label:'學生清單',icon:Users,caption:'Students'},
 {path:'/teaching/statistics',label:'全班統計',icon:LayoutDashboard,caption:'Statistics'},
 {path:'/teaching/export',label:'匯出',icon:BookOpen,caption:'Export'},
 {path:'/assessments',label:'測驗與 PDF 作業',icon:BookOpen,caption:'Assessments'},
 {path:'/assessment-grades',label:'測驗／作業成績',icon:LayoutDashboard,caption:'Grades'},
 ...(auth.user?.hasStudentCourses?[{path:'/student/cases',label:'案例作答',icon:BookOpen,caption:'Cases'},{path:'/student/profile',label:'基本資料',icon:Users,caption:'Profile'}]:[]),
 ...(auth.user?.canTeach?[{path:'/admin/cases',label:'案例維護',icon:BookOpen,caption:'Cases'}]:[])
];if(auth.user?.hasStudentCourses)links.splice(1,0,{path:'/learning-history',label:'我的學習歷程',icon:GraduationCap,caption:'Learning'});if(auth.user?.role==='Administrator'&&!experience.student)links.push({path:'/admin',label:'管理後台',icon:Settings,caption:'Admin'});return links})
const activePage=computed(()=>route.path.startsWith('/courses/')?'/courses':route.path.startsWith('/quizzes/')||route.path.startsWith('/reports/')?'/assessments':route.path.startsWith('/admin')?'/admin':route.path.startsWith('/teaching/students/')?'/teaching':route.path.startsWith('/student/cases/')?'/student/cases':route.path)
const current=computed(()=>route.meta.title || '案例學習平台')
const userName = computed(() => givenName(auth.user?.displayName) || 'U')
const initials = computed(() => Array.from(userName.value).slice(0, 2).join(''))
async function switchView(){const next=!experience.student;const failure=await router.push('/account');if(failure && route.path!=='/account')return;setStudentExperience(next);mobileOpen.value=false;await router.push(next?'/student/cases':'/teaching')}
async function logout() { try { await signOut(); router.replace('/login') } catch (e) { error.value = e.message } }
</script>
<template>
  <RouterView v-if="!auth.user" />
  <div v-else class="workspace-shell">
    <a class="skip-link" href="#main-content">跳至主要內容</a>
    <header class="global-bar">
      <button class="icon-button mobile-menu" aria-label="開啟導覽" @click="mobileOpen = !mobileOpen"><Menu :size="20" /></button>
      <RouterLink to="/" class="brand"><span class="brand-icon"><GraduationCap :size="23" /></span><span>NSYSU <b>Learn</b></span><span class="poc-badge">POC</span></RouterLink>
      <span class="global-caption">教學、思考與協作，在同一個空間。</span>
      <div class="global-actions"><button v-if="experience.canSwitch && experience.contextCanPractice!==false" class="button secondary experience-toggle" type="button" :aria-pressed="experience.student" @click="switchView">{{ experience.student ? '返回教學視角' : '切換學生視角' }}</button><button class="icon-button" :aria-label="theme === 'dark' ? '切換淺色模式' : '切換深色模式'" @click="toggleTheme"><Sun v-if="theme === 'dark'" :size="19" /><Moon v-else :size="19" /></button><RouterLink to="/account" class="global-user" :aria-label="`${userName}，帳號設定`"><span class="avatar">{{ initials }}</span><span class="user-copy">{{ userName }}<small>{{ roleLabels[auth.user.role] }}</small></span></RouterLink></div>
    </header>
    <nav class="activity-rail" aria-label="應用程式"><RouterLink v-for="page in pages" :key="page.path" :to="page.path" :class="{ selected: activePage === page.path }" :aria-label="page.label"><component :is="page.icon" :size="22" /><span>{{ page.caption === 'AI portal' ? 'AI' : page.label.slice(0, 2) }}</span></RouterLink><RouterLink to="/account" class="rail-settings" aria-label="帳號設定"><Settings :size="22" /><span>設定</span></RouterLink></nav>
    <aside class="workspace-sidebar" :class="{ 'mobile-open': mobileOpen }">
      <div class="sidebar-title"><span class="workspace-monogram">學</span><div><strong>教學協作空間</strong><small>中山大學 · 臨床心理學</small></div></div>
      <p class="nav-heading">你的工作區</p>
      <nav aria-label="工作區導覽"><RouterLink v-for="page in pages" :key="page.path" :to="page.path" :class="{ selected: activePage === page.path }" @click="mobileOpen = false"><component :is="page.icon" :size="18" />{{ page.label }}<span v-if="page.path === '/ai'" class="mini-label">AI</span></RouterLink></nav>
      <div class="sidebar-divider"></div><p class="nav-heading">案例學習</p><p class="studio-sidebar-copy">案例原文、四階段作答與文字回饋。正式提交後由教師／助教檢視學習歷程。</p>
      <div class="sidebar-bottom"><div class="sidebar-note"><span class="live-dot"></span><strong>AI 輔助，教師把關</strong><p>讓證據與推理走在分數之前。</p><span class="field-hint">AI 提供文字回饋，不提供分數。</span></div><button class="sign-out" @click="logout"><LogOut :size="17" />登出帳號</button><p v-if="error" class="error" role="alert">{{ error }}</p></div>
    </aside>
    <main id="main-content" class="workspace-main" tabindex="-1"><div class="breadcrumb">工作區 <span>/</span> <strong>{{ current }}</strong><span class="breadcrumb-end">NSYSU E-LEARNING</span></div><p v-if="experience.student && experience.contextCanPractice!==false" class="experience-banner" role="status">學生體驗已開啟：在本人任教課程提交練習，不納入正式成績；本人原為學生的課程仍按正式學生身分作答。</p><RouterView :key="`${route.path}:${experience.student ? 'practice' : 'teaching'}`" /></main>
    <button v-if="mobileOpen" class="sidebar-backdrop" aria-label="關閉導覽" @click="mobileOpen = false"></button>
  </div>
</template>
