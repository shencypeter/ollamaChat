export function formatDate(value) {
  return value ? new Date(value).toLocaleString('zh-TW', { dateStyle: 'medium', timeStyle: 'short' }) : '未設定'
}
export function toLocalInput(value) {
  if (!value) return ''
  const date = new Date(value)
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
}
export function toUtc(value) { return value ? new Date(value).toISOString() : null }
export const assignmentStatus = { draft: '草稿', published: '已發布', closed: '已關閉' }
export const submissionStatus = { not_started: '尚未作答', draft: '草稿已儲存', submitted: '已提交', withdrawn: '已撤回' }
export const questionStages = [
  { key: 'premorbid', label: '病前功能' }, { key: 'illnessCourse', label: '疾病病程' },
  { key: 'functionalAssessment', label: '功能評估結果' }, { key: 'psychosocial', label: '心理社會條件' }
]
