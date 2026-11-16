import {it,expect} from 'vitest'
import {safeReturnPath} from './returnPath'
it('preserves a concrete material route and its query through login',()=>{
 expect(safeReturnPath('/courses/c/materials/r?from=share#document')).toBe('/courses/c/materials/r?from=share#document')
})
it('rejects external, ambiguous, login-loop and API destinations',()=>{
 for(const path of ['https://example.com','//example.com','/\\example.com','/login?next=/courses','/api/accounts','/\nexample.com',null,['/courses']])expect(safeReturnPath(path)).toBe('/')
})
