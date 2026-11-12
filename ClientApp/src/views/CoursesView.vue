<script setup>
import {computed,onMounted,reactive,ref} from 'vue'
import {ArrowRight,BookOpen,Pencil,Plus,Send,Trash2,Users,X} from '@lucide/vue'
import {auth,loadSession} from '../composables/auth'
import {experience} from '../composables/experience'
import {api} from '../api'
import NotificationBadge from '../components/NotificationBadge.vue'
import {courseNotifications} from '../composables/notifications'

const props=defineProps({enrolled:Boolean})
const courses=ref([]),loading=ref(true),error=ref(''),notice=ref(''),showForm=ref(false),busy=ref(false)
const editingId=ref(null),nameDraft=ref(''),confirmDeleteId=ref(null)
const form=reactive({code:'',title:'',year:'',semester:'1',classLabel:''})
const teachingPortal=computed(()=>!props.enrolled && (auth.user.canTeach || auth.user.canCreateCourses))
const visibleCourses=computed(()=>courses.value.filter(c=>teachingPortal.value ? ['teacher','ta'].includes(c.courseRole) : c.courseRole==='student' || taughtPreview(c)))
const roles={teacher:'授課教師',ta:'課程助教',student:'學生'}

function taughtPreview(c){return props.enrolled && c.courseRole==='teacher'}
function courseLink(c){return {path:'/courses/'+c.id,query:taughtPreview(c)?{view:'preview'}:{}}}
async function load(){loading.value=true;error.value='';try{courses.value=await api(props.enrolled?'/courses?studentPortal=true':'/courses')}catch(e){error.value=e.message}finally{loading.value=false}}
async function create(){busy.value=true;error.value='';notice.value='';try{await api('/courses',{method:'POST',body:{code:form.code,title:form.title,classLabel:form.classLabel,termCode:form.year+'-'+form.semester}});Object.assign(form,{code:'',title:'',year:'',semester:'1',classLabel:''});showForm.value=false;await loadSession(true);await load()}catch(e){error.value=e.message}finally{busy.value=false}}
function editName(course){editingId.value=course.id;nameDraft.value=course.title;confirmDeleteId.value=null;error.value='';notice.value=''}
async function saveName(course){busy.value=true;error.value='';notice.value='';try{await api(`/courses/${course.id}`,{method:'PUT',body:{code:course.code,title:nameDraft.value,termCode:course.termCode,classLabel:course.classLabel||'',expectedVersion:course.version}});editingId.value=null;await load();notice.value='課程名稱已更新。'}catch(e){error.value=e.message}finally{busy.value=false}}
async function publish(course){busy.value=true;error.value='';notice.value='';try{await api(`/courses/${course.id}/activate`,{method:'POST'});await load();notice.value='課程已公開給學生。'}catch(e){error.value=e.message}finally{busy.value=false}}
function requestDelete(course){confirmDeleteId.value=course.id;editingId.value=null;error.value='';notice.value=''}
async function remove(course){busy.value=true;error.value='';notice.value='';try{await api(`/courses/${course.id}`,{method:'DELETE'});confirmDeleteId.value=null;await load();notice.value='空白草稿課程已刪除。'}catch(e){error.value=e.message}finally{busy.value=false}}
onMounted(load)
</script>

<template>
  <section class="page-heading">
    <div><h1>{{teachingPortal?'我的授課':'我的修課'}}</h1></div>
    <button v-if="teachingPortal && auth.user.canCreateCourses && !experience.student" class="button primary" @click="showForm=!showForm"><Plus :size="17"/>建立課程</button>
  </section>
  <p v-if="error" class="error-box" role="alert">{{error}} <button type="button" class="text-button" :disabled="loading" @click="load">重新載入課程</button></p>
  <p v-if="notice" class="success-box" role="status">{{notice}}</p>

  <section v-if="showForm" class="surface form-surface">
    <div class="section-title"><h2>建立課程</h2><button class="icon-button" aria-label="關閉建立課程" @click="showForm=false"><X :size="18"/></button></div>
    <form @submit.prevent="create"><fieldset :disabled="busy"><div class="form-grid three">
      <label>課程代碼<input v-model="form.code" required maxlength="40"/></label>
      <label>課程名稱<input v-model="form.title" required maxlength="200"/></label>
      <label>學年度<input v-model="form.year" type="number" min="1" max="999" required/></label>
      <label>學期<select v-model="form.semester"><option value="1">上學期</option><option value="2">下學期</option><option value="S">暑修</option></select></label>
      <label>班別<input v-model="form.classLabel" maxlength="80"/></label>
    </div><p class="field-hint">建立者成為課程擁有者；進入課程後可安排學員與相關評量。</p><button class="button primary">儲存課程</button></fieldset></form>
  </section>

  <p v-if="loading" class="empty-inline">載入課程中…</p>
  <section v-else-if="!error && !visibleCourses.length" class="surface course-empty">
    <BookOpen :size="32"/><h2>{{teachingPortal?'目前沒有授課課程':'目前沒有已選修的課程'}}</h2>
    <p>{{teachingPortal?'建立課程或請課程擁有者加入教學團隊。':(auth.user.canTeach?'這裡會顯示你任教的課程，以及你已選修並開放的課程。':'課程開放且你已加入學員名單後，就會出現在這裡。')}}</p>
  </section>

  <div v-else class="course-grid">
    <article v-for="course in visibleCourses" :key="course.id" class="surface course-card">
      <div class="course-card-top"><span class="course-emblem"><BookOpen :size="24"/></span><span class="status-badge">{{{draft:'草稿',active:'已公開',archived:'已封存'}[course.status]}}</span></div>
      <span class="eyebrow">{{course.code}} · {{course.termCode}} {{course.classLabel}}</span>
      <h2 :aria-label="course.title">{{course.title}} <NotificationBadge v-if="!taughtPreview(course)" :count="courseNotifications(course.id).total" label="本課未讀通知"/></h2>
      <div v-if="!taughtPreview(course)" class="course-notification-links">
        <RouterLink v-if="courseNotifications(course.id).materials" :to="'/courses/'+course.id+'?section=resources'">新教材 <NotificationBadge :count="courseNotifications(course.id).materials"/></RouterLink>
        <RouterLink v-if="courseNotifications(course.id).assignments" :to="`/courses/${course.id}?section=assessments`">新測驗／作業 <NotificationBadge :count="courseNotifications(course.id).assignments" label="新測驗與作業"/></RouterLink>
        <RouterLink v-if="courseNotifications(course.id).questions" :to="`/courses/${course.id}?section=questions`">學生新提問 <NotificationBadge :count="courseNotifications(course.id).questions" label="學生新提問"/></RouterLink>
      </div>
      <p class="muted">{{taughtPreview(course)?'學生視角（授課課程）':roles[course.courseRole]}}<span v-if="course.isOwner"> · 課程擁有者</span></p>
      <div class="course-meta"><span><Users :size="15"/>{{course.studentCount}} 位學員</span><span>{{course.resourceCount}} 份教材</span></div>
      <p class="field-hint">{{course.quizCount}} 份測驗 · {{course.reportCount}} 份 PDF 作業</p>

      <div v-if="teachingPortal&&course.isOwner&&!experience.student" class="course-quick-actions" aria-label="課程快速管理">
        <button class="button secondary" type="button" :disabled="busy" @click="editName(course)"><Pencil :size="14"/>修改名稱</button>
        <button v-if="course.status==='draft'" class="button primary" type="button" :disabled="busy" @click="publish(course)"><Send :size="14"/>公開課程</button>
        <button v-if="course.status==='draft'" class="button danger" type="button" :disabled="busy" @click="requestDelete(course)"><Trash2 :size="14"/>刪除課程</button>
      </div>
      <form v-if="editingId===course.id" class="quick-editor" @submit.prevent="saveName(course)">
        <label>課程名稱<input v-model="nameDraft" required maxlength="200" :disabled="busy"/></label>
        <div><button class="button primary" :disabled="busy||!nameDraft.trim()">儲存名稱</button><button class="button secondary" type="button" :disabled="busy" @click="editingId=null">取消</button></div>
      </form>
      <section v-if="confirmDeleteId===course.id" class="delete-confirm" role="alertdialog" :aria-label="`確認刪除 ${course.title}`">
        <strong>確認刪除「{{course.title}}」？</strong>
        <p>刪除不可復原；只有完全空白的草稿可刪除。已有成員、單元或教學資料時，伺服器會拒絕。</p>
        <div><button class="button danger" type="button" :disabled="busy" @click="remove(course)">確認刪除</button><button class="button secondary" type="button" :disabled="busy" @click="confirmDeleteId=null">取消</button></div>
      </section>
      <RouterLink :to="courseLink(course)" class="button primary course-open-link">進入課程 <ArrowRight :size="16"/></RouterLink>
    </article>
  </div>
</template>

<style scoped>
.course-quick-actions{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:7px;margin:0 0 12px}.course-quick-actions .button{padding:8px 7px;white-space:normal}.button.danger{background:var(--danger);color:white}.quick-editor,.delete-confirm{display:grid;gap:11px;padding:14px;margin:0 0 12px;border:1px solid var(--line);border-radius:8px;background:var(--surface-soft)}.quick-editor>div,.delete-confirm>div{display:flex;gap:8px}.quick-editor .button,.delete-confirm .button{width:auto}.delete-confirm{border-color:color-mix(in srgb,var(--danger) 32%,var(--line))}.delete-confirm p{color:var(--muted);font-size:11px}.course-open-link{margin-top:auto}@media(max-width:480px){.course-quick-actions{grid-template-columns:1fr}.quick-editor>div,.delete-confirm>div{flex-wrap:wrap}}
</style>
