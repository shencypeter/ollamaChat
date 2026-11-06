import { test, expect } from '@playwright/test'
import { readFileSync } from 'node:fs'
test.skip(process.env.OLLAMA_INSPECTION_STUB !== '1', 'Uses the loopback synthetic Ollama fixture only.')
test.use({ actionTimeout: 15000 })
const state = JSON.parse(readFileSync(new URL('../../../.codex-build/portal-smoke.json', import.meta.url), 'utf8'))
const professor = state.accounts.find(a => a.role === 'Professor'), ta = state.accounts.find(a => a.role === 'TeachingAssistant'), student = state.accounts.find(a => a.role === 'Student')
async function post(request, path, data, practice = false) { const token = (await (await request.get('/api/auth/csrf')).json()).token; return request.post(path, { headers: { 'X-CSRF-TOKEN': token, ...(practice ? { 'X-Portal-View': 'student' } : {}) }, ...(data ? { data } : {}) }) }
async function get(request, path, practice = false) { return request.get(path, { headers: practice ? { 'X-Portal-View': 'student' } : {} }) }
async function login(request, user) { await post(request, '/api/auth/logout'); expect((await post(request, '/api/auth/login', { email: user.email, password: state.password })).status()).toBe(200) }
test('professor and TA use independent student practice; official students and grades stay isolated', async ({ request, page, playwright, baseURL }) => {
  await login(request, professor)
  const course = await (await post(request, '/api/courses', { code: `VIEW-${state.run}-${Date.now()}`, title: '學生視角合成課程', termCode: '115-1' })).json()
  for (const [user, role] of [[ta, 'ta'], [student, 'student']]) await post(request, `/api/courses/${course.id}/members`, { userId: user.id, courseRole: role })
  const create = title => post(request, `/api/courses/${course.id}/assignments`, { title, instructions: '合成案例：能在提示下完成指定任務。', questions: [{ promptText: '分析證據限制。', stageCode: 'premorbid' }] })
  const assignment = await (await create('已發布體驗作業')).json(), draft = await (await create('教師草稿不可見')).json()
  await post(request, `/api/assignments/${assignment.id}/publish`)
  expect((await get(request, `/api/assignments/${draft.id}`, true)).status()).toBe(404)
  expect((await get(request, `/api/courses/${course.id}/members`, true)).status()).toBe(403)
  expect((await get(request, '/api/accounts', true)).status()).toBe(403)
  expect((await post(request, '/api/courses', { code: 'blocked', title: 'blocked', termCode: 'test' }, true)).status()).toBe(403)
  const identity = await (await request.get('/api/auth/session')).json(); expect(identity.user.id).toBe(professor.id); expect(identity.user.role).toBe('Professor')
  const assistant = await playwright.request.newContext({ baseURL }); await login(assistant, ta)
  const questionId = assignment.questions[0].id, feedbackPath = `/api/assignments/${assignment.id}/questions/${questionId}/ai-feedback`
  async function practice(client, text) {
    const savedResponse = await post(client, `/api/assignments/${assignment.id}/my-submission`, { expectedVersion: 0, answers: [{ questionId, text }] }, true); expect(savedResponse.status()).toBe(200); const saved = await savedResponse.json()
    const response = await post(client, feedbackPath, { expectedVersion: saved.version, answerRevisionId: saved.answers[0].currentRevisionId }, true); expect(response.status()).toBe(403)
    return saved
  }
  const p = await practice(request, '教授自己的練習'), t = await practice(assistant, '助教自己的練習'); expect(p.id).not.toBe(t.id)
  const taSubmitted = await post(assistant, `/api/assignments/${assignment.id}/my-submission`, { expectedVersion: t.version, submit: true, answers: [{ questionId, text: '助教自己的練習' }] }, true); expect(taSubmitted.status()).toBe(403)
  expect((await get(request, feedbackPath, true)).status()).toBe(403)
  const finalResponse = await post(request, `/api/assignments/${assignment.id}/my-submission`, { expectedVersion: p.version, submit: true, answers: [{ questionId, text: '教授自己的練習' }] }, true); expect(finalResponse.status()).toBe(403)
  expect(await (await get(request, `/api/assignments/${assignment.id}/submissions/${p.id}/revisions`, true)).json()).toEqual([]); const snapshotId = '00000000-0000-0000-0000-000000000001'
  expect((await get(assistant, `/api/assignments/${assignment.id}/submissions/${p.id}/revisions`, true)).status()).toBe(403)
  expect(await (await get(request, `/api/assignments/${assignment.id}/submissions`)).json()).toEqual([])
  expect((await get(request, `/api/assignments/${assignment.id}/submissions/${p.id}`)).status()).toBe(404)
  expect((await post(request, `/api/assignments/${assignment.id}/revisions/${snapshotId}/grades`, { score: 100, criteria: [], feedback: 'must not grade practice' })).status()).toBe(404)
  await assistant.dispose()
  const learner = await playwright.request.newContext({ baseURL }); await login(learner, student)
  expect((await get(learner, `/api/assignments/${assignment.id}`, true)).status()).toBe(403)
  expect((await get(learner, feedbackPath)).status()).toBe(403)
  const official = await post(learner, `/api/assignments/${assignment.id}/my-submission`, { expectedVersion: 0, submit: true, answers: [{ questionId, text: '正式學生作答' }] }); expect(official.status()).toBe(200)
  const roster = await (await get(request, `/api/assignments/${assignment.id}/submissions`)).json(); expect(roster).toHaveLength(1); expect(roster[0].studentUserId).toBe(student.id)
  await learner.dispose()
  await page.goto('/login'); await page.getByLabel('電子郵件', { exact: true }).fill(professor.email); await page.getByLabel('密碼', { exact: true }).fill(state.password); await page.getByRole('button', { name: '登入工作區', exact: true }).click(); await expect(page).toHaveURL(/\/$/)
  await page.getByRole('button', { name: '切換學生視角', exact: true }).click(); await expect(page.getByText('學生體驗模式：', { exact: false })).toBeVisible()
  await expect(page.getByRole('button', { name: '建立課程', exact: true })).toHaveCount(0)
  await page.goto(`/assignments/${assignment.id}`); await expect(page.getByRole('textbox', { name: '題目 1 作答', exact: true })).toHaveValue('教授自己的練習'); await expect(page.getByRole('button', { name: '提交給教師／助教', exact: true })).toBeDisabled(); await expect(page.getByRole('button', { name: '查看作答', exact: true })).toHaveCount(0)
  await page.reload(); await expect(page.getByRole('button', { name: '返回教學視角', exact: true })).toBeVisible()
  await page.goto('/ai'); await expect(page.getByText('系統詞與實際請求（唯讀）', { exact: true })).toHaveCount(0)
  await page.getByRole('tab', { name: '一般對話' }).click(); await page.getByRole('textbox', { name: '傳送訊息', exact: true }).fill('practice-only-chat'); await page.getByRole('button', { name: '傳送訊息', exact: true }).click(); await expect(page.getByText('合成傳輸測試回覆', { exact: true })).toBeVisible()
  await page.screenshot({ path: 'test-results/student-experience.png', fullPage: true, animations: 'disabled' })
  await page.getByRole('button', { name: '切換深色模式', exact: true }).click(); await page.setViewportSize({ width: 390, height: 844 }); expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  await page.screenshot({ path: 'test-results/student-experience-mobile-dark.png', fullPage: true, animations: 'disabled' }); await page.setViewportSize({ width: 1440, height: 1050 })
  await page.getByRole('button', { name: '返回教學視角', exact: true }).click(); await page.goto(`/assignments/${assignment.id}`); await expect(page.getByRole('button', { name: '查看作答', exact: true })).toHaveCount(1)
  await page.goto('/ai'); await expect(page.getByText('系統詞與實際請求（唯讀）', { exact: true })).toBeVisible(); await page.getByRole('tab', { name: '一般對話' }).click(); await expect(page.getByText('practice-only-chat', { exact: true })).toHaveCount(0)
})
