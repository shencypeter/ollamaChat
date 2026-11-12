<script setup>
import { computed, ref, watch, onMounted, onBeforeUnmount, provide, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {BookOpen,Settings,LogOut,Moon,Sun,GraduationCap,Menu,X,UserRoundCog,ChevronDown,ChevronLeft,KeyRound} from '@lucide/vue'
import {api} from './api'
import {courseNotifications} from './composables/notifications'
import {courseSections} from './utils/courseNavigation'
import NotificationBadge from './components/NotificationBadge.vue'
import {notifications,refreshNotifications,resetNotifications} from './composables/notifications'
import { auth, roleLabels, signOut } from './composables/auth'
import { experience } from './composables/experience'
import { givenName } from './utils/names'
import { theme, toggleTheme } from './composables/theme'
const route = useRoute(), router = useRouter(), mobileOpen = ref(false), error = ref('')
watch(()=>[auth.user?.id,experience.student],()=>{resetNotifications();if(auth.user)refreshNotifications()},{immediate:true})
watch(()=>route.fullPath,()=>{if(auth.user)refreshNotifications()})
function visibleRefresh(){if(auth.user&&document.visibilityState==='visible')refreshNotifications()}
let notificationTimer
onMounted(()=>{notificationTimer=setInterval(visibleRefresh,30000);document.addEventListener('visibilitychange',visibleRefresh);window.addEventListener('focus',visibleRefresh)})
onBeforeUnmount(()=>{clearInterval(notificationTimer);document.removeEventListener('visibilitychange',visibleRefresh);window.removeEventListener('focus',visibleRefresh)})
const course=ref(null),menuButton=ref(null),drawer=ref(null)
provide('selectedCourse',course)
const courseId=computed(()=>route.params.courseId || (route.path.startsWith('/learners/') && route.query.course))
watch(()=>[courseId.value,experience.student,auth.user?.id],async ([id],_,cleanup)=>{
  let active=true
  cleanup(()=>{active=false})
  course.value=null
  if(!id || !auth.user)return
  try { const data=await api('/courses/'+id);if(active)course.value=data } catch { /* The view displays its access error. */ }
},{immediate:true})
const hasTeachingPortal=computed(()=>auth.user?.canTeach || auth.user?.canCreateCourses)
const studentPortal=computed(()=>route.path==='/student/courses' || experience.student || !hasTeachingPortal.value || (course.value?.courseRole==='student' && !course.value.canPractice))
const portalPath=computed(()=>studentPortal.value?'/student/courses':'/courses')
const portalLabel=computed(()=>studentPortal.value?'我的修課':'我的授課')
const sections=computed(()=>course.value?courseSections(course.value):[])
const section=computed(()=>route.params.assignmentId||route.params.reportId?'units':route.params.resourceId?'resources':route.query.section||'home')
const current=computed(()=>course.value?(sections.value.find(p=>p.key===section.value)?.label||route.meta.title):route.meta.title)
async function closeDrawer(focus=true){mobileOpen.value=false;await nextTick();if(focus)menuButton.value?.focus()}
async function openDrawer(){mobileOpen.value=true;await nextTick();drawer.value?.querySelector('a')?.focus()}
function keys(e){
  if(!mobileOpen.value)return
  if(e.key==='Escape'){e.preventDefault();closeDrawer();return}
  if(e.key!=='Tab')return
  const links=[...drawer.value.querySelectorAll('a,button')]
  if(e.shiftKey && document.activeElement===links[0]){e.preventDefault();links.at(-1).focus()}
  else if(!e.shiftKey && document.activeElement===links.at(-1)){e.preventDefault();links[0].focus()}
}
const wide=window.matchMedia('(min-width:761px)')
function resize(){if(wide.matches)mobileOpen.value=false}
onMounted(()=>{window.addEventListener('keydown',keys);wide.addEventListener('change',resize)})
onBeforeUnmount(()=>{window.removeEventListener('keydown',keys);wide.removeEventListener('change',resize)})
watch(()=>route.fullPath,async()=>{if(mobileOpen.value){await closeDrawer(false);document.getElementById('main-content')?.focus()}})
const userName = computed(() => givenName(auth.user?.displayName) || 'U')
const initials = computed(() => Array.from(userName.value).slice(0, 2).join(''))
async function switchView(){await router.push({path:'/courses/'+courseId.value,query:{section:'home',view:experience.student?'teaching':'preview'}})}
const accountOpen=ref(false),accountBox=ref(null),accountButton=ref(null)
function outside(event){if(!accountBox.value?.contains(event.target))accountOpen.value=false}
function closeAccount(){accountOpen.value=false;accountButton.value?.focus()}
watch(()=>[route.fullPath,auth.user?.id],()=>{accountOpen.value=false})
onMounted(()=>document.addEventListener('pointerdown',outside))
onBeforeUnmount(()=>document.removeEventListener('pointerdown',outside))
async function logout() { try { await signOut(); router.replace('/login') } catch (e) { error.value = e.message } }
</script>
<template>
  <RouterView v-if="!auth.user" />
  <div v-else class="workspace-shell">
    <a class="skip-link" href="#main-content">跳至主要內容</a>
    <header class="global-bar">
      <button ref="menuButton" class="icon-button mobile-menu" :aria-label="mobileOpen ? '關閉選單' : '開啟選單'" :aria-expanded="mobileOpen" aria-controls="workspace-navigation" @click="mobileOpen ? closeDrawer() : openDrawer()"><X v-if="mobileOpen" :size="20" /><Menu v-else :size="20" /><NotificationBadge :count="notifications.total" label="工作區未讀通知" /></button>
      <RouterLink to="/" aria-label="NSYSU Learn 首頁" class="brand"><span class="brand-icon"><GraduationCap :size="23" /></span><span>NSYSU <b>Learn</b></span></RouterLink>

      <div class="global-actions"><button class="icon-button" :aria-label="theme === 'dark' ? '切換淺色模式' : '切換深色模式'" @click="toggleTheme"><Sun v-if="theme === 'dark'" :size="19" /><Moon v-else :size="19" /></button><div ref="accountBox" class="account-control" @keydown.esc.stop="closeAccount" @focusout="!accountBox?.contains($event.relatedTarget) && (accountOpen=false)">
        <button ref="accountButton" type="button" class="global-user" :aria-label="userName+'，帳號選單'" aria-controls="account-actions" :aria-expanded="accountOpen" @click="accountOpen=!accountOpen"><span class="avatar">{{ initials }}</span><span class="user-copy">{{userName}}<small>{{roleLabels[auth.user.role]}}</small></span><ChevronDown :size="15"/></button>
        <nav v-show="accountOpen" id="account-actions" aria-label="帳號操作" @click="accountOpen=false">
          <RouterLink to="/account"><UserRoundCog :size="16"/>個人資料</RouterLink>
          <RouterLink to="/account/password"><KeyRound :size="16"/>更改密碼</RouterLink>
          <button type="button" @click.stop="logout"><LogOut :size="16"/>登出</button>
          <p v-if="error" role="alert">{{error}}</p>
        </nav></div></div>
    </header>
    <aside ref="drawer" id="workspace-navigation" class="workspace-sidebar" :class="{ 'mobile-open': mobileOpen }" :role="mobileOpen?'dialog':undefined" :aria-modal="mobileOpen||undefined" aria-label="平台導覽">
      <nav aria-label="工作區導覽">
        <template v-if="course">
          <RouterLink :to="portalPath" class="course-exit" :aria-label="'返回'+portalLabel"><ChevronLeft :size="20"/>返回{{portalLabel}}</RouterLink>
          <header class="sidebar-course"><strong>{{course.title}}</strong><span>{{course.code}} · {{course.termCode}} {{course.classLabel}}</span></header>
          <RouterLink v-for="p in sections" :key="p.key" :to="{path:'/courses/'+course.id,query:{section:p.key}}" :class="{selected:section===p.key}" :aria-current="section===p.key?'page':undefined">
            <component :is="p.icon" :size="18"/>{{p.label}} <NotificationBadge v-if="p.badge" :count="courseNotifications(course.id)[p.badge]"/>
          </RouterLink>
          <RouterLink v-if="course.canPractice && !experience.student" to="/student/courses"><GraduationCap :size="18"/>學生視角 · 我的修課</RouterLink>
          <button v-if="course?.canPractice && (course.status!=='draft' || course.courseRole!=='ta')" class="button secondary experience-toggle" type="button" :aria-pressed="experience.student" @click="switchView">{{ experience.student ? '返回教師視角' : '學生視角（預覽）' }}</button>
        </template>
        
        <template v-else>
          <RouterLink v-if="hasTeachingPortal" to="/courses" :class="{selected:route.path==='/courses'}"><BookOpen :size="18"/>我的授課</RouterLink>
          <RouterLink to="/student/courses" :class="{selected:route.path==='/student/courses'||(!hasTeachingPortal&&route.path==='/courses')}"><GraduationCap :size="18"/>學生視角 · 我的修課</RouterLink>
        </template>
        <RouterLink v-if="auth.user.role==='Administrator'&&!experience.student" to="/admin"><Settings :size="18"/>管理後台</RouterLink>
      </nav>
      
    </aside>
    <main id="main-content" class="workspace-main" tabindex="-1" :inert="mobileOpen"><nav class="breadcrumb" aria-label="所在位置"><RouterLink :to="portalPath">{{portalLabel}}</RouterLink><template v-if="course"><span>/</span><RouterLink :to="'/courses/'+course.id">{{course.title}}</RouterLink></template><template v-if="!['/courses','/student/courses'].includes(route.path)"><span>/</span><strong>{{current}}</strong></template></nav>
      <p v-if="experience.student" class="experience-banner" role="status">學生視角預覽 · 唯讀，無法交卷、評分或累積學習紀錄。</p>
      <RouterView :key="route.path+':'+experience.student" />
    </main>
    <button v-if="mobileOpen" class="sidebar-backdrop" aria-label="關閉導覽" tabindex="-1" @click="closeDrawer()"></button>
  </div>
</template>

<style scoped>
.sidebar-course{margin:4px 0 10px;padding:12px;border-inline-start:4px solid var(--accent);background:var(--accent-soft);border-radius:4px;display:grid;gap:5px;overflow-wrap:anywhere}
.sidebar-course strong{font-size:1.2rem;line-height:1.35;color:var(--text)}
.sidebar-course span{font-size:.82rem;color:var(--muted)}
.workspace-sidebar .course-exit{color:var(--accent);font-weight:650}

.account-control{position:relative}.account-control>button{background:transparent;color:inherit;border:0;cursor:pointer}.account-control nav{position:absolute;right:0;top:calc(100% + 8px);z-index:80;min-width:160px;padding:5px;background:var(--surface);color:var(--text);border:1px solid var(--line);border-radius:8px;box-shadow:0 5px 20px #0002}.account-control nav :is(a,button){display:flex;width:100%;align-items:center;gap:10px;background:transparent;color:inherit;border:0;padding:10px;cursor:pointer;font:inherit;text-align:left}.account-control nav :is(a,button):hover{background:var(--accent-soft)}
</style>
