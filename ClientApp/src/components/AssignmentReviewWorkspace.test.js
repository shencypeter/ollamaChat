import { mount } from '@vue/test-utils'
import { expect, it } from 'vitest'
import AssignmentReviewWorkspace from './AssignmentReviewWorkspace.vue'

const assignment = {
  id: 'assignment-1',
  title: '單元觀察作業',
  maxScore: 100,
  questions: [
    { id: 'q1', sortOrder: 1, sectionTitle: '觀察', promptText: '請寫出觀察證據。', questionType: 'essay' }
  ]
}

const roster = [
  { studentId: 'missing', studentNumber: 'D003', studentName: '未交同學', id: null, submittedAt: null },
  { studentId: 'submitted-b', studentNumber: 'D002', studentName: '已交乙', id: 'submission-b', submittedAt: '2026-11-12T03:00:00Z', total: null },
  { studentId: 'submitted-a', studentNumber: 'D001', studentName: '已交甲', id: 'submission-a', submittedAt: '2026-11-12T02:00:00Z', total: 88 }
]

function render() {
  return mount(AssignmentReviewWorkspace, {
    props: { assignment, roster },
    global: { stubs: { TeachingPanel: { props: ['submissionId'], template: '<div class="teaching-stub">{{submissionId}}</div>' } } }
  })
}

it('lists submitted students before unsubmitted students and opens a real submission', async () => {
  const view = render()
  expect(view.findAll('.review-student').map(node => node.text())).toEqual([
    expect.stringContaining('已交甲'),
    expect.stringContaining('已交乙'),
    expect.stringContaining('未交同學')
  ])
  expect(view.get('.teaching-stub').text()).toBe('submission-a')
  await view.findAll('.review-student')[1].trigger('click')
  expect(view.get('.teaching-stub').text()).toBe('submission-b')
})

it('renders an unsubmitted student as a read-only blank paper without grading controls', async () => {
  const view = render()
  await view.findAll('.review-student')[2].trigger('click')
  expect(view.text()).toContain('未交卷 · 唯讀空白卷')
  expect(view.text()).toContain('請寫出觀察證據。')
  expect(view.text()).toContain('（未作答）')
  expect(view.find('.teaching-stub').exists()).toBe(false)
  expect(view.find('input[type="number"]').exists()).toBe(false)
})
