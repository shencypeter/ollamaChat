<script setup>
import { computed, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { GraduationCap, ArrowRight, ShieldCheck, Moon, Sun, BookOpen, Sparkles, Users } from '@lucide/vue'
import { auth, loadSession, signIn } from '../composables/auth'
import { theme, toggleTheme } from '../composables/theme'
const router = useRouter(), busy = ref(false), error = ref('')
const form = reactive({ email: '', password: '', displayName: '', setupToken: '' })
const setup = computed(() => auth.needsSetup)
async function submit() {
  busy.value = true; error.value = ''
  try { await signIn(form, setup.value); form.password = ''; form.setupToken = ''; await router.replace('/') } catch (e) { error.value = e.message } finally { busy.value = false }
}
async function retry() { try { await loadSession(true) } catch {} }
</script>
<template>
  <div class="login-layout"><section class="login-story"><RouterLink to="/" class="brand"><GraduationCap :size="28" /><span>NSYSU <b>Learn</b></span></RouterLink><div class="login-story-copy"><span class="eyebrow">A SPACE TO THINK TOGETHER</span><h1>讓每一次思考，<br>都有更好的回應。</h1><p>連結教師、助教與學生。<br>從案例分析到 AI 回饋，建立有證據的學習對話。</p><div class="login-feature"><BookOpen :size="20" /><span>四階段案例分析與學習歷程</span></div><div class="login-feature"><Users :size="20" /><span>教授、助教與學生，各司其職</span></div><div class="login-feature"><Sparkles :size="20" /><span>AI 文字回饋，不提供評分</span></div></div><small>國立中山大學 · E-learning proof of concept</small></section>
    <section class="login-panel"><button class="icon-button login-theme" :aria-label="theme === 'dark' ? '切換淺色模式' : '切換深色模式'" @click="toggleTheme"><Sun v-if="theme === 'dark'" :size="20" /><Moon v-else :size="20" /></button><div class="login-form-wrap"><div class="login-emblem"><GraduationCap :size="30" /></div><p class="eyebrow">S1 · CASE LEARNING</p><h2>{{ setup ? '建立第一位教授帳號' : '歡迎回到學習空間' }}</h2><p class="muted">{{ setup ? '使用本機設定檔中的初始化碼，啟用教學平台。' : '使用你的教學平台帳號登入。' }}</p>
      <div v-if="auth.error" class="error-box" role="alert">{{ auth.error }} <button class="text-button" @click="retry">重新連線</button></div>
      <form v-else @submit.prevent="submit" class="stack-form">
        <label v-if="setup">顯示名稱<input v-model="form.displayName" required maxlength="100" autocomplete="name" placeholder="你的姓名" /></label>
        <label>電子郵件<input v-model="form.email" type="email" required maxlength="254" autocomplete="username" placeholder="name@university.edu.tw" /></label>
        <label>密碼<input v-model="form.password" type="password" required :minlength="setup ? 12 : 1" maxlength="128" :autocomplete="setup ? 'new-password' : 'current-password'" :placeholder="setup ? '至少 12 個字元' : '輸入你的密碼'" /></label>
        <label v-if="setup">初始化碼<input v-model="form.setupToken" type="password" required autocomplete="off" placeholder="Portal:BootstrapToken" /></label>
        <p v-if="error" class="error-box" role="alert">{{ error }}</p>
        <button class="button primary login-submit" :disabled="busy">{{ busy ? '處理中…' : setup ? '建立並進入工作區' : '登入工作區' }}<ArrowRight :size="18" /></button>
      </form><p class="login-help"><ShieldCheck :size="16" />{{ setup ? '初始化僅能執行一次，後續帳號由教授建立。' : '還沒有帳號？請聯絡課程教授或平台管理者。' }}</p></div><small class="login-footer">NSYSU Learn · 教學平台 POC</small></section>
  </div>
</template>
