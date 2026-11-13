import {mount} from '@vue/test-utils'
import {nextTick} from 'vue'
import {beforeEach,it,expect,vi} from 'vitest'
import PortalPanel from './PortalPanel.vue'
import {auth} from '../composables/auth'
vi.mock('../composables/auth',()=>({auth:{user:{id:'one'}}}))
beforeEach(()=>{localStorage.clear();auth.user={id:'one'};window.location.hash='';vi.restoreAllMocks()})
function render(scope='student'){return mount(PortalPanel,{props:{id:'tasks',panelKey:'tasks',scope,title:'學習待辦',count:'3 項'},slots:{default:'<input value="keep my input"/>',actions:'<a href="/learning-history">全部待辦</a>'}})}
it('starts expanded with native keyboard control and keeps the count and form state when folded',async()=>{
 const v=render();expect(v.get('details').element.open).toBe(true);expect(v.get('summary').text()).toContain('學習待辦');expect(v.get('summary').text()).toContain('3 項')
 v.get('details').element.open=false;await v.get('details').trigger('toggle');await nextTick()
 expect(v.get('details').element.open).toBe(false);expect(v.get('input').element.value).toBe('keep my input');expect(v.find('summary a').exists()).toBe(false)
 v.unmount();const restored=render();expect(restored.get('details').element.open).toBe(false);restored.unmount()
})
it('isolates preferences by account and perspective and opens a folded section for an anchor',async()=>{
 let v=render();v.get('details').element.open=false;await v.get('details').trigger('toggle');v.unmount()
 v=render('teaching');expect(v.get('details').element.open).toBe(true);v.unmount()
 auth.user={id:'two'};v=render();expect(v.get('details').element.open).toBe(true);v.unmount()
 auth.user={id:'one'};v=render();expect(v.get('details').element.open).toBe(false)
 window.location.hash='#tasks';window.dispatchEvent(new Event('hashchange'));await nextTick();expect(v.get('details').element.open).toBe(true);v.unmount()
})
it('works when browser storage is unavailable and does not persist an anonymous preference',async()=>{
 vi.spyOn(Storage.prototype,'getItem').mockImplementation(()=>{throw new Error('blocked')});const write=vi.spyOn(Storage.prototype,'setItem').mockImplementation(()=>{throw new Error('blocked')})
 const v=render();v.get('details').element.open=false;await v.get('details').trigger('toggle');expect(v.get('details').element.open).toBe(false);v.unmount()
 write.mockClear();auth.user=null;const anonymous=render();anonymous.get('details').element.open=false;await anonymous.get('details').trigger('toggle');expect(write).not.toHaveBeenCalled();anonymous.unmount()
})
it('defaults to five items and paginates all local records with selectable size',async()=>{
 const items=Array.from({length:12},(_,i)=>({id:i,title:`項目 ${i+1}`}))
 const v=mount(PortalPanel,{props:{panelKey:'list',scope:'student',title:'項目',items},slots:{default:'<template #default="{items}"><ul><li v-for="item in items" :key="item.id">{{item.title}}</li></ul></template>'}})
 expect(v.findAll('li')).toHaveLength(5);expect(v.get('select').element.value).toBe('5')
 await v.get('button[aria-label="項目 下一頁"]').trigger('click');expect(v.findAll('li')[0].text()).toBe('項目 6')
 await v.get('select').setValue('10');expect(v.findAll('li')).toHaveLength(10);expect(v.findAll('li')[0].text()).toBe('項目 1')
 await v.get('button[aria-label="項目 下一頁"]').trigger('click');expect(v.findAll('li').map(li=>li.text())).toEqual(['項目 11','項目 12']);v.unmount()
})
it('requests server pages and resets to page one when the size changes',async()=>{
 const v=mount(PortalPanel,{props:{panelKey:'inbox',scope:'student',title:'訊息',remote:true,remotePage:3,remoteSize:5,hasMore:true,items:[{id:1}]}})
 await v.get('select').setValue('20');expect(v.emitted('page-change').at(-1)).toEqual([{page:1,pageSize:20}])
 await v.get('button[aria-label="訊息 上一頁"]').trigger('click');expect(v.emitted('page-change').at(-1)).toEqual([{page:2,pageSize:20}]);v.unmount()
})
