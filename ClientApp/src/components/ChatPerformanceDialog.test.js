import { mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import ChatPerformanceDialog from './ChatPerformanceDialog.vue'
import {speedScaleMaximum} from '../utils/performanceSpeedScale'

it('shows actual generate prompt safely and discloses combined repair calls',async()=>{
  const prompt='<|im_start|>user\n<script>literal text</script><|im_end|>\n<|im_start|>assistant\n',payload={model:'qwen-test',prompt,raw:true,stream:false,options:{num_ctx:16384}}
  const wrapper=mount(ChatPerformanceDialog,{attachTo:document.body,props:{open:true,canInspectRequests:true,initialTab:'request',metrics:{providerCalls:2},requestInspection:{requests:[{url:'http://model.test/api/generate',payload}]}}});await nextTick();await nextTick()
  expect(wrapper.get('[data-generated-prompt]').text()).toBe(prompt.trim());expect(wrapper.find('script').exists()).toBe(false);expect(JSON.parse(wrapper.get('[data-request-json]').text())).toEqual(payload)
  await wrapper.get('[data-tab="statistics"]').trigger('click');expect(wrapper.get('[data-provider-calls]').text()).toContain('模型呼叫 2 次');wrapper.unmount()
})

beforeEach(() => {speedScaleMaximum.value=200})
afterEach(() => { document.body.innerHTML = ''; vi.restoreAllMocks() })
const row = (wrapper, label) => wrapper.findAll('dl > div').find(r => r.find('dt').text() === label)?.find('dd').text()
describe('chat performance dialog', () => {
  it.each([[0,200],[128.41,200],[160,200],[180,250],[200,250],[210,300],[250,350],[320,400]])('shows measured speed %s with a rounded maximum %s and headroom',async(speed,maximum)=>{
    const wrapper=mount(ChatPerformanceDialog,{props:{open:true,metrics:{tokensPerSecond:speed}}});await nextTick()
    const meter=wrapper.get('[role="meter"]')
    expect(meter.attributes('aria-valuemin')).toBe('0');expect(meter.attributes('aria-valuemax')).toBe(String(maximum));expect(meter.attributes('aria-valuenow')).toBe(String(speed))
    expect(meter.attributes('aria-valuetext')).toContain(`${speed.toFixed(2)} tok/s`);expect(parseFloat(meter.get('[data-speed-fill]').element.style.width)).toBeCloseTo(speed/maximum*100,3)
    expect(row(wrapper,'生成速度')).toBe(`${speed.toFixed(2)} tok/s`)
    expect(wrapper.get('[data-speed-scale]').text()).toContain(`${maximum} tok/s`);expect(wrapper.find('[data-speed-overflow]').exists()).toBe(false)
    wrapper.unmount()
  })
  it('keeps the expanded comparison scale across slower replies and dialog instances',async()=>{
    const first=mount(ChatPerformanceDialog,{props:{open:true,metrics:{tokensPerSecond:320}}});await nextTick()
    expect(first.get('[role="meter"]').attributes('aria-valuemax')).toBe('400')
    await first.setProps({open:false});await first.setProps({metrics:{tokensPerSecond:128.41},open:true})
    expect(first.get('[role="meter"]').attributes('aria-valuemax')).toBe('400')
    const second=mount(ChatPerformanceDialog,{props:{open:true,metrics:{tokensPerSecond:20}}});await nextTick()
    expect(second.get('[role="meter"]').attributes('aria-valuemax')).toBe('400');first.unmount();second.unmount()
  })
  it.each([null,{}, {tokensPerSecond:Infinity},{tokensPerSecond:-1},{tokensPerSecond:0,availableFields:[]}])('does not show an unmeasured speed as a zero-filled gauge (%j)',async metrics=>{
    const wrapper=mount(ChatPerformanceDialog,{props:{open:true,metrics}});await nextTick();expect(wrapper.find('[role="meter"]').exists()).toBe(false);wrapper.unmount()
  })
  it('opens directly on the request tab when requested by course staff',async()=>{
    const wrapper=mount(ChatPerformanceDialog,{attachTo:document.body,props:{open:true,canInspectRequests:true,initialTab:'request'}});await nextTick();await nextTick()
    expect(wrapper.get('[data-tab="request"]').attributes('aria-selected')).toBe('true')
    await wrapper.setProps({canInspectRequests:false});expect(wrapper.get('dialog').exists()).toBe(true)
    expect(wrapper.find('[data-tab="request"]').exists()).toBe(false);wrapper.unmount()
  })
  it('shows only authorized captured requests as escaped readable messages and complete copyable JSON', async () => {
    const inspection={promptVersion:'actual-v3',requests:[{purpose:'case_feedback',url:'http://model.test/api/chat',payload:{model:'actual-model',messages:[{role:'system',content:'目前階段 B\n<script>alert(1)</script>'},{role:'user',content:'原文與歷史'}],format:{type:'object'},options:{temperature:0.2},stream:false}}]}
    const copy=vi.fn().mockResolvedValue();Object.defineProperty(navigator,'clipboard',{configurable:true,value:{writeText:copy}})
    const wrapper=mount(ChatPerformanceDialog,{attachTo:document.body,props:{open:true,canInspectRequests:true,requestInspection:inspection}});await nextTick()
    await wrapper.get('[role="tab"][data-tab="request"]').trigger('click')
    expect(wrapper.text()).toContain('目前階段 B');expect(wrapper.text()).toContain('actual-model');expect(wrapper.find('script').exists()).toBe(false)
    expect(JSON.parse(wrapper.get('[data-request-json]').text())).toEqual(inspection.requests[0].payload)
    await wrapper.get('[data-action="copy-request"]').trigger('click');expect(copy).toHaveBeenCalledWith(JSON.stringify(inspection.requests[0].payload,null,2))
    await wrapper.setProps({canInspectRequests:false});expect(wrapper.find('[data-tab="request"]').exists()).toBe(false);expect(wrapper.text()).not.toContain('目前階段 B')
    wrapper.unmount()
  })
  it('labels multiple outgoing bodies as a payload array without copying capture metadata',async()=>{
    const payloads=[{model:'one',messages:[]},{model:'two',messages:[]}]
    const wrapper=mount(ChatPerformanceDialog,{props:{open:true,canInspectRequests:true,requestInspection:{promptVersion:'v3',requests:payloads.map(payload=>({url:'http://model.test/api/chat',purpose:'feedback',payload}))}}});await nextTick();await wrapper.get('[data-tab="request"]').trigger('click')
    expect(wrapper.text()).toContain('各請求 payload JSON 陣列');expect(JSON.parse(wrapper.get('[data-request-json]').text())).toEqual(payloads);expect(wrapper.get('[data-action="copy-request"]').text()).toBe('複製 payload 陣列')
    wrapper.unmount()
  })
  it('explains missing historical capture and cycles keyboard focus through request controls',async()=>{
    const wrapper=mount(ChatPerformanceDialog,{attachTo:document.body,props:{open:true,canInspectRequests:true}});await nextTick()
    await wrapper.get('[data-tab="request"]').trigger('click');expect(wrapper.text()).toContain('未擷取此回覆的 Ollama 請求');expect(wrapper.text()).toContain('重新整理')
    const request=wrapper.get('[data-tab="request"]');request.element.focus();await request.trigger('keydown',{key:'Tab'});expect(document.activeElement).toBe(wrapper.get('[aria-label="關閉效能資訊"]').element)
    await wrapper.get('[aria-label="關閉效能資訊"]').trigger('keydown',{key:'Tab',shiftKey:true});expect(document.activeElement).toBe(request.element)
    wrapper.unmount()
  })
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
