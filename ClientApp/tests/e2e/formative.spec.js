import {test,expect} from '@playwright/test'
import {readFileSync} from 'node:fs'
const state=JSON.parse(readFileSync(new URL('../../../.codex-build/portal-smoke.json',import.meta.url),'utf8'))
test('assignment AI endpoints are disabled for every role',async({playwright,baseURL})=>{
 for(const user of state.accounts){
  const ctx=await playwright.request.newContext({baseURL});let token=(await(await ctx.get('/api/auth/csrf')).json()).token;
  expect((await ctx.post('/api/auth/login',{headers:{'X-CSRF-TOKEN':token},data:{email:user.email,password:state.password}})).status()).toBe(200);
  token=(await(await ctx.get('/api/auth/csrf')).json()).token;
  const path='/api/assignments/00000000-0000-0000-0000-000000000001/questions/00000000-0000-0000-0000-000000000001/ai-feedback';
  expect((await ctx.get(path)).status()).toBe(403);expect((await ctx.post(path,{headers:{'X-CSRF-TOKEN':token},data:{}})).status()).toBe(403);await ctx.dispose();
 }
})
