import { mount, flushPromises } from '@vue/test-utils'
import { beforeEach, expect, it, vi } from 'vitest'
import OverviewView from './OverviewView.vue'
import { api } from '../api'
import { auth } from '../composables/auth'
import { experience } from '../composables/experience'

vi.mock('../api', () => ({ api: vi.fn() }))
vi.mock('../composables/auth', () => ({ auth: { user: { displayName: '測試同學', canTeach: false } }, roleLabels: { Member: '一般成員' } }))
vi.mock('../composables/experience', () => ({ experience: { student: false } }))
const global = { stubs: { PortalActivity: true, RouterLink: { props: ['to'], template: '<a :href="typeof to===\'string\'?to:to.path"><slot/></a>' } } }
const course = { id: 'c', title: '數學課', code: 'M', termCode: '115-1', status: 'active', courseRole: 'student' }
beforeEach(() => { api.mockReset(); auth.user.canTeach = false; experience.student = false })

it('shows actionable student tasks, course progress and published grades only', async () => {
  api.mockImplementation(async path => {
    if (path === '/courses') return [course]
    if (path === '/learning-history/courses') return [{ ...course, required: 3, submitted: 1, pending: 1 }]
    if (path.includes('todo=true')) return { total: 1, items: [{ id: 't', title: '比例練習', courseId: 'c', state: 'overdue', url: '/quizzes/t' }] }
    return { total: 2, items: [{ id: 'g', title: '已公布測驗', state: 'graded', score: 88, maxScore: 100, courseId: 'c', url: '/quizzes/g' }, { id: 'p', title: '尚未發布的草稿分數', state: 'pending', score: 90, url: '/quizzes/p' }] }
  })
  const view = mount(OverviewView, { global }); await flushPromises()
  expect(view.text()).toContain('學習待辦'); expect(view.text()).toContain('逾期未交')
  expect(view.get('a[href="/quizzes/t"]').text()).toContain('比例練習')
  expect(view.get('a[href="/quizzes/g"]').text()).toContain('已公布測驗')
  expect(view.text()).not.toContain('尚未發布的草稿分數')
  expect(view.get('a[href="/courses/c"]').text()).toContain('數學課')
  expect(view.find('.dashboard-course progress').exists()).toBe(false)
  expect(view.get('.dashboard-course').text()).not.toContain('已繳交')
  expect(view.get('.dashboard-course').text()).not.toContain('進行中')
  view.unmount()
})
it('scopes all student dashboard requests to the configured term and preserves historical selection',async()=>{
 api.mockImplementation(async path=>{
  if(path==='/academic-calendar')return {currentTerm:'115-2',terms:['115-2','114-1']}
  if(path==='/courses')return [{...course,id:'current',termCode:'115-2'},{...course,id:'old',title:'歷年數學',termCode:'114-1'}]
  if(path==='/learning-history/courses')return []
  if(path.includes('term=115-2'))return {items:[{id:'now',title:'當學期紀錄',state:'graded',score:80,courseId:'current',url:'/current'}],total:1}
  if(path.includes('term=114-1'))return {items:[{id:'past',title:'歷年紀錄',state:'graded',score:90,courseId:'old',url:'/old'}],total:1}
  return {items:[],total:0}
 })
 const view=mount(OverviewView,{global});await flushPromises()
 expect(view.findAll('.dashboard-course')).toHaveLength(1);expect(view.text()).not.toContain('歷年數學')
 expect(api).toHaveBeenCalledWith('/learning-history?todo=true&page=1&pageSize=5&term=115-2')
 expect(api).toHaveBeenCalledWith('/learning-history?page=1&pageSize=12&term=115-2')
 await view.get('select[aria-label="學年度"]').setValue('114');await view.get('select[aria-label="學期"]').setValue('1');await flushPromises()
 expect(view.text()).toContain('歷年數學');expect(view.text()).toContain('歷年紀錄');expect(view.text()).not.toContain('當學期紀錄')
 view.unmount()
})

it('keeps successful sections visible and explains unavailable data', async () => {
  api.mockImplementation(async path => {
    if (path === '/courses') return [course]
    if (path === '/learning-history/courses') return []
    if (path.includes('todo=true')) throw new Error('待辦服務暫時無法使用')
    return { total: 0, items: [] }
  })
  const view = mount(OverviewView, { global }); await flushPromises()
  expect(view.text()).toContain('待辦服務暫時無法使用')
  expect(view.text()).not.toContain('目前沒有待交評量')
  expect(view.get('a[href="/courses/c"]').text()).toContain('數學課')
  expect(view.find('button').text()).toContain('重新整理')
  view.unmount()
})

it('shows teacher work queues and skips restricted grading/statistics requests', async () => {
  auth.user.canTeach = true
  api.mockImplementation(async path => {
    if (path === '/courses') return [{ ...course, courseRole: 'teacher' }, { ...course, id: 'ta', title: '助教課程', courseRole: 'ta', taRestrictions: ['grading', 'statistics'] }]
    if (path.includes('/questions')) return { items: [{ id: 'q', title: '尚無回覆的討論', replyCount: 0, locked: false }, { id: 'r', title: '已回覆', replyCount: 2 }, { id: 'closed', title: '已關閉', replyCount: 0, locked: true }] }
    if (path === '/courses/c/curriculum') return { activities: [{ id: 'a', kind: 'assignment', title: '文字作業', status: 'published' }, { id: 'pdf', kind: 'report', title: '檔案繳交作業', status: 'published' }] }
    if (path === '/assignments/a/submissions') return [{ id: 's', studentName: '測試學員', submittedAt: '2026-11-12T10:00:00Z', gradingStatus: 'ungraded' }, { id: 'scored', studentName: '已評同學', gradingStatus: 'draft' }]
    if (path === '/reports/pdf/submissions') return [{ id: 'pdfs', studentName: '第一組', submittedAt: '2026-11-13T10:00:00Z', score: null }, { id: 'returned', score: null, returned: true }]
    throw new Error('Unexpected request: ' + path)
  })
  const view = mount(OverviewView, { global }); await flushPromises()
  expect(view.text()).toContain('待批改'); expect(view.text()).toContain('尚無回覆的討論')
  expect(view.text()).not.toContain('已關閉')
  expect(view.get('a[href="/courses/c/assignments/a"]').text()).toContain('文字作業')
  expect(view.get('a[href="/courses/c/reports/pdf"]').text()).toContain('檔案繳交作業')
  expect(api.mock.calls.some(([path]) => path.includes('/statistics') || path.includes('/courses/ta/curriculum'))).toBe(false)
  expect(view.text()).toContain('助教批改權限')
  view.unmount()
})
it('requests five student tasks by default and supports larger server pages without dropping remaining tasks',async()=>{
 api.mockImplementation(async path=>{
  if(path==='/courses')return [course]
  if(path==='/learning-history/courses')return []
  if(path.includes('todo=true')){const query=new URLSearchParams(path.split('?')[1]),size=Number(query.get('pageSize')),page=Number(query.get('page'));return {total:12,items:Array.from({length:Math.min(size,12-(page-1)*size)},(_,i)=>({id:`t${(page-1)*size+i}`,title:`待辦 ${(page-1)*size+i+1}`,state:'todo',url:'/task'}))}}
  return {items:[],total:0}
 })
 const v=mount(OverviewView,{global});await flushPromises()
 expect(api).toHaveBeenCalledWith('/learning-history?todo=true&page=1&pageSize=5');expect(v.findAll('#learning-tasks li')).toHaveLength(5)
 await v.get('select[aria-label="學習待辦 每頁筆數"]').setValue('10');await flushPromises();expect(v.findAll('#learning-tasks li')).toHaveLength(10)
 await v.get('button[aria-label="學習待辦 下一頁"]').trigger('click');await flushPromises();expect(v.findAll('#learning-tasks li')).toHaveLength(2);expect(v.text()).toContain('待辦 12');v.unmount()
})
