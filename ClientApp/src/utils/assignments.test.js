import { describe, expect, it } from 'vitest'
import { toLocalInput, toUtc, formatDate } from './assignments'
describe('assignment dates', () => {
  it('round trips a UTC deadline through the local datetime input', () => { const utc='2026-11-15T08:30:00.000Z'; expect(toUtc(toLocalInput(utc))).toBe(utc) })
  it('keeps optional opening/deadline fields absent', () => { expect(toLocalInput(null)).toBe(''); expect(toUtc('')).toBeNull(); expect(formatDate(null)).toBe('未設定') })
})
