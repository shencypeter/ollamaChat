import {expect,it} from 'vitest'
import {academicYears,matchesTerm,splitTerm,termQuery} from './academicCalendar'

it('keeps an unset current term unset even when newer course records exist',()=>{
 expect(splitTerm(null)).toEqual({year:'',semester:''})
 expect(academicYears(['114-2','116-1','115-S'],null)).toEqual(['116','115','114'])
 expect(matchesTerm('114-2','','')).toBe(true)
})
it('supports summer and both partial and exact academic filters without guessing dates',()=>{
 expect(splitTerm('115-S')).toEqual({year:'115',semester:'S'})
 expect(matchesTerm('115-S','115','S')).toBe(true)
 expect(matchesTerm('114-S','115','S')).toBe(false)
 expect(termQuery('115','S')).toBe('term=115-S')
 expect(termQuery('115','')).toBe('year=115')
 expect(termQuery('','S')).toBe('semester=S')
 expect(termQuery('','')).toBe('')
})
