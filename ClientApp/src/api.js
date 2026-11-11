let csrfToken = null
import { experience } from './composables/experience'
export function resetCsrf() { csrfToken = null }
export async function api(path, options = {}) {
  const method = options.method || 'GET'
  const mutating = !['GET', 'HEAD'].includes(method)
  if (mutating && !csrfToken) {
    const response = await fetch('/api/auth/csrf', { credentials: 'same-origin' })
    if (!response.ok) throw new Error('無法建立安全連線，請重新整理。')
    csrfToken = (await response.json()).token
  }
  const response = await fetch(`/api${path}`, {
    ...options, method, credentials: 'same-origin',
    headers: { ...(options.body !== undefined && !(options.body instanceof FormData) ? { 'Content-Type': 'application/json' } : {}), ...(mutating ? { 'X-CSRF-TOKEN': csrfToken } : {}), ...(experience.student && experience.canSwitch && !path.startsWith('/auth/') && path!=='/studio/profile' ? { 'X-Portal-View': 'student' } : {}), ...options.headers },
    body: options.body instanceof FormData ? options.body : options.body !== undefined ? JSON.stringify(options.body) : undefined,
  })
  const body = response.status === 204 ? null : options.download && response.ok ? await response.blob() : await response.json().catch(() => null)
  if (!response.ok) {
    const error = new Error(body?.error || (response.status === 401 ? '登入已逾時，請重新登入。' : response.status === 403 ? '你沒有執行此操作的權限。' : response.status === 429 ? '操作過於頻繁，請稍後再試。' : Object.values(body?.errors || {}).flat().join(' ') || '請求失敗，請稍後再試。'))
    error.status = response.status
    error.requestInspection = body?.requestInspection || null
    if (response.status === 401) window.dispatchEvent(new Event('portal-session-expired'))
    throw error
  }
  return body
}
