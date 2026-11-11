<script setup>
import {computed,ref,reactive,nextTick,onMounted,onBeforeUnmount} from 'vue'
import {markNotificationsRead} from '../composables/notifications'
import {api} from '../api'
import {filterMaterialMetadata,groupMaterials} from '../utils/materialMetadata'
import {parseResourceTags,resourceTagChoices,filterResources} from '../utils/resourceTags'
import {materialFormat} from '../utils/materialFormat'
import ResourceLearningStats from './ResourceLearningStats.vue'
import FilePreview from './FilePreview.vue'
import {Pencil,ChevronDown,FileText,Play,UsersRound} from '@lucide/vue'
import {onBeforeRouteLeave,onBeforeRouteUpdate} from 'vue-router'
const props=defineProps({course:Object,staff:Boolean,section:{type:String,default:'resources'},showClassmates:{type:Boolean,default:true}})
const emit=defineEmits(['changed'])
const loading=ref(true)
const resources=ref([]),groups=ref([]),classmates=ref([]),units=ref([]),error=ref(''),notice=ref(''),busy=ref(false),file=ref(null),group=ref({name:'',members:[]})
const activePreview=ref(null)
const expandedResources=reactive({}),titleEdits=reactive({}),uploadTitle=ref(''),uploadTags=ref(''),sourceKind=ref('file'),youtubeUrl=ref(''),uploadTranscript=ref(''),tagEdits=reactive({}),selectedTag=ref(''),search=ref(''),onlyUntagged=ref(false),statsResource=ref(null)
const notifyStudents=ref(false),uploadUnit=ref('')
const uploadDate=ref(''),uploadSummary=ref(''),metadataEdits=reactive({}),dateFrom=ref(''),dateTo=ref(''),undated=ref(false),materialSort=ref('teaching_desc'),grouped=ref(true)
const base=`/courses/${props.course.id}`
const editable=computed(()=>props.staff && (props.section!=='groups'||!props.course.taRestrictions?.includes('assessments')) && (props.section!=='resources'||!props.course.taRestrictions?.includes('materials')) && props.course.status!=='archived')
const tagChoices=computed(()=>resourceTagChoices(resources.value))
const tagSuggestions=computed(()=>[...new Set([...Array.from({length:18},(_,i)=>`第${i+1}週`),...tagChoices.value.map(t=>t.name)])])
const rangeError=computed(()=>dateFrom.value&&dateTo.value&&dateFrom.value>dateTo.value?'授課日期起日不能晚於迄日。':'')
const visibleResources=computed(()=>filterMaterialMetadata(filterResources(resources.value,{tag:selectedTag.value,query:search.value,untagged:onlyUntagged.value}),{from:dateFrom.value,to:dateTo.value,undated:undated.value,sort:materialSort.value}))
const materialGroups=computed(()=>groupMaterials(visibleResources.value,grouped.value))
function metadataChanged(e){return e.date!==e.originalDate||e.summary!==e.originalSummary}
function editMetadata(item){metadataEdits[item.id]={date:item.teachingDate||'',summary:item.summary||'',originalDate:item.teachingDate||'',originalSummary:item.summary||'',expectedVersion:item.metadataVersion}}
async function saveMetadata(item){await run(async()=>{const e=metadataEdits[item.id];try{await api(`${base}/resources/${item.id}/metadata`,{method:'PUT',body:{teachingDate:e.date||null,summary:e.summary,expectedVersion:e.expectedVersion}})}catch(error){if(error.status===409)await load();throw error}delete metadataEdits[item.id];notice.value='授課日期與摘要已儲存。'})}
function chooseTag(tag){selectedTag.value=tag;onlyUntagged.value=false}
function tagsChanged(edit){try{return JSON.stringify(parseResourceTags(edit.text))!==JSON.stringify(edit.original)}catch{return true}}
function editTags(item){tagEdits[item.id]={text:(item.tags||[]).join(', '),original:[...(item.tags||[])],expectedVersion:item.tagsVersion}}
async function saveTags(item){await run(async()=>{const edit=tagEdits[item.id];try{await api(`${base}/resources/${item.id}/tags`,{method:'PUT',body:{tags:parseResourceTags(edit.text),expectedVersion:edit.expectedVersion}})}catch(e){if(e.status===409)await load();throw e}delete tagEdits[item.id];notice.value='教材標籤已儲存。'})}
const membership=computed(()=>new Map(groups.value.flatMap(g=>g.members.map(m=>[m.id,g]))))
const groupCandidates=computed(()=>classmates.value)
function assignedElsewhere(id){const owner=membership.value.get(id);return owner&&owner.id!==group.value.id}

async function load(){try{if(props.section==='resources'){const [items,curriculum]=await Promise.all([api(`${base}/resources`),api(`${base}/curriculum`)]);resources.value=items;units.value=curriculum?.units||[];if(!props.staff&&!props.course.canPractice){await nextTick();await markNotificationsRead('material',visibleResources.value.filter(r=>r.notifyStudents).map(r=>r.id))}}else [groups.value,classmates.value]=await Promise.all([api(`${base}/groups`),props.staff?api(`${base}/classmates`):[]])}catch(e){error.value=e.message}finally{loading.value=false}}
async function run(action){busy.value=true;error.value='';notice.value='';try{await action();await load()}catch(e){error.value=e.message}finally{busy.value=false}}
async function upload(){await run(async()=>{const tags=parseResourceTags(uploadTags.value);if(sourceKind.value==='youtube'){await api(`${base}/resources/youtube`,{method:'POST',body:{title:uploadTitle.value,url:youtubeUrl.value,notifyStudents:notifyStudents.value,unitId:uploadUnit.value||null,tags,transcript:uploadTranscript.value,teachingDate:uploadDate.value||null,summary:uploadSummary.value}});youtubeUrl.value='';uploadTranscript.value=''}else{const body=new FormData();body.append('file',file.value);body.append('notifyStudents',notifyStudents.value);body.append('title',uploadTitle.value);body.append('teachingDate',uploadDate.value);body.append('summary',uploadSummary.value);if(uploadUnit.value)body.append('unitId',uploadUnit.value);for(const tag of tags)body.append('tags',tag);await api(`${base}/resources`,{method:'POST',body});file.value=null}uploadTitle.value='';uploadTags.value='';uploadDate.value='';uploadSummary.value='';emit('changed');notice.value='教材已新增。'})}

function editTitle(item){titleEdits[item.id]={title:item.title||'',original:item.title||'',expectedVersion:item.titleVersion}}
async function saveTitle(item){await run(async()=>{const edit=titleEdits[item.id];try{await api(`${base}/resources/${item.id}/title`,{method:'PUT',body:{title:edit.title,expectedVersion:edit.expectedVersion}})}catch(e){if(e.status===409)await load();throw e}delete titleEdits[item.id];notice.value='文件名稱已儲存，原始檔案保持不變。'})}
const dirtyTitles=computed(()=>Object.values(titleEdits).some(e=>e.title!==e.original)||Object.values(tagEdits).some(tagsChanged)||Object.values(metadataEdits).some(metadataChanged))
function titleGuard(){return !dirtyTitles.value || confirm('教材有尚未儲存的修改，確定離開？')}
onBeforeRouteLeave(titleGuard);onBeforeRouteUpdate(titleGuard)
function titleUnload(event){if(dirtyTitles.value){event.preventDefault();event.returnValue=''}}
window.addEventListener('beforeunload',titleUnload);onBeforeUnmount(()=>window.removeEventListener('beforeunload',titleUnload))
async function download(item){await run(async()=>{const blob=await api(`${base}/resources/${item.id}/download`,{method:'POST',download:true});const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=item.fileName;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000)})}
async function dissolve(item){if(confirm('解散「'+item.name+'」？已繳交的作業會保留成員紀錄。'))await run(async()=>{await api(base+'/groups/'+item.id,{method:'DELETE'});group.value={name:'',members:[]}})}
async function saveGroup(){await run(async()=>{await api(`${base}/groups${group.value.id?'/'+group.value.id:''}`,{method:group.value.id?'PUT':'POST',body:group.value});group.value={name:'',members:[]};notice.value='小組已儲存。'})}
defineExpose({reload:load})
onMounted(load)
</script>
<template>
<p v-if="error" class="error-box" role="alert">{{error}}</p><p v-if="notice" role="status">{{notice}}</p>
<template v-if="section==='resources'"><section class="surface form-surface">
<h2>課程教材</h2>
<details v-if="editable"><summary>新增教材</summary><form @submit.prevent="upload">
<label>教材來源<select aria-label="教材來源" v-model="sourceKind" :disabled="busy"><option value="file">上傳檔案</option><option value="youtube">YouTube 課程回放</option></select></label>
<label>所屬單元<select aria-label="新增教材所屬單元" v-model="uploadUnit"><option value="">未歸類（可稍後安排）</option><option v-for="unit in units" :key="unit.id" :value="unit.id">{{unit.name}}</option></select></label>
<label>{{sourceKind==='youtube'?'回放名稱':'文件名稱（選填）'}}<input :aria-label="sourceKind==='youtube'?'回放名稱':'上傳文件名稱'" v-model="uploadTitle" :required="sourceKind==='youtube'" maxlength="200" placeholder="例如：第一週案例分析" /></label>
<label v-if="sourceKind==='file'">上傳檔案（最多 20 MB）<input type="file" required @change="file=$event.target.files[0]" /></label>
<label v-else>YouTube 網址<input aria-label="YouTube 網址" type="url" v-model="youtubeUrl" required maxlength="2000" placeholder="https://www.youtube.com/watch?v=…" /></label>
<label v-if="sourceKind==='youtube'">逐字稿（選填）<textarea aria-label="新增回放逐字稿" v-model="uploadTranscript" rows="6" maxlength="100000" placeholder="貼上課程逐字稿，可保留時間標記" /></label>
<label>授課日期（選填）<input aria-label="新增教材授課日期" type="date" v-model="uploadDate" /></label><label>教師摘要（選填）<textarea aria-label="新增教材教師摘要" v-model="uploadSummary" rows="4" maxlength="10000" placeholder="貼上本次教學重點；與逐字稿分開保存" /></label>
<label>教材標籤<input aria-label="新增教材標籤" v-model="uploadTags" :list="`resource-tags-${course.id}`" placeholder="第1週, 案例分析（以逗號分隔）" maxlength="1000" /></label><p class="field-hint">可自訂週次或主題；每份最多 10 個標籤、每個最多 30 字。YouTube 網址需允許嵌入，影片變更請新增另一份教材。</p>
<label><input type="checkbox" v-model="notifyStudents"/>通知學生有新教材</label><button class="button primary" :disabled="busy || (sourceKind==='file'?!file:!youtubeUrl)">{{sourceKind==='youtube'?'新增回放':'上傳'}}</button></form></details>
<datalist :id="`resource-tags-${course.id}`"><option v-for="tag in tagSuggestions" :key="tag" :value="tag" /></datalist>
<label>搜尋教材名稱或標籤<input aria-label="搜尋教材名稱或標籤" v-model="search" type="search" placeholder="輸入檔名、名稱、摘要或週次" /></label>
<details class="material-filters"><summary>日期、標籤與排序</summary><div class="form-grid"><label>授課日期起日<input aria-label="授課日期起日" type="date" v-model="dateFrom" :disabled="undated" /></label><label>授課日期迄日<input aria-label="授課日期迄日" type="date" v-model="dateTo" :disabled="undated" /></label><label>教材排序<select aria-label="教材排序" v-model="materialSort"><option value="teaching_desc">授課日期新到舊</option><option value="teaching_asc">授課日期舊到新</option><option value="uploaded_desc">上傳日期新到舊</option></select></label></div><label class="choice-label"><input type="checkbox" v-model="undated" @change="dateFrom='';dateTo=''" />只看未指定授課日期</label><label class="choice-label"><input type="checkbox" v-model="grouped" />依授課日期分組</label><p v-if="rangeError" class="error-box" role="alert">{{rangeError}}</p>
<div class="resource-tags" aria-label="教材標籤篩選"><button type="button" class="tag-chip" :aria-pressed="!selectedTag&&!onlyUntagged" @click="chooseTag('')">全部（{{resources.length}}）</button><button type="button" class="tag-chip" :aria-pressed="onlyUntagged" @click="selectedTag='';onlyUntagged=true">未標記</button><button v-for="tag in tagChoices" :key="tag.name" type="button" class="tag-chip" :aria-pressed="selectedTag===tag.name" @click="chooseTag(tag.name)">{{tag.name}}（{{tag.count}}）</button></div>
</details><p v-if="loading" role="status">載入教材中…</p><p v-else class="field-hint">顯示 {{visibleResources.length}} / {{resources.length}} 份教材</p>
<section v-for="materialGroup in materialGroups" :key="materialGroup.key"><h3 v-if="grouped" class="material-date-heading">{{materialGroup.label}}</h3><ol class="resource-list"><li v-for="item in materialGroup.items" :key="item.id" class="question-editor resource-card" :class="{'resource-expanded':expandedResources[item.id]}"><strong class="material-title"><component :is="materialFormat(item).icon" :size="22"/> {{item.title || item.fileName}} <small>{{materialFormat(item).label}}</small></strong><p v-if="item.title&&item.resourceType!=='youtube'" class="field-hint resource-original-name">原始檔名：{{item.fileName}}</p><form v-if="editable && titleEdits[item.id]" @submit.prevent="saveTitle(item)"><label>文件名稱<input :aria-label="`${item.fileName} 文件名稱`" v-model="titleEdits[item.id].title" maxlength="200" :disabled="busy" /></label><p class="field-hint">{{item.resourceType==='youtube'?'留白則顯示預設回放名稱。':'留白則顯示原始檔名。原始下載檔名不變。'}}</p><button class="button primary resource-title-save" :disabled="busy || titleEdits[item.id].title===titleEdits[item.id].original">儲存文件名稱</button><button type="button" class="button secondary" :disabled="busy" @click="delete titleEdits[item.id]">取消編輯</button></form><button v-else-if="editable" class="text-button" :disabled="busy" @click="editTitle(item)"><Pencil :size="14" />{{item.resourceType==='youtube'?'編輯回放名稱':'編輯文件名稱'}}</button><p v-if="item.resourceType!=='youtube'">{{Math.ceil(item.size/1024)}} KB · {{item.downloadCount}} 次下載</p>
<div class="resource-tags"><button type="button" v-for="tag in item.tags||[]" :key="tag" class="tag-chip" @click="chooseTag(tag)">{{tag}}</button><span v-if="!item.tags?.length" class="muted">未標記</span></div>
<form v-if="editable && tagEdits[item.id]" @submit.prevent="saveTags(item)"><label>教材標籤<input :aria-label="`${item.title||item.fileName} 標籤`" v-model="tagEdits[item.id].text" :list="`resource-tags-${course.id}`" maxlength="1000" placeholder="第1週, 案例分析" /></label><p class="field-hint">以逗號分隔；留白可清除全部標籤。</p><button class="button primary" :disabled="busy||!tagsChanged(tagEdits[item.id])">儲存標籤</button><button type="button" class="button secondary" :disabled="busy" @click="delete tagEdits[item.id]">取消標籤編輯</button></form><button v-else-if="editable" type="button" class="text-button" :disabled="busy" @click="editTags(item)"><Pencil :size="14" />編輯標籤</button>
<p class="field-hint">授課日期：{{item.teachingDate||'未指定授課日期'}}</p><form v-if="editable&&metadataEdits[item.id]" @submit.prevent="saveMetadata(item)"><label>授課日期<input :aria-label="`${item.title||item.fileName} 授課日期`" type="date" v-model="metadataEdits[item.id].date" /></label><label>教師摘要<textarea :aria-label="`${item.title||item.fileName} 教師摘要`" rows="6" maxlength="10000" v-model="metadataEdits[item.id].summary" /></label><button class="button primary" :disabled="busy||!metadataChanged(metadataEdits[item.id])">儲存日期與摘要</button><button type="button" class="button secondary" :disabled="busy" @click="delete metadataEdits[item.id]">取消日期摘要編輯</button></form><button v-else-if="editable" type="button" class="text-button" @click="editMetadata(item)">編輯日期與摘要</button><details open v-if="item.summary" class="teacher-summary"><summary><FileText :size="16" aria-hidden="true" /><span>教師摘要</span><ChevronDown :size="16" class="summary-chevron" aria-hidden="true" /></summary><p class="material-summary">{{item.summary}}</p></details>
<p class="field-hint">上傳日期：{{new Date(item.createdAt).toLocaleDateString()}}</p><p v-if="item.resourceType==='youtube'" class="submission-notice">YouTube 課程回放 · 請進入平台播放器觀看及計時。</p><p v-if="editable&&item.resourceType==='youtube'" class="field-hint">逐字稿可於回放頁新增或編輯。</p>
<FilePreview compact :active="activePreview===item.id" v-if="item.resourceType!=='youtube'" :endpoint="`${base}/resources/${item.id}/preview`" :file-name="item.title || item.fileName" :content-type="item.previewType" @expanded-change="expandedResources[item.id]=$event;if($event)activePreview=item.id" />
<RouterLink :to="`/courses/${course.id}/materials/${item.id}`" class="button secondary"><Play v-if="item.resourceType==='youtube'" :size="16" aria-hidden="true" />{{item.resourceType==='youtube'?(staff?'播放課程回放（示範）':'觀看課程回放／計時'):item.learningType?'進入教材學習／計時':'開啟教材與互動'}}</RouterLink><button v-if="item.resourceType!=='youtube' && !(course.canPractice&&!staff)" class="button secondary" :disabled="busy" @click="download(item)">下載檔案</button><button v-if="staff&&item.learningType&&!course.taRestrictions?.includes('statistics')" type="button" class="button secondary" @click="statsResource=item"><UsersRound :size="16" aria-hidden="true" />查看學生參與紀錄</button></li></ol></section>
<p v-if="!loading&&!resources.length" class="muted">尚無可使用的課程教材。</p><p v-else-if="!loading&&!visibleResources.length" class="muted">沒有符合條件的教材，請調整標籤或搜尋文字。</p></section><ResourceLearningStats v-if="section==='resources'&&statsResource" :key="statsResource.id" :course-id="course.id" :resource-id="statsResource.id" :title="statsResource.title||statsResource.fileName" @close="statsResource=null" /></template>
<template v-else>
<section class="surface form-surface"><h2>課程小組設定</h2><p class="muted">由本課程教師／助教安排，每人每課至多一個小組。教師可指定小組共同繳交 PDF 作業。</p>
<form v-if="editable" @submit.prevent="saveGroup"><label>小組名稱<input v-model="group.name" required maxlength="100" /></label><fieldset><legend>選擇本課學生</legend><label class="choice-label" v-for="m in groupCandidates" :key="m.id" style="display:block"><input type="checkbox" v-model="group.members" :disabled="!!assignedElsewhere(m.id)" :value="m.id" @change="!group.members.includes(group.leaderId)&&(group.leaderId=null)" />{{m.studentNumber}} {{m.displayName}} · {{membership.get(m.id)?.name||'未分組'}}</label><p v-if="!groupCandidates.length">請先加入課程學生。</p></fieldset><label>組長<select required v-model="group.leaderId"><option disabled :value="null">請選擇組長</option><option v-for="m in classmates.filter(m=>group.members.includes(m.id))" :key="m.id" :value="m.id">{{m.displayName}}</option></select></label><button class="button primary" :disabled="busy">{{group.id?'更新小組':'建立小組'}}</button><button type="button" class="button secondary" :disabled="busy" @click="group={name:'',members:[]}">清除編輯</button></form>
<article v-for="item in groups" :key="item.id" class="question-editor"><h3>{{item.name}}</h3><p>{{item.members.map(m=>m.displayName+(m.isLeader?'（組長）':'')).join('、') || '尚無成員'}}</p><button v-if="editable" class="button secondary" :disabled="busy" @click="group={id:item.id,name:item.name,leaderId:item.members.find(m=>m.isLeader)?.id||null,members:item.members.map(m=>m.id)}">編輯小組</button> <button v-if="editable" class="button secondary" @click="dissolve(item)">解散小組</button></article>
</section></template>
</template>

<style scoped>
.teacher-summary>summary{display:flex;align-items:center;gap:7px;list-style:none;cursor:pointer;padding:10px 0;font-weight:600}.teacher-summary>summary::-webkit-details-marker{display:none}.teacher-summary>summary::marker{content:''}.teacher-summary>summary:focus-visible{outline:2px solid var(--accent);outline-offset:3px;border-radius:4px}.summary-chevron{margin-left:auto;transition:transform .15s ease}.teacher-summary[open] .summary-chevron{transform:rotate(180deg)}

.material-summary{white-space:pre-wrap;overflow-wrap:anywhere;line-height:1.8}.material-date-heading{margin:28px 0 8px}
.resource-tags{display:flex;gap:8px;flex-wrap:wrap;margin:8px 0}.tag-chip{border:1px solid var(--border,#d9dcec);border-radius:999px;background:var(--surface,#fff);color:var(--text,#30344d);padding:6px 12px;cursor:pointer;overflow-wrap:anywhere;max-width:100%}.tag-chip[aria-pressed=true]{background:#514fc4;color:#fff;border-color:#514fc4}

.resource-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(min(100%,260px),1fr));gap:16px;margin-top:20px}
.resource-card{margin:0;min-width:0;display:flex;flex-direction:column;gap:10px}
.resource-card>strong,.resource-original-name{overflow-wrap:anywhere}.resource-title-save{background:#2563eb;color:white}.resource-card>form{min-width:0}.resource-card>form .button{margin:8px 6px 0 0}
.resource-card>.button{margin-top:auto;align-self:flex-start}
.resource-card :deep(.file-thumbnail){margin-inline:auto;max-width:100%}
.resource-card:not(.resource-expanded) :deep(.file-thumbnail img){width:100%;height:160px}
.resource-expanded{grid-column:1 / -1}
@media(max-width:600px){.resource-grid{grid-template-columns:minmax(0,1fr)}}

.section-delete{color:var(--danger);margin-left:8px}.student-section-actions{display:flex;gap:8px;align-items:center}.student-section-actions select{width:210px;max-width:100%}.student-section-actions .button{white-space:nowrap;background:#2563eb;color:white}
</style>

<style scoped>
.resource-list{list-style:none;padding:0;margin:0}.resource-list>.resource-card{display:flex;flex-flow:row wrap;align-items:center;padding:14px 4px;gap:6px 12px;border:0;border-bottom:1px solid var(--line);border-radius:0;background:transparent}
.material-title,.material-summary,.teacher-summary,.resource-card>form{flex-basis:100%}.material-title{display:flex;align-items:center;gap:8px}.material-title small{font-size:.75rem;color:var(--muted)}.resource-card p{margin:2px 0}.resource-expanded :deep(.file-preview){width:100%}
</style>
<style scoped>
.material-filters{margin:12px 0}.form-surface>details>summary{cursor:pointer;padding:8px 0;color:var(--accent);font-weight:600}
</style>
