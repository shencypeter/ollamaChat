import { ref, watch } from 'vue'
const saved = (() => { try { return localStorage.getItem('portal-theme') } catch { return null } })()
export const theme = ref(saved === 'light' || saved === 'dark' ? saved : window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light')
watch(theme, value => { document.documentElement.dataset.theme = value; try { localStorage.setItem('portal-theme', value) } catch {} }, { immediate: true })
export function toggleTheme() { theme.value = theme.value === 'dark' ? 'light' : 'dark' }
