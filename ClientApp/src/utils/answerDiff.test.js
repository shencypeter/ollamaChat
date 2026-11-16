import { describe, expect, it } from 'vitest'
import { diffAnswers } from './answerDiff'

function reconstruct(result, side) {
  return result.segments.filter(s => s.type === 'equal' || s.type === side).map(s => s.text).join('')
}
describe('answer revision differences', () => {
  it.each([
    ['原句。', '原句。'], ['', '新答案'], ['舊答案', ''], ['', ''],
    ['第一行\n  第二行\t', '第一行\n 第二行\t\n'],
    ['觀察🙂與👩‍⚕️', '觀察🙃與👩‍⚕️。'],
    ['<script>alert(1)</script>', '<img src=x onerror=alert(1)>'],
    ['abcABC', 'ABCabc'], ['作答一，補充。', '作答二，補充資料。'],
  ])('reconstructs both exact inputs: %j → %j', (left, right) => {
    const result = diffAnswers(left, right)
    expect(reconstruct(result, 'remove')).toBe(left)
    expect(reconstruct(result, 'add')).toBe(right)
    expect(result.unchanged).toBe(left === right)
    for (const segment of result.segments) {
      expect(segment.text).not.toMatch(/^[\uDC00-\uDFFF]|[\uD800-\uDBFF]$/u)
    }
  })
  it('identifies exact localized additions and removals', () => {
    expect(diffAnswers('我有症狀。', '我沒有症狀。').segments).toEqual([
      { type: 'equal', text: '我' }, { type: 'add', text: '沒' }, { type: 'equal', text: '有症狀。' },
    ])
    expect(diffAnswers('ABC', 'AC').segments).toContainEqual({ type: 'remove', text: 'B' })
  })
  it('bounds work for 8,000 differing characters and reports the exact fallback', () => {
    const left = '甲'.repeat(8000), right = '乙'.repeat(8000)
    const result = diffAnswers(left, right)
    expect(result.fallback).toBe(true)
    expect(result.segments).toEqual([{ type: 'remove', text: left }, { type: 'add', text: right }])
    expect(reconstruct(result, 'remove')).toBe(left)
    expect(reconstruct(result, 'add')).toBe(right)
  })
  it('retains shared edges when the bounded middle falls back', () => {
    const result = diffAnswers('前🙂甲甲尾\n', '前🙂乙乙尾\n', { maxCells: 1 })
    expect(result.fallback).toBe(true)
    expect(result.segments[0]).toEqual({ type: 'equal', text: '前🙂' })
    expect(result.segments.at(-1)).toEqual({ type: 'equal', text: '尾\n' })
  })
  it('reconstructs deterministic mixed edits in either comparison direction', () => {
    let seed = 71
    const alphabet = ['甲', '乙', '🙂', ' ', '\n', '<', '>']
    const next = () => { seed = (seed * 16807) % 2147483647; return seed }
    for (let sample = 0; sample < 80; sample++) {
      const make = () => Array.from({ length: next() % 35 }, () => alphabet[next() % alphabet.length]).join('')
      const left = make(), right = make()
      for (const [a, b] of [[left, right], [right, left]]) {
        const result = diffAnswers(a, b)
        expect(reconstruct(result, 'remove')).toBe(a)
        expect(reconstruct(result, 'add')).toBe(b)
      }
    }
  })
})
