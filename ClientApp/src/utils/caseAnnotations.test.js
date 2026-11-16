import {describe,it,expect} from 'vitest'
import {annotationSegments,validateAnnotations,phaseForStage,annotationPhaseAlignment} from './caseAnnotations'
const catalog=[{code:'A1a',label:'年齡',phase:'A'},{code:'A1b',label:'性別',phase:'A'},{code:'C5',label:'日常生活活動',phase:'C'}]
const ann=(start,end,text,codes=['A1a'],extra={})=>({annotationId:'ann',start,end,text,codes,explanation:'辨識到描述',status:'unverified',...extra})
describe('exact UTF-16 submitted-answer annotations',()=>{
 it('compares only valid exact-original classifications with the selected stage without judging clinical correctness',()=>{
  expect(['premorbid','illnessCourse','functionalAssessment','psychosocial','raw','B',undefined].map(phaseForStage)).toEqual(['A','B','C','D',null,null,null])
  const mixed=annotationPhaseAlignment('55ADL',[ann(0,2,'55'),ann(2,5,'ADL',['C5'],{status:'unsupported'}),ann(0,2,'XX',['A1b'])],catalog,'functionalAssessment')
  expect(mixed).toMatchObject({phase:'C',currentCodes:['C5'],otherCodes:['A1a'],otherPhases:['A'],misplacedCodes:[]})
  expect(annotationPhaseAlignment('55',[ann(0,2,'XX')],catalog,'illnessCourse')).toBeNull()
  expect(annotationPhaseAlignment('55',[ann(0,2,'55')],catalog,'raw')).toBeNull()
 })
 it('keeps Chinese/English, whitespace, special characters and emoji exactly once with overlapping multi-phase codes',()=>{
  const text='  55歲 ADL😀<b>\n',annotations=[ann(2,5,'55歲',['A1a','A1b']),ann(4,9,'歲 ADL',['C5'])]
  const segments=annotationSegments(text,annotations,catalog)
  expect(segments.map(s=>s.text).join('')).toBe(text)
  expect(segments.map(s=>[s.text,s.codes])).toEqual([['  ',[]],['55',['A1a','A1b']],['歲',['A1a','A1b','C5']],[' ADL',['C5']],['😀<b>\n',[]]])
  expect(segments[2].phases).toEqual(['A','C'])
 })
 it('uses explicit offsets for repeated phrases and never searches a replacement occurrence',()=>{
  expect(annotationSegments('男性 男性',[ann(3,5,'男性',['A1b'])],catalog).map(s=>[s.text,s.codes])).toEqual([['男性 ',[]],['男性',['A1b']]])
  expect(validateAnnotations('男性 男性',[ann(2,4,'男性')],catalog)).toEqual([])
 })
 it('rejects invalid bounds, nonintegers, text mismatch, unsupported codes and split surrogate pairs',()=>{
  const text='😀55歲'
  const invalid=[ann(-1,2,'😀'),ann(0,99,text),ann(2,2,''),ann(.5,2,'😀'),ann(0,2,'錯'),ann(2,5,'55歲',['A2c']),ann(0,1,'\ud83d'),ann(1,2,'\ude00'),ann(0,2,'😀',['A1a','B2c'])]
  expect(validateAnnotations(text,invalid,catalog)).toEqual([])
  expect(validateAnnotations(text,[ann(0,2,'😀')],catalog)).toHaveLength(1)
 })
 it('handles empty, legacy and malformed data without inventing labels',()=>{
  for(const list of [[],null,undefined,{},[null],[ann(0,1,'x',[])]])expect(annotationSegments('x',list,catalog)).toEqual([{start:0,end:1,text:'x',annotations:[],codes:[],phases:[]}])
  expect(annotationSegments('',[],catalog)).toEqual([])
  expect(validateAnnotations('x',[ann(0,1,'x')],[])).toEqual([])
 })
 it('preserves independent recognition statuses and adjacent annotations',()=>{
  const segments=annotationSegments('55男',[ann(0,2,'55',undefined,{status:'unsupported'}),ann(2,3,'男',['A1b'],{annotationId:'b',status:'misplaced'})],catalog)
  expect(segments.map(s=>s.annotations[0].status)).toEqual(['unsupported','misplaced'])
 })
})
