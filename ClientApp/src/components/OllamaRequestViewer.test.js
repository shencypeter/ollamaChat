import { mount } from '@vue/test-utils'
import { describe, it, expect } from 'vitest'
import OllamaRequestViewer from './OllamaRequestViewer.vue'
describe('read-only Ollama request viewer', () => {
  it('explains how to obtain the actual request without offering prompt edits', () => {
    const wrapper = mount(OllamaRequestViewer)
    expect(wrapper.text()).toContain('送出評閱、比較或對話後')
    expect(wrapper.findAll('input, textarea, [contenteditable=true]')).toHaveLength(0)
  })
  it('shows assembled prompts, context and parameters safely as text', () => {
    const wrapper = mount(OllamaRequestViewer, { props: { inspection: { promptVersion: 'test-v1', requests: [{ purpose: 'assessment', url: 'http://localhost/api/chat', responseStatusCode: 200, rawResponse: { model: 'test-model', message: { role: 'assistant', content: '模型原始 JSON 內容' }, eval_count: 12 }, payload: { model: 'test-model', messages: [{ role: 'system', content: '<img src=x onerror=alert(1)>規準' }, { role: 'user', content: '合成案例及答案' }], options: { temperature: 0 }, format: { type: 'object' } } }] } } })
    expect(wrapper.text()).toContain('規準'); expect(wrapper.text()).toContain('合成案例及答案'); expect(wrapper.text()).toContain('temperature'); expect(wrapper.text()).toContain('模型原始 JSON 內容'); expect(wrapper.text()).toContain('HTTP 200')
    expect(wrapper.findAll('img, script, textarea')).toHaveLength(0)
  })
})
