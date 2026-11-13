import {flushPromises,mount} from '@vue/test-utils'
import {reactive} from 'vue'
import {beforeEach,expect,it,vi} from 'vitest'
import ExamManager from './ExamManager.vue'
import {api} from '../api'
vi.mock('../api',()=>({api:vi.fn()}))
vi.mock('vue-router',()=>({useRoute:()=>route}))
const route=reactive({query:{}})
const base='/courses/c/exams'
const exams=[{id:'a',title:'線上期中',status:'published'},{id:'b',title:'線上期末',status:'published'}]
function render(staff=false){return mount(ExamManager,{props:{course:{id:'c',status:'active'},staff},global:{stubs:{AssignmentEditor:true,QuestionAnswer:true}}})}
beforeEach(()=>{api.mockReset();route.query={};api.mockImplementation(async path=>{
 if(path===base)return exams
 if(path.endsWith('/attempt'))return {answers:{},version:1,submittedAt:null,score:null}
 if(path.includes('/results?'))return []
 const exam=exams.find(exam=>path===`${base}/${exam.id}`)
 if(exam)return {...exam,questions:[],isPractice:false}
 throw new Error(`Unexpected lookup ${path}`)
})})
it('opens the authorized online agenda selection and keeps the existing student attempt flow',async()=>{
 route.query={onlineExam:'b'};const view=render();await flushPromises()
 expect(view.get('h3').text()).toBe('線上期末')
 expect(api).toHaveBeenCalledWith(`${base}/b`)
 expect(api).toHaveBeenCalledWith(`${base}/b/attempt`)
 expect(api.mock.calls.every(([,options])=>!options)).toBe(true)
 view.unmount()
})
it('does not look up a foreign online exam id or consume the paper exam query',async()=>{
 for(const query of [{onlineExam:'foreign'},{exam:'a'}]){
  api.mockClear();route.query=query;const view=render();await flushPromises()
  expect(api).toHaveBeenCalledTimes(1);expect(api).toHaveBeenCalledWith(base)
  expect(view.find('h3').exists()).toBe(false);view.unmount()
 }
})
it('opens a second authorized exam on a same-component route change and ignores later foreign ids',async()=>{
 route.query={onlineExam:'a'};const view=render(true);await flushPromises()
 expect(view.get('h3').text()).toBe('線上期中')
 route.query={onlineExam:'b'};await flushPromises()
 expect(view.get('h3').text()).toBe('線上期末')
 expect(api).toHaveBeenCalledWith(`${base}/b/results?sort=score`)
 api.mockClear();route.query={onlineExam:'foreign'};await flushPromises()
 expect(api).not.toHaveBeenCalled();expect(view.get('h3').text()).toBe('線上期末')
 view.unmount()
})
