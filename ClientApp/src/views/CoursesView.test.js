import {mount,flushPromises} from '@vue/test-utils'
import {expect,it,vi} from 'vitest'
import CoursesView from './CoursesView.vue'

vi.mock('../composables/auth',()=>({auth:{user:{canTeach:true,canCreateCourses:true}},loadSession:vi.fn()}))
vi.mock('../api',()=>({api:vi.fn(async()=>[
  {id:'a',title:'指導課程',courseRole:'teacher',status:'active'},{id:'b',title:'進修課程',courseRole:'student'},
  {id:'draft',title:'備課中',courseRole:'teacher',status:'draft'},{id:'c',title:'協同助教課程',courseRole:'ta'}
])}))
it.each([
  [true,['指導課程','進修課程','備課中'],false],
  [false,['指導課程','備課中','協同助教課程'],true]
])('shows the same professor the courses for their selected portal',async(enrolled,titles,canCreate)=>{
  const view=mount(CoursesView,{props:{enrolled},global:{stubs:{RouterLink:{name:'RouterLink',props:['to'],template:'<a><slot/></a>'}}}})
  await flushPromises()
  expect(view.findAll('.course-card h2').map(h=>h.text())).toEqual(titles)
  expect(view.find('.page-heading button').exists()).toBe(canCreate)
  if(enrolled){
    const links=view.findAllComponents({name:'RouterLink'}).filter(l=>l.classes('course-open-link'))
    expect(links.map(l=>l.props('to'))).toEqual([{path:'/courses/a',query:{view:'preview'}},{path:'/courses/b',query:{}},{path:'/courses/draft',query:{view:'preview'}}])
  }
  view.unmount()
})
