import { test, expect } from '@playwright/test'
import { readFileSync, writeFileSync } from 'node:fs'
const statePath = new URL('../../../.codex-build/portal-smoke.json', import.meta.url)
const state = JSON.parse(readFileSync(statePath, 'utf8'))
const professor = state.accounts.find(a => a.role === 'Professor'), student = state.accounts.find(a => a.role === 'Student'), ta = state.accounts.find(a => a.role === 'TeachingAssistant')
async function csrf(request) { return (await (await request.get('/api/auth/csrf')).json()).token }
async function post(request, path, data, method = 'POST') { return request.fetch(path, { method, headers: { 'X-CSRF-TOKEN': await csrf(request) }, ...(data ? { data } : {}) }) }
async function login(request, user) { const session = await (await request.get('/api/auth/session')).json(); if (session.user) await post(request, '/api/auth/logout'); expect((await post(request, '/api/auth/login', { email: user.email, password: state.password })).status()).toBe(200) }
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
async function extraStudent(request) {
  const result = await post(request, '/api/accounts', { displayName: 'P0 未加入課程學生', email: `qa-${state.run}-outsider-${Date.now()}@example.invalid`, password: state.password, role: 'Student' }); expect(result.status()).toBe(200)
  const user = await result.json(), current = JSON.parse(readFileSync(statePath, 'utf8')); current.accounts.push(user); writeFileSync(statePath, JSON.stringify(current)); return user
}

test('P0 real API: drafts, revisions, submission immutability, deadlines and course access', async ({ request, playwright, baseURL }) => {
  await login(request, professor); const c = await course(request, 'api'), outsider = await extraStudent(request)
  let a = await create(request, c, draft('個案分析草稿'))
  const edit = { ...draft('個案分析正式題目'), expectedEditVersion: a.editVersion }
  const updated = await post(request, `/api/assignments/${a.id}`, edit, 'PUT'); expect(updated.status()).toBe(200); a = await updated.json()
  expect((await post(request, `/api/assignments/${a.id}`, edit, 'PUT')).status()).toBe(409)
  const s = await context(playwright, baseURL, student), t = await context(playwright, baseURL, ta), o = await context(playwright, baseURL, outsider)
  try {
    expect(await (await s.get(`/api/courses/${c.id}/assignments`)).json()).toEqual([])
    expect((await s.get(`/api/assignments/${a.id}`)).status()).toBe(404)
    expect((await post(s, `/api/courses/${c.id}/assignments`, draft('非法作業'))).status()).toBe(403)
    expect((await o.get(`/api/courses/${c.id}/assignments`)).status()).toBe(403)
    const taDraft = await create(t, c, draft('助教建立的作業'))
    expect((await post(t, `/api/assignments/${a.id}/publish`)).status()).toBe(200)
    expect((await post(request, `/api/assignments/${a.id}`, { ...edit, expectedEditVersion: a.editVersion }, 'PUT')).status()).toBe(409)
    expect((await o.get(`/api/assignments/${a.id}`)).status()).toBe(403)
    expect((await o.get(`/api/assignments/${a.id}/my-submission`)).status()).toBe(403)
    expect((await s.get(`/api/assignments/${a.id}/submissions`)).status()).toBe(403)
    expect((await post(s, `/api/assignments/${a.id}/my-submission`, payload(a, 0, ['', ''], true))).status()).toBe(400)
    expect((await (await s.get(`/api/assignments/${a.id}/my-submission`)).json()).status).toBe('not_started')
    const mixed = payload(a, 0, ['答案', '答案']); mixed.answers[0].questionId = taDraft.questions[0].id
    expect((await post(s, `/api/assignments/${a.id}/my-submission`, mixed)).status()).toBe(400)
    expect((await post(request, `/api/assignments/${a.id}/my-submission`, payload(a, 0, ['教師不能代交', '答案']))).status()).toBe(403)
    const saved = await post(s, `/api/assignments/${a.id}/my-submission`, payload(a, 0, ['第一版病前分析', ''])); expect(saved.status()).toBe(200); const first = await saved.json()
    expect(first.status).toBe('draft'); expect(first.version).toBeGreaterThan(0)
    expect(await (await t.get(`/api/assignments/${a.id}/submissions`)).json()).toEqual([])
    const newer = await post(s, `/api/assignments/${a.id}/my-submission`, payload(a, first.version, ['第二版病前分析', '疾病病程分析'])); expect(newer.status()).toBe(200); const second = await newer.json()
    expect(second.answers[0].revisions).toHaveLength(2)
    expect((await post(s, `/api/assignments/${a.id}/my-submission`, payload(a, first.version, ['過期頁面', '過期頁面']))).status()).toBe(409)
    expect((await (await s.get(`/api/assignments/${a.id}/my-submission`)).json()).answers[0].text).toBe('第二版病前分析')
    const final = await post(s, `/api/assignments/${a.id}/my-submission`, payload(a, second.version, ['第二版病前分析', '疾病病程分析'], true)); expect(final.status()).toBe(200); const submitted = await final.json()
    expect(submitted.status).toBe('submitted'); expect(submitted.submittedAt).toBeTruthy(); expect(submitted.answers.every(answer => answer.revisions.some(r => r.id === answer.currentRevisionId && r.isSubmitted))).toBe(true)
    expect((await post(s, `/api/assignments/${a.id}/my-submission`, payload(a, second.version, ['竄改', '竄改']))).status()).toBe(409)
    const roster = await (await t.get(`/api/assignments/${a.id}/submissions`)).json(); expect(roster).toHaveLength(1); expect(roster[0].studentUserId).toBe(student.id)
    const review = await (await request.get(`/api/assignments/${a.id}/submissions/${submitted.id}`)).json(); expect(review.answers[0].text).toBe('第二版病前分析'); expect(review.answers[0].revisions).toHaveLength(2)
    expect((await s.get(`/api/assignments/${a.id}/submissions/${submitted.id}`)).status()).toBe(403)
    expect((await t.get(`/api/assignments/${taDraft.id}/submissions/${submitted.id}`)).status()).toBe(404)
    expect((await post(t, `/api/assignments/${a.id}/close`)).status()).toBe(200)
    expect((await s.get(`/api/assignments/${a.id}/my-submission`)).status()).toBe(200)
    expect((await post(request, `/api/courses/${c.id}/members`, { userId: outsider.id, courseRole: 'student' })).status()).toBe(204)
    expect((await post(o, `/api/assignments/${a.id}/my-submission`, payload(a, 0, ['遲交', '遲交']))).status()).toBe(409)
    expect((await (await o.get(`/api/assignments/${a.id}/my-submission`)).json()).status).toBe('not_started')
    for (const [title, dates] of [['未開放', { opensAt: new Date(Date.now()+3600000).toISOString() }], ['已截止', { dueAt: new Date(Date.now()-3600000).toISOString() }]]) {
      const timed = await create(request, c, draft(title, dates)); expect((await post(request, `/api/assignments/${timed.id}/publish`)).status()).toBe(200)
      expect((await post(s, `/api/assignments/${timed.id}/my-submission`, payload(timed, 0, ['答案', '答案']))).status()).toBe(409)
    }
    const concurrent = await create(request, c, draft('並行儲存測試')); await post(request, `/api/assignments/${concurrent.id}/publish`)
    const race = await Promise.all([post(s, `/api/assignments/${concurrent.id}/my-submission`, payload(concurrent, 0, ['版本 A', '答案 A'])), post(s, `/api/assignments/${concurrent.id}/my-submission`, payload(concurrent, 0, ['版本 B', '答案 B']))])
    expect(race.map(r => r.status()).sort()).toEqual([200, 409])
  } finally { await Promise.all([s.dispose(), t.dispose(), o.dispose()]) }
})

test('P0 browser: staff publishes, student saves/reloads/submits, staff reads the submitted work', async ({ page, request }) => {
  await login(request, professor); const c = await course(request, 'browser')
  async function signIn(user) { await page.goto('/login'); await page.getByLabel('電子郵件', { exact: true }).fill(user.email); await page.getByLabel('密碼', { exact: true }).fill(state.password); await page.getByRole('button', { name: '登入工作區', exact: true }).click(); await expect(page).toHaveURL(/\/$/) }
  await signIn(professor); await page.goto(`/courses/${c.id}?section=assignments`); await page.getByRole('button', { name: '建立個人作業', exact: true }).click()
  await page.getByRole('textbox', { name: '作業名稱', exact: true }).fill('P0 Demo · 個人案例分析'); await page.getByRole('textbox', { name: '作業說明與案例資料' }).fill('請根據課堂案例提出有依據的分析。')
  await page.getByRole('textbox', { name: '題目 1 內容', exact: true }).fill('描述案例的重要事實與推論限制。'); await page.getByRole('button', { name: '儲存作業草稿', exact: true }).click()
  await page.getByRole('link', { name: /P0 Demo · 個人案例分析/ }).click(); page.once('dialog', dialog => dialog.accept()); await page.getByRole('button', { name: '發布作業', exact: true }).click(); await expect(page.getByText('作業已發布。', { exact: true })).toBeVisible()
  const url = page.url(); await page.getByRole('button', { name: '登出帳號', exact: true }).click(); await expect(page).toHaveURL(/\/login$/); await signIn(student); await page.goto(url)
  await page.getByRole('textbox', { name: '題目 1 作答' }).fill('初稿：目前資料不足以支持全面性的結論。'); await page.getByRole('button', { name: '儲存草稿', exact: true }).click(); await expect(page.getByText('草稿已儲存至資料庫。')).toBeVisible()
  await page.reload(); await expect(page.getByRole('textbox', { name: '題目 1 作答' })).toHaveValue('初稿：目前資料不足以支持全面性的結論。')
  await page.getByRole('textbox', { name: '題目 1 作答' }).fill('提交版：將案例的已知事實與推論分開，並保留資料不足的限制。'); page.once('dialog', dialog => dialog.accept()); await page.getByRole('button', { name: '提交給教師／助教', exact: true }).click()
  await expect(page.getByText('作答已成功提交，教師與助教現在可以查看。')).toBeVisible(); await expect(page.getByRole('textbox', { name: '題目 1 作答' })).toHaveCount(0)
  await page.screenshot({ path: 'test-results/p0-student-submitted-light.png', fullPage: true, animations: 'disabled' })
  await page.getByRole('button', { name: '登出帳號', exact: true }).click(); await expect(page).toHaveURL(/\/login$/); await signIn(professor); await page.goto(url); await page.getByRole('button', { name: '查看作答', exact: true }).click()
  await expect(page.locator('.review-answer-text').filter({ hasText: '提交版：將案例的已知事實與推論分開，並保留資料不足的限制。' })).toBeVisible(); await page.getByRole('button', { name: '切換深色模式' }).click()
  await page.screenshot({ path: 'test-results/p0-staff-review-dark.png', fullPage: true, animations: 'disabled' }); await page.setViewportSize({ width: 390, height: 844 }); expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  await page.screenshot({ path: 'test-results/p0-review-mobile.png', fullPage: true, animations: 'disabled' })
})
