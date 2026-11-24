import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import { nextTick } from 'vue'
import AnswerRevisionDiff from './AnswerRevisionDiff.vue'

afterEach(() => { document.body.innerHTML = '' })
const revisions = [
  { id: '1', label: '第 1 次提問', content: '觀察甲\n  🙂' },
  { id: '2', label: '第 2 次提問', content: '不相鄰中間版本' },
  { id: '3', label: '第 3 次提問', content: '觀察乙\n  🙂' },
  { id: '4', label: '第 4 次提問', content: '最後版本' },
]
const open = async wrapper => { await wrapper.get('button').trigger('click'); await nextTick() }
describe('answer revision comparison', () => {
  it('disables comparison until two revisions exist', () => {
    const wrapper = mount(AnswerRevisionDiff, { props: { revisions: revisions.slice(0, 1) } })
    expect(wrapper.get('button').attributes('disabled')).toBeDefined()
    expect(wrapper.get('button').text()).toBe('比較作答版本')
    wrapper.unmount()
  })
  it('allows any two nonadjacent revisions and reversing direction', async () => {
    const wrapper = mount(AnswerRevisionDiff, { attachTo: document.body, props: { revisions } })
    await open(wrapper)
    const [left, right] = wrapper.findAll('select')
    expect(left.attributes('aria-label')).toBe('基準版本')
    expect(right.attributes('aria-label')).toBe('比較版本')
    await left.setValue('1'); await right.setValue('3')
    expect(wrapper.get('[data-diff-change="remove"]').text()).toBe('甲')
    expect(wrapper.get('[data-diff-change="add"]').text()).toBe('乙')
    expect(wrapper.get('[data-diff-side="left"]').element.textContent).toBe(revisions[0].content)
    expect(wrapper.get('[data-diff-side="right"]').element.textContent).toBe(revisions[2].content)
    expect(wrapper.findAll('del,ins')).toHaveLength(0)
    await left.setValue('3'); await right.setValue('1')
    expect(wrapper.get('[data-diff-change="remove"]').text()).toBe('乙')
    expect(wrapper.get('[data-diff-change="add"]').text()).toBe('甲')
    expect(wrapper.get('[data-diff-side="left"]').element.textContent).toBe(revisions[2].content)
    expect(wrapper.get('[data-diff-side="right"]').element.textContent).toBe(revisions[0].content)
    wrapper.unmount()
  })
  it('shows unchanged, HTML safely, missing whitespace and bounded fallback', async () => {
    const wrapper = mount(AnswerRevisionDiff, { props: { revisions: [
      { id: 'a', label: '<img src=x>', content: '同樣內容\n ' },
      { id: 'b', label: '版本二', content: '同樣內容\n ' },
    ] } })
    await open(wrapper)
    expect(wrapper.text()).toContain('兩個版本內容相同')
    expect(wrapper.find('img').exists()).toBe(false)
    await wrapper.setProps({ revisions: [{ id: 'a', label: '甲', content: '<img src=x>\n  ' }, { id: 'b', label: '乙', content: '<script>x</script>\n ' }] })
    expect(wrapper.findAll('img, script')).toHaveLength(0)
    await wrapper.setProps({ revisions: [{ id: 'a', label: '甲', content: '甲'.repeat(8000) }, { id: 'b', label: '乙', content: '乙'.repeat(8000) }] })
    expect(wrapper.text()).toContain('差異範圍較大')
    expect(wrapper.get('[data-diff-side="left"]').element.textContent).toBe('甲'.repeat(8000))
    expect(wrapper.get('[data-diff-side="right"]').element.textContent).toBe('乙'.repeat(8000))
    wrapper.unmount()
  })
  it('supports Escape, focus return and backdrop close without closing on panel clicks', async () => {
    const wrapper = mount(AnswerRevisionDiff, { attachTo: document.body, props: { revisions } })
    wrapper.get('button').element.focus()
    await open(wrapper)
    expect(wrapper.get('dialog').element.open).toBe(true)
    const comparison=wrapper.get('[data-diff-scroll]');expect(comparison.attributes('tabindex')).toBe('0')
    comparison.element.focus()
    await comparison.trigger('keydown',{key:'Tab'});expect(document.activeElement).toBe(wrapper.get('[aria-label="關閉作答版本比較"]').element)
    await wrapper.get('[aria-label="關閉作答版本比較"]').trigger('keydown',{key:'Tab',shiftKey:true});expect(document.activeElement).toBe(comparison.element)
    await wrapper.get('.answer-diff-panel').trigger('click')
    expect(wrapper.get('dialog').element.open).toBe(true)
    await wrapper.get('dialog').trigger('keydown', { key: 'Escape' })
    expect(wrapper.get('dialog').element.open).toBe(false)
    expect(document.activeElement).toBe(wrapper.get('button').element)
    await open(wrapper)
    await wrapper.get('dialog').trigger('click')
    expect(wrapper.get('dialog').element.open).toBe(false)
    wrapper.unmount()
  })
})
