import { afterEach, describe, expect, it, vi } from 'vitest'
import { bindExperience, experience, setStudentExperience } from './experience'
import { api, resetCsrf } from '../api'
afterEach(() => { localStorage.clear(); bindExperience(null); resetCsrf(); vi.unstubAllGlobals() })
describe('student experience is a view, not a different identity', () => {
  it('allows professor and TA to select student view and starts each login in the authorized perspective', () => {
    for (const role of ['Administrator', 'Member']) {
      const user = { id: role, role,canTeach:true }; bindExperience(user); setStudentExperience(true); expect(experience.student).toBe(true); expect(user.role).toBe(role)
      bindExperience(null); bindExperience(user); expect(experience.student).toBe(false)
      bindExperience({ id: 'another', role,canTeach:true }); expect(experience.student).toBe(false)
    }
  })
  it('does not enable practice for actual students or leak the previous user preference', () => {
    bindExperience({ id: 'teacher', role: 'Member',canTeach:true }); setStudentExperience(true)
    bindExperience({ id: 'student', role: 'Member',canTeach:false }); setStudentExperience(true); expect(experience.canSwitch).toBe(false); expect(experience.student).toBe(false)
  })
  it('adds a view header to data requests but not to login/session operations', async () => {
    bindExperience({ id: 'teacher', role: 'Member',canTeach:true }); setStudentExperience(true)
    const fetch = vi.fn().mockResolvedValue({ ok: true, status: 200, json: async () => ({}) }); vi.stubGlobal('fetch', fetch)
    await api('/courses'); expect(fetch.mock.calls[0][1].headers['X-Portal-View']).toBe('student')
    await api('/auth/session'); expect(fetch.mock.calls[1][1].headers['X-Portal-View']).toBeUndefined()
    await api('/studio/profile'); expect(fetch.mock.calls[2][1].headers['X-Portal-View']).toBeUndefined()
  })
})
