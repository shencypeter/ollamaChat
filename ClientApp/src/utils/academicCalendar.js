export const semesters = [{code:'1',label:'上學期'},{code:'2',label:'下學期'},{code:'S',label:'暑修'}]
export function splitTerm(term) { const match=/^(\d+)-(1|2|S)$/.exec(term||''); return match ? {year:match[1],semester:match[2]} : {year:'',semester:''} }
export function matchesTerm(term,year,semester) { const parts=splitTerm(term);return (!year||parts.year===year)&&(!semester||parts.semester===semester) }
export function academicYears(terms,currentTerm) { return [...new Set([...terms,currentTerm].map(term=>splitTerm(term).year).filter(Boolean))].sort((a,b)=>Number(b)-Number(a)) }
export function termQuery(year,semester) { const q=new URLSearchParams();if(year&&semester)q.set('term',`${year}-${semester}`);else {if(year)q.set('year',year);if(semester)q.set('semester',semester)}return q.toString() }
