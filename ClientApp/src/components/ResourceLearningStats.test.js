import {mount,flushPromises} from '@vue/test-utils'
import {it,expect,vi,beforeEach} from 'vitest'
import ResourceLearningStats from './ResourceLearningStats.vue'
import {api} from '../api'

vi.mock('../api',()=>({api:vi.fn()}))
beforeEach(()=>api.mockReset())

function render(data){api.mockResolvedValue(data);return mount(ResourceLearningStats,{props:{courseId:'c',resourceId:'r',title:'合成教材'}})}

it('shows accumulated time only for video resources',async()=>{
 const view=render({isVideo:true,studentCount:2,watchedCount:1,totalSeconds:60,totalContentSeconds:120,students:[
  {id:'s',displayName:'合成學生',studentNumber:'S1',accumulatedSeconds:60,contentSeconds:120,playbackSpeeds:[],lastWatchedAt:'2026-11-12'}
 ]});await flushPromises()
 expect(view.text()).toContain('正式學習時間');expect(view.text()).toContain('實際觀看時間');expect(view.text()).toContain('影片內容時間')
 expect(view.text()).not.toContain('總瀏覽次數');view.unmount()
})

it('shows the viewed roster, total views and likes without time for static resources',async()=>{
 const view=render({isVideo:false,studentCount:3,viewedCount:2,viewCount:5,likes:4,students:[
  {id:'a',displayName:'甲同學',studentNumber:'S1',views:3,lastViewedAt:'2026-11-12'},
  {id:'b',displayName:'乙同學',studentNumber:'S2',views:2,lastViewedAt:'2026-11-11'},
  {id:'c',displayName:'未點閱',studentNumber:'S3',views:0,lastViewedAt:null}
 ]});await flushPromises()
 expect(view.text()).toContain('學生點閱名冊');expect(view.text()).toContain('總瀏覽 5 次');expect(view.text()).toContain('讚 4')
 expect(view.text()).toContain('甲同學');expect(view.text()).toContain('乙同學');expect(view.text()).not.toContain('未點閱')
 expect(view.text()).not.toContain('觀看時間');expect(view.text()).not.toContain('內容時間');view.unmount()
})
