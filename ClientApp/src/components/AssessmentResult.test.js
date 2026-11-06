import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import AssessmentResult from './AssessmentResult.vue'
const evaluation={version:'current',caseEvidenceAvailable:false,supportedClaims:[],partiallySupportedClaims:[],unsupportedClaims:[{claim:'<script>alert(1)</script>',evidence:'<img src=x onerror=alert(1)>',explanation:'未提供案例證據'}],unverifiedClaims:[],importantOmissions:[],reasoningQuality:'需限制結論範圍',score:2,justification:'核心論點缺乏支持'}
describe('structured assessment feedback',()=>{
  it('renders findings as text, never executable model HTML',()=>{const wrapper=mount(AssessmentResult,{props:{evaluation}});expect(wrapper.text()).toContain('<script>alert(1)</script>');expect(wrapper.find('script').exists()).toBe(false);expect(wrapper.find('img').exists()).toBe(false);expect(wrapper.text()).toContain('核心論點缺乏支持')})
  it('marks absent case facts as provisional',()=>{const wrapper=mount(AssessmentResult,{props:{evaluation}});expect(wrapper.find('.provisional').exists()).toBe(true);expect(wrapper.find('.score strong').text()).toBe('2')})
  it('labels comparison versions independently',()=>{const wrapper=mount(AssessmentResult,{props:{evaluation:{...evaluation,version:'before',score:4,caseEvidenceAvailable:true}}});expect(wrapper.text()).toContain('修改前版本');expect(wrapper.find('.score strong').text()).toBe('4');expect(wrapper.find('.provisional').exists()).toBe(false)})
})
