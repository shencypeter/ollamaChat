import { reactive } from 'vue'
import { api, resetCsrf } from '../api'
import { bindExperience } from './experience'
export const auth = reactive({ user: null, needsSetup: false, loaded: false, error: '', profileComplete: null })
let pending
export async function loadSession(force = false) {
  if (auth.loaded && !force) return
  if (pending) return pending
  pending = api('/auth/session').then(data => { auth.user = data.user; bindExperience(data.user); auth.needsSetup = data.needsSetup; auth.loaded = true; auth.error = '' }).catch(error => { auth.error = error.message; throw error }).finally(() => { pending = null })
  return pending
}
export async function signIn(form, bootstrap = false) {
  auth.user = await api(bootstrap ? '/auth/bootstrap' : '/auth/login', { method: 'POST', body: form })
  bindExperience(auth.user)
  auth.profileComplete = null
  auth.needsSetup = false; auth.loaded = true; resetCsrf()
}
export async function signOut() {
  await api('/auth/logout', { method: 'POST' }); auth.user = null; auth.profileComplete=null; bindExperience(null); resetCsrf()
}
export const roleLabels = { Administrator: '管理員', Member: '一般成員' }
