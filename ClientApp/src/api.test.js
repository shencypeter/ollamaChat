import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { api, resetCsrf } from './api'
beforeEach(()=>resetCsrf());afterEach(()=>vi.unstubAllGlobals())
function response(status,body){return {ok:status>=200&&status<300,status,json:async()=>body}}
describe('same-origin API client',()=>{
  it('gets an antiforgery token before writes and sends credentials',async()=>{const mock=vi.fn().mockResolvedValueOnce(response(200,{token:'csrf-value'})).mockResolvedValueOnce(response(200,{id:1}));vi.stubGlobal('fetch',mock);await api('/courses',{method:'POST',body:{title:'課程'}});expect(mock.mock.calls[0][0]).toBe('/api/auth/csrf');expect(mock.mock.calls[1][1].headers['X-CSRF-TOKEN']).toBe('csrf-value');expect(mock.mock.calls[1][1].credentials).toBe('same-origin');expect(JSON.parse(mock.mock.calls[1][1].body)).toEqual({title:'課程'})})
  it('reports forbidden operations without treating them as success',async()=>{vi.stubGlobal('fetch',vi.fn().mockResolvedValue(response(403,null)));await expect(api('/accounts')).rejects.toMatchObject({status:403})})
  it('handles a successful empty response',async()=>{vi.stubGlobal('fetch',vi.fn().mockResolvedValueOnce(response(200,{token:'csrf'})).mockResolvedValueOnce(response(204,null)));expect(await api('/auth/logout',{method:'POST'})).toBeNull()})
})
