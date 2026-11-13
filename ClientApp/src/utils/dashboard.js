import { activityLocation } from './courseActivityNavigation'

function limitedApi(api, concurrency = 4) {
  const queue = []
  let active = 0
  function next() {
    while (active < concurrency && queue.length) {
      const { path, resolve, reject } = queue.shift()
      active++
      Promise.resolve().then(() => api(path)).then(resolve, reject).finally(() => { active--; next() })
    }
  }
  return path => new Promise((resolve, reject) => { queue.push({ path, resolve, reject }); next() })
}

export async function loadDashboard(api, teaching) {
  const request = limitedApi(api)
  const data = { courses: [], learningCourses: [], tasks: [], taskTotal: 0, grades: [], pending: [], recent: [], discussions: [], errors: [], unavailable: {}, restricted: [] }
  async function read(path, label, area, accept) {
    try { await accept(await request(path)) }
    catch (error) { data.errors.push(`${label}：${error.message}`); data.unavailable[area] = true }
  }
  const courses = read('/courses', '課程清單', 'courses', rows => {
    data.courses = rows.filter(course => teaching ? ['teacher', 'ta'].includes(course.courseRole) : course.courseRole === 'student')
  })
  if (!teaching) {
    await Promise.all([
      courses,
      read('/learning-history/courses', '修課進度', 'progress', rows => { data.learningCourses = rows }),
      read('/learning-history?todo=true&pageSize=6', '學習待辦', 'tasks', page => { data.tasks = page.items; data.taskTotal = page.total }),
      read('/learning-history?page=1&pageSize=12', '近期成績', 'grades', page => { data.grades = page.items.filter(item => item.state === 'graded' && item.score != null) })
    ])
    return data
  }
  await courses
  await Promise.all(data.courses.filter(course => course.status === 'active').flatMap(course => {
    const jobs = [read(`/courses/${course.id}/questions?all=true`, `${course.title}討論`, 'discussions', result => {
      data.discussions.push(...result.items.filter(item => !item.locked && !item.answeredAt && item.replyCount === 0).map(item => ({ ...item, courseTitle: course.title, url: `/courses/${course.id}?section=questions&thread=${encodeURIComponent(item.id)}` })))
    })]
    if (course.courseRole === 'ta' && course.taRestrictions?.includes('grading')) { data.restricted.push(course.title); return jobs }
    jobs.push(read(`/courses/${course.id}/curriculum`, `${course.title}評量`, 'grading', async curriculum => {
      await Promise.all(curriculum.activities.filter(activity => ['quiz', 'assignment', 'report'].includes(activity.kind) && activity.status !== 'draft').map(activity => {
        const pdf = activity.kind === 'report'
        return read(`/${pdf ? 'reports' : 'assignments'}/${activity.id}/submissions`, `${course.title}／${activity.title}繳交`, 'grading', rows => {
          const submissions = rows.filter(row => !row.returned)
          const pending = submissions.filter(row => pdf ? row.score == null : ['ungraded', 'pending_essay'].includes(row.gradingStatus))
          const shared = { title: activity.title, courseTitle: course.title, url: activityLocation(course.id, activity) }
          if (pending.length) data.pending.push({ ...shared, id: activity.id, count: pending.length })
          data.recent.push(...submissions.map(row => ({ ...shared, id: row.id, studentName: row.studentName, submittedAt: row.submittedAt })))
        })
      }))
    }))
    return jobs
  }))
  data.recent.sort((a, b) => (Date.parse(b.submittedAt) || 0) - (Date.parse(a.submittedAt) || 0))
  data.discussions.sort((a, b) => (Date.parse(b.createdAt) || 0) - (Date.parse(a.createdAt) || 0))
  return data
}
