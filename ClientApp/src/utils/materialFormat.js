import {File,FileText,FileSpreadsheet,Presentation,Image,Video,Archive,FileType} from '@lucide/vue'
const types=[
  ['PDF',FileType,/application\/pdf/,['pdf']],
  ['Word',FileText,/msword|wordprocessingml/,['doc','docx']],
  ['Excel',FileSpreadsheet,/ms-excel|spreadsheetml/,['xls','xlsx']],
  ['PowerPoint',Presentation,/powerpoint|presentationml/,['ppt','pptx']],
  ['文字',FileText,/^text\//,['txt','md']],
  ['圖片',Image,/^image\//,['jpg','jpeg','png','webp','gif']],
  ['影片',Video,/^video\//,['mp4','webm']],
  ['壓縮檔',Archive,/zip|7z/,['zip','7z']]
]
export function materialFormat(item){
  const mime=item.previewType || item.learningType || item.contentType || ''
  const ext=item.fileName?.split('.').pop()?.toLowerCase()
  const [label,icon]=types.find(t=>t[2].test(mime)) || types.find(t=>t[3].includes(ext)) || ['檔案',File]
  return item.resourceType==='youtube'?{label:'YouTube',icon:Video}:{label,icon}
}
