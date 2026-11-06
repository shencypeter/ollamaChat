import { test, expect } from '@playwright/test'
import { readFileSync, writeFileSync } from 'node:fs'
test.use({ actionTimeout: 15000, trace: 'retain-on-failure' })
const statePath = new URL('../../../.codex-build/portal-smoke.json', import.meta.url)
const state = JSON.parse(readFileSync(statePath, 'utf8'))
const professor = state.accounts.find(a => a.role === 'Professor'), student = state.accounts.find(a => a.role === 'Student'), ta = state.accounts.find(a => a.role === 'TeachingAssistant')
async function csrf(request) { return (await (await request.get('/api/auth/csrf')).json()).token }
async function post(request, path, data, method = 'POST') { return request.fetch(path, { method, headers: { 'X-CSRF-TOKEN': await csrf(request) }, ...(data ? { data } : {}) }) }
async function login(request, user) { const session = await (await request.get('/api/auth/session')).json(); if (session.user) await post(request, '/api/auth/logout'); let result; for (let i = 0; i < 14; i++) { result = await post(request, '/api/auth/login', { email: user.email, password: state.password }); if (result.status() !== 429) break; await new Promise(resolve => setTimeout(resolve, 5000)) } expect(result.status()).toBe(200) }
async function context(playwright, baseURL, user) { const request = await playwright.request.newContext({ baseURL }); await login(request, user); return request }
async function course(request, suffix) {
  const response = await post(request, '/api/courses', { code: `P0-${state.run}-${suffix}-${Date.now()}`, title: `P0 個人作業 · ${suffix}`, termCode: '115-1' }); expect(response.status()).toBe(200)
  const course = await response.json()
  for (const [user, role] of [[student, 'student'], [ta, 'ta']]) expect((await post(request, `/api/courses/${course.id}/members`, { userId: user.id, courseRole: role })).status()).toBe(204)
  return course
}
function draft(title, extras = {}) { return { title, instructions: '請依案例資料作答，分清事實與推論。', questions: [{ promptText: '請整理病前功能。', stageCode: 'premorbid' }, { promptText: '請整理疾病病程。', stageCode: 'illnessCourse' }], ...extras } }
async function create(request, course, payload) { const result = await post(request, `/api/courses/${course.id}/assignments`, payload); expect(result.status()).toBe(200); return result.json() }
function payload(assignment, version, texts, submit = false) { return { expectedVersion: version, submit, answers: assignment.questions.map((q, i) => ({ questionId: q.id, text: texts[i] || '' })) } }

test('P1 real PostgreSQL: versioned submissions, immutable rubrics, draft/released grades and isolation', async ({ request, playwright, baseURL }) => {
  test.setTimeout(120000)
  await login(request, professor); const c = await course(request, 'p1-api'), other = await course(request, 'p1-other')
  const rubricBody = { title: '證據與推理', criteria: [{ title: '證據', description: '判斷支持範圍', weight: 1, levels: ['不足', '部分', '大致完整', '完整'] }, { title: '推理', description: '論證品質', weight: 3, levels: ['不足', '部分', '大致完整', '完整'] }] }
  let response = await post(request, `/api/courses/${c.id}/rubrics`, rubricBody); expect(response.status()).toBe(200); let rubric = await response.json()
  response = await post(request, `/api/courses/${c.id}/rubrics/${rubric.id}`, { ...rubricBody, expectedVersion: rubric.versionNo, title: '證據與推理修訂' }, 'PUT'); expect(response.status()).toBe(200); rubric = await response.json(); expect(rubric.versionNo).toBe(2)
  const a = await create(request, c, draft('P1 可修訂作業', { allowRevisions: true, maxScore: 100, rubricId: rubric.id }))
  response = await post(request, `/api/courses/${c.id}/rubrics/${rubric.id}`, { ...rubricBody, expectedVersion: rubric.versionNo, title: '新規準版本' }, 'PUT'); expect(response.status()).toBe(200); const fork = await response.json(); expect(fork.id).not.toBe(rubric.id)
  expect((await post(request, `/api/courses/${other.id}/assignments`, draft('跨課規準', { rubricId: rubric.id }))).status()).toBe(400)
  await post(request, `/api/assignments/${a.id}/publish`)
  const secondUserResponse = await post(request, '/api/accounts', { displayName: 'P1 隔離學生', email: `qa-${state.run}-p1-${Date.now()}@example.invalid`, password: state.password, role: 'Student' }); expect(secondUserResponse.status()).toBe(200)
  const secondUser = await secondUserResponse.json(), localState = JSON.parse(readFileSync(statePath, 'utf8')); localState.accounts.push(secondUser); writeFileSync(statePath, JSON.stringify(localState))
  await post(request, `/api/courses/${c.id}/members`, { userId: secondUser.id, courseRole: 'student' })
  const s = await context(playwright, baseURL, student), t = await context(playwright, baseURL, ta)
  const isolated = await context(playwright, baseURL, secondUser)
  try {
    expect((await post(s, `/api/courses/${c.id}/rubrics`, rubricBody)).status()).toBe(403)
    const first = await (await post(s, `/api/assignments/${a.id}/my-submission`, payload(a, 0, ['第一版', '病程一'], true))).json()
    const path = `/api/assignments/${a.id}/submissions/${first.id}/revisions`
    let revisions = await (await t.get(path)).json(); expect(revisions).toHaveLength(1); const original = revisions[0]
    expect(original.answers[0].text).toBe('第一版'); expect(original.authorUserId).toBe(student.id)
    const gradePath = `/api/assignments/${a.id}/revisions/${original.id}/grades`
    expect((await isolated.get(path)).status()).toBe(403)
    expect((await isolated.get(gradePath)).status()).toBe(403)
    expect((await isolated.get(`/api/courses/${other.id}/rubrics`)).status()).toBe(403)
    const grading = { score: 62.5, feedback: '教師回饋：補足核心證據。', criteria: rubric.criteria.map((v, i) => ({ criterionId: v.id, level: i === 0 ? 4 : 2 })) }
    expect((await post(s, gradePath, grading)).status()).toBe(403)
    expect((await post(t, gradePath, { ...grading, score: 99 })).status()).toBe(400)
    response = await post(t, gradePath, grading); expect(response.status()).toBe(200); const grade = await response.json()
    expect(await (await s.get(gradePath)).json()).toEqual([])
    expect((await post(t, gradePath, grading)).status()).toBe(409)
    expect((await post(s, `${gradePath}/${grade.id}/release`)).status()).toBe(403)
    expect((await post(t, `${gradePath}/${grade.id}/release`)).status()).toBe(200)
    expect((await (await s.get(gradePath)).json())[0].score).toBe(62.5)
    const race = await Promise.all([post(s, `/api/assignments/${a.id}/my-submission`, payload(a, first.version, ['修訂 A', '病程 A'], true)), post(s, `/api/assignments/${a.id}/my-submission`, payload(a, first.version, ['修訂 B', '病程 B'], true))])
    expect(race.map(r => r.status()).sort()).toEqual([200, 409])
    revisions = await (await s.get(path)).json(); expect(revisions).toHaveLength(2); expect(revisions.map(r => r.revisionNo)).toEqual([2, 1]); expect(revisions[1]).toEqual(original)
    expect(await (await s.get(`/api/assignments/${a.id}/revisions/${revisions[0].id}/grades`)).json()).toEqual([])
    expect((await post(s, `/api/assignments/${a.id}/my-submission`, payload(a, (await (await s.get(`/api/assignments/${a.id}/my-submission`)).json()).version, ['草稿不能覆寫', '草稿'], false))).status()).toBe(409)
    response = await post(t, gradePath, { ...grading, expectedGradeId: grade.id, score: 100, criteria: rubric.criteria.map(v => ({ criterionId: v.id, level: 4 })), feedback: '修正的評分草稿' }); expect(response.status()).toBe(200); const amended = await response.json(); expect(amended.supersedesGradeId).toBe(grade.id)
    expect((await (await s.get(gradePath)).json())[0].feedback).toBe(grading.feedback)
    expect((await post(t, `${gradePath}/${grade.id}/release`)).status()).toBe(409)
    await post(t, `${gradePath}/${amended.id}/release`); expect((await (await s.get(gradePath)).json()).map(g => g.score)).toEqual([100, 62.5])
    const otherA = await create(request, other, draft('另一份作業')); await post(request, `/api/assignments/${otherA.id}/publish`)
    expect((await t.get(`/api/assignments/${otherA.id}/revisions/${original.id}/grades`)).status()).toBe(404)
    // Remove the TA from the other course: platform role alone must not grant access.
    await post(request, `/api/courses/${other.id}/members`, { userId: ta.id, courseRole: 'student' })
    expect((await post(t, `/api/courses/${other.id}/rubrics`, rubricBody)).status()).toBe(403)
    const frozen = await (await t.get(`/api/assignments/${a.id}/rubric`)).json(); expect(frozen.id).toBe(rubric.id); expect(frozen.title).toBe('證據與推理修訂')
    await post(t, `/api/assignments/${a.id}/close`)
    expect((await post(s, `/api/assignments/${a.id}/my-submission`, payload(a, first.version, ['關閉後', '答案'], true))).status()).toBe(409)
  } finally { await s.dispose(); await t.dispose(); await isolated.dispose() }
})

test('P1 browser: create rubric, submit twice, compare, grade and release feedback', async ({ page, request }) => {
  test.setTimeout(120000)
  await login(request, professor); const c = await course(request, 'p1-browser')
  async function signIn(user) { await page.goto('/login'); await page.getByLabel('電子郵件', { exact: true }).fill(user.email); await page.getByLabel('密碼', { exact: true }).fill(state.password); await page.getByRole('button', { name: '登入工作區', exact: true }).click(); await expect(page).toHaveURL(/\/$/) }
  async function switchTo(user, url) { await page.getByRole('button', { name: '登出帳號', exact: true }).click(); await expect(page).toHaveURL(/\/login$/); await signIn(user); await page.goto(url) }
  await signIn(professor); await page.goto(`/courses/${c.id}?section=assignments`); await page.getByRole('button', { name: '建立規準', exact: true }).click()
  await page.getByLabel('規準名稱', { exact: true }).fill('P1 Demo 規準'); await page.getByLabel('規準項目 1', { exact: true }).fill('證據品質')
  for (let i = 1; i <= 4; i++) await page.getByLabel(`${i} 級描述`, { exact: true }).fill(`第 ${i} 級證據`)
  await page.getByRole('button', { name: '儲存規準', exact: true }).click(); await expect(page.getByText('P1 Demo 規準 · v1 · 1 項')).toBeVisible()
  await page.getByRole('button', { name: '建立個人作業', exact: true }).click(); await page.getByLabel('作業名稱', { exact: true }).fill('P1 Demo 修訂與回饋'); await page.getByLabel('題目 1 內容', { exact: true }).fill('請提出證據與限制。')
  await page.getByLabel('評分規準', { exact: true }).selectOption({ label: 'P1 Demo 規準 · v1' }); await page.getByLabel('允許學生在截止前提交修訂版本').check(); await page.getByRole('button', { name: '儲存作業草稿' }).click()
  await page.getByRole('link', { name: /P1 Demo 修訂與回饋/ }).click(); page.once('dialog', d => d.accept()); await page.getByRole('button', { name: '發布作業', exact: true }).click(); await expect(page.getByText('作業已發布。', { exact: true })).toBeVisible()
  const url = page.url(); await switchTo(student, url)
  for (const answer of ['初次提交的分析', '修訂後：有證據支持的部分與資料不足的限制。']) { await page.getByLabel('題目 1 作答', { exact: true }).fill(answer); page.once('dialog', d => d.accept()); await page.getByRole('button', { name: '提交給教師／助教', exact: true }).click(); await expect(page.getByText('作答已成功提交，教師與助教現在可以查看。', { exact: true })).toBeVisible(); await expect(page.getByLabel('檢視版本')).toContainText(answer.startsWith('初次') ? '第 1 次提交' : '第 2 次提交') }
  await switchTo(professor, url); await page.getByRole('button', { name: '查看作答', exact: true }).click()
  await expect(page.locator('.revision-comparison')).toContainText('初次提交的分析'); await expect(page.locator('.revision-comparison')).toContainText('修訂後：有證據支持的部分')
  await page.getByLabel('證據品質（權重 1）').selectOption('3'); await page.getByLabel('教師回饋', { exact: true }).fill('請進一步說明案例證據的範圍。'); await page.getByRole('button', { name: '儲存評分草稿', exact: true }).click(); await expect(page.getByText('人工評分草稿已儲存，尚未發布。')).toBeVisible()
  page.once('dialog', d => d.accept()); await page.getByRole('button', { name: '發布回饋', exact: true }).click(); await expect(page.getByText('回饋已發布。', { exact: true })).toBeVisible()
  await page.screenshot({ path: 'test-results/p1-teacher-review.png', fullPage: true, animations: 'disabled' })
  await switchTo(student, url); await expect(page.getByText('人工評分 75 / 100 · 已發布')).toBeVisible(); await expect(page.getByText('請進一步說明案例證據的範圍。', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: '切換深色模式' }).click(); await page.setViewportSize({ width: 390, height: 844 }); expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  await page.screenshot({ path: 'test-results/p1-student-feedback-dark-mobile.png', fullPage: true, animations: 'disabled' })
})
