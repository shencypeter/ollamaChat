import { test, expect } from '@playwright/test'
import { readFileSync } from 'node:fs'
test.skip(process.env.OLLAMA_INSPECTION_STUB !== '1', 'Requires an isolated API pointing to the synthetic Ollama fixture; never run against a real model.')
test.use({ actionTimeout: 15000 })
const state = JSON.parse(readFileSync(new URL('../../../.codex-build/portal-smoke.json', import.meta.url), 'utf8'))
async function post(request, path, data) { const token = (await (await request.get('/api/auth/csrf')).json()).token; return request.post(path, { headers: { 'X-CSRF-TOKEN': token }, ...(data ? { data } : {}) }) }
test('staff sees own exact wire requests including failures; students never receive them', async ({ request, page }) => {
  for (const role of ['Professor', 'TeachingAssistant', 'Student']) {
    const user = state.accounts.find(a => a.role === role)
    await post(request, '/api/auth/logout')
    expect((await post(request, '/api/auth/login', { email: user.email, password: state.password })).status()).toBe(200)
    const response = await post(request, '/api/ai/send', { stage: 'premorbid', message: `合成 ${role} 作答`, caseFacts: '合成案例', compare: false })
    expect(response.status()).toBe(200); const body = await response.json()
    if (role === 'Student') expect(body).not.toHaveProperty('requestInspection')
    else { expect(body.requestInspection.requests).toHaveLength(1); const wire = body.requestInspection.requests[0]; expect(wire.payload.model).toBe('inspection-test'); expect(wire.payload.messages[0].role).toBe('system'); expect(wire.payload.messages.at(-1).content).toContain(`合成 ${role} 作答`); expect(wire.payload.format).toBeTruthy(); expect(wire.responseStatusCode).toBe(200); expect(wire.rawResponse.message.content).toBeTruthy(); expect(JSON.stringify(wire.rawResponse)).not.toContain('thinking') }
    const failed = await post(request, '/api/ai/send', { stage: 'premorbid', message: 'INSPECTION_INVALID', compare: false })
    expect(failed.status()).toBe(502); const failure = await failed.json()
    if (role === 'Student') expect(failure).not.toHaveProperty('requestInspection')
    else { expect(failure.requestInspection.requests[0].payload.messages.at(-1).content).toContain('INSPECTION_INVALID'); expect(failure.requestInspection.requests[0].rawResponse.message.content).toBe('invalid assessment JSON'); expect(JSON.stringify(failure.requestInspection.requests[0].rawResponse)).not.toContain('thinking') }
    const session = await (await request.get('/api/ai/configuration')).json(); expect(session).not.toHaveProperty('requestInspection')
  }
  async function signIn(role) { const user = state.accounts.find(a => a.role === role); await page.goto('/login'); await page.getByLabel('電子郵件', { exact: true }).fill(user.email); await page.getByLabel('密碼', { exact: true }).fill(state.password); await page.getByRole('button', { name: '登入工作區', exact: true }).click(); await expect(page).toHaveURL(/\/$/); await page.goto('/ai') }
  await signIn('Professor'); await page.getByText('系統詞與實際請求（唯讀）', { exact: true }).click()
  await expect(page.getByText('送出評閱、比較或對話後', { exact: false })).toBeVisible()
  await page.getByRole('textbox', { name: '學生作答' }).fill('INSPECTION_INVALID'); await page.getByRole('button', { name: '開始評閱', exact: true }).click()
  await expect(page.getByRole('alert')).toContainText('不符合證據與評分格式')
  await expect(page.locator('.ollama-inspection')).toContainText('你是中山大學臨床心理學課程的 AI 教學助教')
  await page.getByText('user 訊息 1', { exact: true }).click(); await expect(page.locator('.ollama-inspection')).toContainText('INSPECTION_INVALID')
  await expect(page.locator('.ollama-inspection')).toContainText('模型原始回應（已移除 thinking）'); await expect(page.locator('.ollama-inspection')).toContainText('invalid assessment JSON')
  await expect(page.locator('.ollama-inspection input, .ollama-inspection textarea')).toHaveCount(0)
  await page.screenshot({ path: 'test-results/ollama-request-inspection.png', fullPage: true, animations: 'disabled' })
  await page.reload(); await page.getByText('系統詞與實際請求（唯讀）', { exact: true }).click(); await expect(page.getByText('送出評閱、比較或對話後', { exact: false })).toBeVisible()
  await page.getByRole('button', { name: '登出帳號', exact: true }).click(); await expect(page).toHaveURL(/\/login$/); await signIn('Student')
  await expect(page.getByText('系統詞與實際請求（唯讀）', { exact: true })).toHaveCount(0)
})
