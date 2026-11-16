import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import ChatPerformanceDialog from './ChatPerformanceDialog.vue'

afterEach(() => { document.body.innerHTML = ''; vi.restoreAllMocks() })
const row = (wrapper, label) => wrapper.findAll('dl > div').find(r => r.find('dt').text() === label)?.find('dd').text()
describe('chat performance dialog', () => {
  it('uses actual values, preserves zero and converts duration units', async () => {
    const wrapper = mount(ChatPerformanceDialog, { attachTo: document.body, props: { open: true, modelName: 'test-model', promptVersion: 'v1', metrics: {
      promptTokens: 120, cachedPromptTokens: 0, responseTokens: 32, tokensPerSecond: 8,
      roundTripMilliseconds: 2350, totalDurationNanoseconds: 2000000000,
      loadDurationNanoseconds: 50000000, promptEvalDurationNanoseconds: 200000000, evalDurationNanoseconds: 1750000000,
      doneReason: '<img src=x>', systemPrompt: 'PRIVATE SYSTEM PROMPT',
    } } })
    await nextTick()
    expect(row(wrapper, '輸入 Token')).toBe('120')
    expect(row(wrapper, '快取輸入 Token')).toBe('0')
    expect(row(wrapper, '輸出 Token')).toBe('32')
    expect(row(wrapper, '生成速度')).toBe('8.00 tok/s')
    expect(row(wrapper, '往返時間')).toBe('2.35 秒')
    expect(row(wrapper, '模型處理時間')).toBe('2.00 秒')
    expect(row(wrapper, '模型載入時間')).toBe('0.05 秒')
    expect(row(wrapper, '輸入處理時間')).toBe('0.20 秒')
    expect(row(wrapper, '生成時間')).toBe('1.75 秒')
    expect(wrapper.text()).toContain('<img src=x>')
    expect(wrapper.findAll('img')).toHaveLength(0)
    expect(wrapper.text()).not.toContain('PRIVATE SYSTEM PROMPT')
    expect(wrapper.get('dialog').attributes('aria-labelledby')).toBeTruthy()
    wrapper.unmount()
  })
  it('explicitly marks historical missing metrics without fabricating zero', async () => {
    const wrapper = mount(ChatPerformanceDialog, { props: { open: true, metrics: null } })
    await nextTick()
    expect(wrapper.text()).toContain('此回覆未提供效能資料')
    expect(wrapper.text()).not.toContain('0.00')
    await wrapper.setProps({ metrics: { promptTokens: null, responseTokens: 0, tokensPerSecond: Infinity } })
    expect(row(wrapper, '輸入 Token')).toBe('未提供')
    expect(row(wrapper, '輸出 Token')).toBe('0')
    expect(row(wrapper, '生成速度')).toBe('未提供')
    wrapper.unmount()
  })
  it('respects measured fields while preserving measured zero and legacy metrics', async () => {
    const wrapper = mount(ChatPerformanceDialog, { props: { open: true, metrics: {
      availableFields: ['promptTokens', 'responseTokens', 'roundTripMilliseconds'],
      promptTokens: 0, cachedPromptTokens: 0, responseTokens: 0, roundTripMilliseconds: 0,
      tokensPerSecond: 0, totalDurationNanoseconds: 0, loadDurationNanoseconds: 0,
      promptEvalDurationNanoseconds: 0, evalDurationNanoseconds: 0,
    } } })
    await nextTick()
    expect(row(wrapper, '輸入 Token')).toBe('0')
    expect(row(wrapper, '輸出 Token')).toBe('0')
    expect(row(wrapper, '往返時間')).toBe('0.00 秒')
    for (const label of ['快取輸入 Token', '生成速度', '模型處理時間', '模型載入時間', '輸入處理時間', '生成時間']) {
      expect(row(wrapper, label)).toBe('未提供')
    }
    await wrapper.setProps({ metrics: { availableFields: [], promptTokens: 0 } })
    expect(row(wrapper, '輸入 Token')).toBe('未提供')
    await wrapper.setProps({ metrics: { availableFields: null, promptTokens: 0 } })
    expect(row(wrapper, '輸入 Token')).toBe('0')
    wrapper.unmount()
  })
  it('closes with Escape, backdrop or close button and returns focus', async () => {
    const trigger = document.createElement('button'); document.body.append(trigger); trigger.focus()
    const wrapper = mount(ChatPerformanceDialog, { attachTo: document.body, props: { open: false } })
    await wrapper.setProps({ open: true }); await nextTick()
    expect(wrapper.get('dialog').element.open).toBe(true)
    await wrapper.get('dialog').trigger('keydown', { key: 'Escape' })
    expect(wrapper.emitted('close')).toHaveLength(1)
    expect(document.activeElement).toBe(trigger)
    await wrapper.setProps({ open: false }); await wrapper.setProps({ open: true }); await nextTick()
    await wrapper.get('dialog').trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(2)
    await wrapper.setProps({ open: false }); await wrapper.setProps({ open: true }); await nextTick()
    await wrapper.get('button[aria-label="關閉效能資訊"]').trigger('click')
    expect(wrapper.emitted('close')).toHaveLength(3)
    wrapper.unmount()
  })
  it('uses the native modal API when available and handles native cancel', async () => {
    const show = vi.fn(function () { this.open = true })
    // jsdom has <dialog> but intentionally does not implement its modal methods.
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: show })
    const wrapper = mount(ChatPerformanceDialog, { attachTo: document.body, props: { open: true } })
    await nextTick()
    expect(show).toHaveBeenCalledOnce()
    await wrapper.get('dialog').trigger('cancel')
    expect(wrapper.emitted('close')).toHaveLength(1)
    wrapper.unmount()
    delete HTMLDialogElement.prototype.showModal
  })
})
