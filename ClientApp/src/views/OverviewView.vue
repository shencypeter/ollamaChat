<script setup>
import { computed, onMounted, ref } from 'vue'
import { ArrowUpRight, Sparkles, BookOpen, Users, ArrowRight, ShieldCheck, Layers3 } from '@lucide/vue'
import { auth, roleLabels } from '../composables/auth'
import { experience } from '../composables/experience'
import { api } from '../api'
import { givenName } from '../utils/names'
const courses = ref([]), error = ref(''), loading = ref(true)
onMounted(async () => { try { courses.value = await api('/courses') } catch(e) { error.value = e.message } finally { loading.value = false } })
const greeting = computed(() => `${givenName(auth.user.displayName)}，歡迎回來`)
</script>
<template>
  <section class="page-heading"><div><p class="eyebrow">LEARNING, CONNECTED</p><h1>{{ greeting }}</h1><p class="muted">從今天的教學與思考開始，讓學習持續發生。</p></div><span class="role-pill"><ShieldCheck :size="15" />{{ experience.student ? '學生體驗' : roleLabels[auth.user.role] }}工作區</span></section>
  <section class="welcome-banner"><div class="welcome-copy"><span class="banner-tag"><span class="live-dot"></span>AI 教學入口已就緒</span><h2>好的分析，<br>從問對問題開始。</h2><p>將案例證據、臨床推理與教學回饋放在同一個工作流程。</p><RouterLink class="button white" to="/ai">開始案例分析 <ArrowRight :size="17" /></RouterLink></div><div class="banner-art" aria-hidden="true"><div class="art-ring"></div><div class="art-card back"><Layers3 :size="26" /><span>Case evidence</span><i></i><i></i></div><div class="art-card front"><span class="art-spark"><Sparkles :size="24" /></span><strong>Think. Reflect. Learn.</strong><div class="art-lines"><i></i><i></i><i></i></div><span class="art-status">Evidence before scores</span></div><span class="art-dot one"></span><span class="art-dot two"></span></div></section>
  <div class="section-title"><div><h2>你的學習工具</h2><p>將時間留給教學，把整理交給工作區。</p></div></div>
  <div class="tool-grid"><RouterLink to="/ai" class="tool-card"><span class="tool-icon violet"><Sparkles :size="25" /></span><ArrowUpRight class="card-arrow" :size="19" /><h3>AI 教學入口</h3><p>四階段案例評閱、前後版本比較，以及獨立的 AI 對話。</p><span class="tool-link">開始獨立練習 <ArrowRight :size="15" /></span></RouterLink><RouterLink to="/courses" class="tool-card"><span class="tool-icon blue"><BookOpen :size="25" /></span><ArrowUpRight class="card-arrow" :size="19" /><h3>我的課程</h3><p>開啟課程作業、儲存作答草稿並完成提交，或查看學生的提交紀錄。</p><span class="tool-link">{{ loading ? '讀取課程中…' : `${courses.length} 門參與課程` }} <ArrowRight :size="15" /></span></RouterLink><RouterLink :to="(auth.user.role === 'Professor' && !experience.student) ? '/members' : '/account'" class="tool-card"><span class="tool-icon peach"><Users :size="25" /></span><ArrowUpRight class="card-arrow" :size="19" /><h3>{{ (auth.user.role === 'Professor' && !experience.student) ? '教學成員' : '個人帳號' }}</h3><p>{{ (auth.user.role === 'Professor' && !experience.student) ? '建立教授、助教與學生帳號，讓每個人進入自己的工作區。' : '查看你的平台角色與個人資訊，管理登入密碼。' }}</p><span class="tool-link">{{ (auth.user.role === 'Professor' && !experience.student) ? '管理平台成員' : '管理個人帳號' }} <ArrowRight :size="15" /></span></RouterLink></div>
  <p v-if="error" class="error-box" role="alert">{{ error }}</p>
  <section class="workspace-footnote"><ShieldCheck :size="21" /><div><strong>AI 是學習夥伴，評分仍需要專業判斷。</strong><p>AI 入口的結果為練習回饋，不會自動成為正式成績。未提供原始案例時，評分會明確標示為暫定。</p></div></section>
</template>
