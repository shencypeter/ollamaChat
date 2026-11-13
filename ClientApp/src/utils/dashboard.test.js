import { expect, it } from 'vitest'
import { loadDashboard } from './dashboard'

it('bounds concurrent HTTP requests even when each course fans out into activities', async () => {
  let active = 0, maximum = 0
  const courses = Array.from({ length: 8 }, (_, index) => ({ id: String(index), title: `課程 ${index}`, status: 'active', courseRole: 'teacher' }))
  const api = async path => {
    active++; maximum = Math.max(maximum, active)
    await new Promise(resolve => setTimeout(resolve, 1))
    active--
    if (path === '/courses') return courses
    if (path.includes('/questions')) return { items: [] }
    if (path.includes('/curriculum')) return { activities: [0, 1, 2].map(index => ({ id: `${path.split('/')[2]}-${index}`, kind: 'assignment', title: '作業', status: 'published' })) }
    return []
  }
  const data = await loadDashboard(api, true)
  expect(maximum).toBe(4)
  expect(data.errors).toEqual([])
})

it('retains successful teacher work when another course fails and excludes archived courses', async () => {
  const paths = []
  const data = await loadDashboard(async path => {
    paths.push(path)
    if (path === '/courses') return [{ id: 'ok', title: '正常課程', status: 'active', courseRole: 'teacher' }, { id: 'bad', title: '暫停服務課程', status: 'active', courseRole: 'teacher' }, { id: 'old', status: 'archived', courseRole: 'teacher' }]
    if (path.includes('/bad/')) throw new Error('服務中斷')
    if (path.includes('/questions')) return { items: [] }
    if (path.includes('/curriculum')) return { activities: [{ id: 'a', title: '文字作業', kind: 'assignment', status: 'published' }] }
    return [{ id: 's', studentName: '合成測試學員', gradingStatus: 'pending_essay', submittedAt: '2026-11-13T01:00:00Z' }]
  }, true)
  expect(data.pending[0]).toMatchObject({ title: '文字作業', count: 1, url: '/courses/ok/assignments/a' })
  expect(data.unavailable).toMatchObject({ grading: true, discussions: true })
  expect(data.errors).toHaveLength(2)
  expect(paths.some(path => path.includes('/old/'))).toBe(false)
})
