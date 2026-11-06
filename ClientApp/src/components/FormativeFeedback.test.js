import { mount, flushPromises } from '@vue/test-utils'
import { describe, it, expect, vi, afterEach } from 'vitest'
import FormativeFeedback from './FormativeFeedback.vue'
import AssignmentView from '../views/AssignmentView.vue'
import { api } from '../api'
import { formativeSubmissionWarning } from '../utils/formative'
vi.mock('../api', () => ({ api: vi.fn() }))
vi.mock('../composables/auth', () => ({ auth: { user: { role: 'Student' } } }))
vi.mock('vue-router', () => ({ useRoute: () => ({ params: { assignmentId: 'a' } }), onBeforeRouteLeave: vi.fn(), onBeforeRouteUpdate: vi.fn() }))
const assessment = { evaluations: [{ version: 'current', score: 2, caseEvidenceAvailable: false, supportedClaims: [], partiallySupportedClaims: [], unsupportedClaims: [], unverifiedClaims: [], importantOmissions: [], reasoningQuality: '合成推理', justification: '合成理由' }], comparisonFeedback: '' }
const feedback = extra => ({ id: 'e', answerRevisionId: 'r1', revisionNo: 1, status: 'completed', isStale: false, createdAt: '2026-10-28T00:00:00Z', model: 'synthetic', profile: 'breeze', promptVersion: 'test', assessment, ...extra })
const props = { assignmentId: 'a', question: { id: 'q', stageCode: 'premorbid' }, answer: { currentRevisionId: 'r1', text: 'saved', revisions: [] }, editorText: 'saved', version: 1, eligible: true }
let wrapper
afterEach(() => { wrapper?.unmount(); vi.restoreAllMocks(); api.mockReset() })
describe('private formative feedback', () => {
  it('requires saving unsaved content and never evaluates different saved text silently', async () => {
    api.mockResolvedValue([]); wrapper = mount(FormativeFeedback, { props: { ...props, editorText: 'unsaved' } }); await flushPromises()
    expect(wrapper.get('button').attributes('disabled')).toBeDefined(); expect(wrapper.text()).toContain('請先按「儲存草稿」')
    expect(api).toHaveBeenCalledTimes(1)
  })
  it('labels feedback as private, nonofficial and fallible; shows revision binding and stale state', async () => {
    api.mockResolvedValue([feedback()]); wrapper = mount(FormativeFeedback, { props }); await flushPromises()
    expect(wrapper.text()).toContain('不是正式成績'); expect(wrapper.text()).toContain('AI 可能出錯'); expect(wrapper.text()).toContain('只供你本人閱讀')
    expect(wrapper.text()).toContain('對應目前已儲存版本'); expect(wrapper.text()).toContain('版本識別：r1')
    await wrapper.setProps({ editorText: 'changed' }); expect(wrapper.text()).toContain('已過期'); expect(wrapper.find('.outdated-feedback').exists()).toBe(true)
  })
  it('marks feedback stale after a saved revision change', async () => {
    api.mockResolvedValue([feedback()]); wrapper = mount(FormativeFeedback, { props }); await flushPromises()
    await wrapper.setProps({ answer: { ...props.answer, currentRevisionId: 'r2' } }); await flushPromises(); expect(wrapper.text()).toContain('已過期')
  })
  it('sends only revision identity and handles loading and timeout without clearing saved draft', async () => {
    let reject
    api.mockResolvedValueOnce([]).mockImplementationOnce(() => new Promise((_, fail) => { reject = fail }))
    wrapper = mount(FormativeFeedback, { props }); await flushPromises(); await wrapper.get('button').trigger('click')
    expect(api).toHaveBeenLastCalledWith('/assignments/a/questions/q/ai-feedback', { method: 'POST', body: { expectedVersion: 1, answerRevisionId: 'r1' } })
    expect(wrapper.text()).toContain('你仍可修改、儲存或直接提交'); reject(new Error('timeout')); await flushPromises()
    expect(wrapper.text()).toContain('草稿仍保留'); expect(wrapper.props('editorText')).toBe('saved')
  })
  it('displays persisted failures and never renders staff inspection or raw response fields', async () => {
    api.mockResolvedValue([feedback({ status: 'failed', assessment: null, error: 'AI 暫時無法提供有效回饋', requestInspection: { rawResponse: 'STAFF_SECRET' }, thinking: 'PRIVATE_THOUGHT' })])
    wrapper = mount(FormativeFeedback, { props }); await flushPromises()
    expect(wrapper.text()).toContain('AI 暫時無法'); expect(wrapper.text()).not.toContain('STAFF_SECRET'); expect(wrapper.text()).not.toContain('PRIVATE_THOUGHT'); expect(wrapper.text()).not.toContain('系統詞與實際請求')
  })
  it('does not enable AI for unconfigured or ineligible questions', async () => {
    api.mockResolvedValue([]); wrapper = mount(FormativeFeedback, { props: { ...props, question: { id: 'q', stageCode: null }, eligible: false } }); await flushPromises()
    expect(wrapper.get('button').attributes('disabled')).toBeDefined(); expect(wrapper.text()).toContain('尚未設定分析階段')
  })
})
describe('student submission actions', () => {
  const question = { id: 'q', promptText: 'synthetic', sortOrder: 1, stageCode: 'premorbid' }
  const own = { id: 's', status: 'draft', version: 1, answers: [{ questionId: 'q', text: 'saved', currentRevisionId: 'r1', revisions: [] }] }
  it('allows private practice submission in staff student experience', async () => {
    api.mockImplementation(path => Promise.resolve(path === '/assignments/a' ? { id: 'a', courseId: 'c', courseRole: 'student', courseStatus: 'active', status: 'published', isPractice: true, title: 'synthetic', questions: [question] } : path.endsWith('/my-submission') ? own : []))
    wrapper = mount(AssignmentView, { global: { stubs: { RouterLink: { template: '<a><slot /></a>' }, TeachingPanel: true } } }); await flushPromises()
    const submit = wrapper.findAll('button').find(b => b.text() === '提交給教師／助教')
    expect(submit.exists()).toBe(true); expect(submit.attributes('disabled')).toBeUndefined(); expect(wrapper.text()).toContain('提交自己的練習作答')
    expect(wrapper.findAll('button').some(b => b.text() === '請 AI 提供回饋')).toBe(false)
  })
  it('offers save and submit without assignment AI actions or AI confirmation', async () => {
    api.mockImplementation(path => Promise.resolve(path === '/assignments/a' ? { id: 'a', courseId: 'c', courseRole: 'student', courseStatus: 'active', status: 'published', title: 'synthetic', questions: [question] } : path.endsWith('/my-submission') ? own : []))
    wrapper = mount(AssignmentView, { global: { stubs: { RouterLink: { template: '<a><slot /></a>' }, TeachingPanel: true } } }); await flushPromises()
    const buttons = wrapper.findAll('button'); expect(buttons.some(b => b.text() === '儲存草稿')).toBe(true); expect(buttons.some(b => b.text() === '請 AI 提供回饋')).toBe(false)
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    await buttons.find(b => b.text() === '提交給教師／助教').trigger('click')
    expect(confirm).toHaveBeenCalledWith(expect.stringContaining('提交後不可修改'))
    expect(api.mock.calls.filter(([, options]) => options?.method === 'POST')).toHaveLength(0)
  })
  it('warns for stale feedback while never requiring a successful AI grade', () => {
    expect(formativeSubmissionWarning([question], own, { q: 'changed' }, { q: feedback() })).toContain('較早版本')
    expect(formativeSubmissionWarning([question], own, { q: 'saved' }, { q: feedback({ status: 'failed' }) })).toContain('尚未取得')
    expect(formativeSubmissionWarning([question], own, { q: 'saved' }, { q: feedback() })).toBe('')
  })
})
