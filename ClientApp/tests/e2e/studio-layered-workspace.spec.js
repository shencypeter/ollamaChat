import {test,expect} from '@playwright/test'
import {retryLogin} from '../helpers/auth'

test.skip(!process.env.PORTAL_DEMO_PASSWORD,'Requires the local demo teacher password')

test('teacher browses from course content into a focused unit workspace',async({browser,baseURL})=>{
 const context=await browser.newContext({baseURL,ignoreHTTPSErrors:true})
 expect((await retryLogin(context.request,{email:'faculty1@portal-demo.example.invalid',password:process.env.PORTAL_DEMO_PASSWORD})).ok()).toBeTruthy()
 const courses=await (await context.request.get('/api/courses')).json()
 const course=courses.find(item=>item.courseRole==='teacher'&&item.status!=='archived')
 expect(course).toBeTruthy()
 const curriculum=await (await context.request.get(`/api/courses/${course.id}/curriculum`)).json()
 expect(curriculum.units.length).toBeGreaterThan(0)
 const unit=curriculum.units[0]
 const page=await context.newPage()
 await page.goto(`/courses/${course.id}`)
 await expect(page.getByRole('link',{name:'課程內容'})).toBeVisible()
 await page.getByRole('link',{name:/進入單元/}).first().click()
 await expect(page).toHaveURL(new RegExp(`/courses/${course.id}/units/${unit.id}$`))
 await expect(page.getByRole('heading',{name:unit.name})).toBeVisible()
 await expect(page.getByRole('navigation',{name:'工作區導覽'}).getByRole('link',{name:'返回課程內容'})).toBeVisible()
 await expect(page.getByRole('link',{name:'課程討論'})).toBeVisible()
 await context.close()
})
