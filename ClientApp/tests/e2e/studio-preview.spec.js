import {retryLogin} from '../helpers/auth'
import {test,expect} from '@playwright/test'
import {readFileSync} from 'node:fs'
const state=JSON.parse(readFileSync(new URL('../../../.codex-build/portal-smoke.json',import.meta.url),'utf8'))
test('published navigation uses the new site map without calling a model',async({page})=>{
 await retryLogin(page.request,{email:state.accounts.find(a=>a.role==='Professor').email,password:state.password});await page.goto('/courses');await expect(page).toHaveURL(/\/courses$/)
 await expect(page.getByRole('heading',{name:'課程管理',exact:true})).toBeVisible();expect(await page.locator('a[href="/courses"]').count()).toBeGreaterThan(0);expect(await page.locator('a[href="/ai"]').count()).toBe(0)
 await page.getByRole('navigation',{name:'工作區導覽'}).getByRole('link',{name:'管理後台',exact:true}).click();await expect(page.getByRole('navigation',{name:'管理後台功能'}).getByRole('link')).toHaveCount(6);await expect(page.getByRole('heading',{name:'管理後台首頁',exact:true})).toBeVisible()
 const settings=await(await page.request.get('/api/studio/admin/settings')).json();if(process.env.OLLAMA_INSPECTION_STUB!=='1')expect(settings.model).not.toBe('inspection-test');expect(settings.profile).toBeTruthy();
 await page.goto('/account');await page.getByRole('button',{name:'登出並切換帳號'}).click();await expect(page).toHaveURL(/login/)
})

