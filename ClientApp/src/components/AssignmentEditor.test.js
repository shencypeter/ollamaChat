import { describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import AssignmentEditor from './AssignmentEditor.vue'

vi.mock('../api', () => ({ api: vi.fn() }))

const initial = {
  courseId: 'course-1',
  title: '概念檢核',
  instructions: '',
  maxScore: 100,
  questions: [
    { promptText: '第一題', questionType: 'essay' },
    { promptText: '第二題', questionType: 'essay' }
  ]
}

function render() {
  return mount(AssignmentEditor, {
    props: { initial, courseId: 'course-1', quiz: true },
    global: {
      stubs: {
        QuizScoringPlan: true,
        LearningRequirementsEditor: true
      }
    }
  })
}

describe('AssignmentEditor question ordering', () => {
  it('uses accessible icon buttons and preserves move ordering', async () => {
    const view = render()
    const drag = view.get('button[aria-label="拖曳題目排序"]')
    const firstUp = view.get('button[aria-label="題目 1 上移"]')
    const firstDown = view.get('button[aria-label="題目 1 下移"]')
    const secondUp = view.get('button[aria-label="題目 2 上移"]')
    const secondDown = view.get('button[aria-label="題目 2 下移"]')

    for (const control of [drag, firstUp, firstDown]) {
      expect(control.classes()).toContain('icon-button')
      expect(control.find('svg').exists()).toBe(true)
    }
    expect(drag.text()).toBe('')
    expect(firstUp.text()).toBe('')
    expect(firstDown.text()).toBe('')
    expect(firstUp.attributes('disabled')).toBeDefined()
    expect(secondDown.attributes('disabled')).toBeDefined()

    await secondUp.trigger('click')
    await view.get('form').trigger('submit')

    expect(view.emitted('save')[0][0].questions.map(question => question.promptText)).toEqual(['第二題', '第一題'])
  })
})
