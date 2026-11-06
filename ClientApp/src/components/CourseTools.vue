<script setup>
import {computed,ref,reactive,onMounted,onBeforeUnmount} from 'vue'
import {api} from '../api'
import {parseResourceTags,resourceTagChoices,filterResources} from '../utils/resourceTags'
import ResourceLearningStats from './ResourceLearningStats.vue'
import FilePreview from './FilePreview.vue'
import {Pencil} from '@lucide/vue'
import {onBeforeRouteLeave,onBeforeRouteUpdate} from 'vue-router'
const props=defineProps({course:Object,staff:Boolean,section:{type:String,default:'resources'},showClassmates:{type:Boolean,default:true}})
const emit=defineEmits(['changed'])
const resources=ref([]),groups=ref([]),classmates=ref([]),error=ref(''),notice=ref(''),busy=ref(false),file=ref(null),group=ref({name:'',members:[]})
const expandedResources=reactive({}),titleEdits=reactive({}),uploadTitle=ref(''),uploadTags=ref(''),sourceKind=ref('file'),youtubeUrl=ref(''),uploadTranscript=ref(''),tagEdits=reactive({}),selectedTag=ref(''),search=ref(''),onlyUntagged=ref(false),statsResource=ref(null)
const base=`/courses/${props.course.id}`
const editable=computed(()=>props.staff && props.course.status!=='archived')
const tagChoices=computed(()=>resourceTagChoices(resources.value))
const tagSuggestions=computed(()=>[...new Set([...Array.from({length:18},(_,i)=>`第${i+1}週`),...tagChoices.value.map(t=>t.name)])])
const visibleResources=computed(()=>filterResources(resources.value,{tag:selectedTag.value,query:search.value,untagged:onlyUntagged.value}))
function chooseTag(tag){selectedTag.value=tag;onlyUntagged.value=false}
function tagsChanged(edit){try{return JSON.stringify(parseResourceTags(edit.text))!==JSON.stringify(edit.original)}catch{return true}}
function editTags(item){tagEdits[item.id]={text:(item.tags||[]).join(', '),original:[...(item.tags||[])],expectedVersion:item.tagsVersion}}
async function saveTags(item){await run(async()=>{const edit=tagEdits[item.id];try{await api(`${base}/resources/${item.id}/tags`,{method:'PUT',body:{tags:parseResourceTags(edit.text),expectedVersion:edit.expectedVersion}})}catch(e){if(e.status===409)await load();throw e}delete tagEdits[item.id];notice.value='教材標籤已儲存。'})}
const groupCandidates=computed(()=>classmates.value)
async function load(){try{if(props.section==='resources')resources.value=await api(`${base}/resources`);else [groups.value,classmates.value]=await Promise.all([api(`${base}/groups`),api(`${base}/classmates`)])}catch(e){error.value=e.message}}
async function run(action){busy.value=true;error.value='';notice.value='';try{await action();await load()}catch(e){error.value=e.message}finally{busy.value=false}}
async function upload(){await run(async()=>{const tags=parseResourceTags(uploadTags.value);if(sourceKind.value==='youtube'){await api(`${base}/resources/youtube`,{method:'POST',body:{title:uploadTitle.value,url:youtubeUrl.value,tags,transcript:uploadTranscript.value}});youtubeUrl.value='';uploadTranscript.value=''}else{const body=new FormData();body.append('file',file.value);body.append('title',uploadTitle.value);for(const tag of tags)body.append('tags',tag);await api(`${base}/resources`,{method:'POST',body});file.value=null}uploadTitle.value='';uploadTags.value='';emit('changed');notice.value='教材已新增。'})}

function editTitle(item){titleEdits[item.id]={title:item.title||'',original:item.title||'',expectedVersion:item.titleVersion}}
async function saveTitle(item){await run(async()=>{const edit=titleEdits[item.id];try{await api(`${base}/resources/${item.id}/title`,{method:'PUT',body:{title:edit.title,expectedVersion:edit.expectedVersion}})}catch(e){if(e.status===409)await load();throw e}delete titleEdits[item.id];notice.value='文件名稱已儲存，原始檔案保持不變。'})}
const dirtyTitles=computed(()=>Object.values(titleEdits).some(e=>e.title!==e.original)||Object.values(tagEdits).some(tagsChanged))
function titleGuard(){return !dirtyTitles.value || confirm('文件名稱或標籤有尚未儲存的修改，確定離開？')}
onBeforeRouteLeave(titleGuard);onBeforeRouteUpdate(titleGuard)
function titleUnload(event){if(dirtyTitles.value){event.preventDefault();event.returnValue=''}}
window.addEventListener('beforeunload',titleUnload);onBeforeUnmount(()=>window.removeEventListener('beforeunload',titleUnload))
async function download(item){await run(async()=>{const blob=await api(`${base}/resources/${item.id}/download`,{method:'POST',download:true});const url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=item.fileName;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000)})}
async function saveGroup(){await run(async()=>{await api(`${base}/groups${group.value.id?'/'+group.value.id:''}`,{method:group.value.id?'PUT':'POST',body:group.value});group.value={name:'',members:[]};notice.value='小組已儲存。'})}
defineExpose({reload:load})
onMounted(load)
</script>
<template>
<p v-if="error" class="error-box" role="alert">{{error}}</p><p v-if="notice" role="status">{{notice}}</p>
<template v-if="section==='resources'"><section class="surface form-surface">
<h2>教學資源／講義</h2>
<form v-if="editable" @submit.prevent="upload">
<label>教材來源<select aria-label="教材來源" v-model="sourceKind" :disabled="busy"><option value="file">上傳檔案</option><option value="youtube">YouTube 課程回放</option></select></label>
<label>{{sourceKind==='youtube'?'回放名稱':'文件名稱（選填）'}}<input :aria-label="sourceKind==='youtube'?'回放名稱':'上傳文件名稱'" v-model="uploadTitle" :required="sourceKind==='youtube'" maxlength="200" placeholder="例如：第一週案例分析" /></label>
<label v-if="sourceKind==='file'">上傳檔案（最多 20 MB）<input type="file" required @change="file=$event.target.files[0]" /></label>
<label v-else>YouTube 網址<input aria-label="YouTube 網址" type="url" v-model="youtubeUrl" required maxlength="2000" placeholder="https://www.youtube.com/watch?v=…" /></label>
<label v-if="sourceKind==='youtube'">逐字稿（選填）<textarea aria-label="新增回放逐字稿" v-model="uploadTranscript" rows="6" maxlength="100000" placeholder="貼上課程逐字稿，可保留時間標記" /></label>
<label>教材標籤<input aria-label="新增教材標籤" v-model="uploadTags" :list="`resource-tags-${course.id}`" placeholder="第1週, 案例分析（以逗號分隔）" maxlength="1000" /></label><p class="field-hint">可自訂週次或主題；每份最多 10 個標籤、每個最多 30 字。YouTube 網址需允許嵌入，影片變更請新增另一份教材。</p>
<button class="button primary" :disabled="busy || (sourceKind==='file'?!file:!youtubeUrl)">{{sourceKind==='youtube'?'新增回放':'上傳'}}</button></form>
<datalist :id="`resource-tags-${course.id}`"><option v-for="tag in tagSuggestions" :key="tag" :value="tag" /></datalist>
<label>搜尋教材名稱或標籤<input aria-label="搜尋教材名稱或標籤" v-model="search" type="search" placeholder="輸入檔名、名稱或週次" /></label>
<div class="resource-tags" aria-label="教材標籤篩選"><button type="button" class="tag-chip" :aria-pressed="!selectedTag&&!onlyUntagged" @click="chooseTag('')">全部（{{resources.length}}）</button><button type="button" class="tag-chip" :aria-pressed="onlyUntagged" @click="selectedTag='';onlyUntagged=true">未標記</button><button v-for="tag in tagChoices" :key="tag.name" type="button" class="tag-chip" :aria-pressed="selectedTag===tag.name" @click="chooseTag(tag.name)">{{tag.name}}（{{tag.count}}）</button></div>
<p class="field-hint">顯示 {{visibleResources.length}} / {{resources.length}} 份教材 · 週次由授課者標記，清單依新增時間由新到舊。</p>
<div class="resource-grid"><article v-for="item in visibleResources" :key="item.id" class="question-editor resource-card" :class="{'resource-expanded':expandedResources[item.id]}"><strong>{{item.title || item.fileName}}</strong><p v-if="item.title&&item.resourceType!=='youtube'" class="field-hint resource-original-name">原始檔名：{{item.fileName}}</p><form v-if="editable && titleEdits[item.id]" @submit.prevent="saveTitle(item)"><label>文件名稱<input :aria-label="`${item.fileName} 文件名稱`" v-model="titleEdits[item.id].title" maxlength="200" :disabled="busy" /></label><p class="field-hint">{{item.resourceType==='youtube'?'留白則顯示預設回放名稱。':'留白則顯示原始檔名。原始下載檔名不變。'}}</p><button class="button primary resource-title-save" :disabled="busy || titleEdits[item.id].title===titleEdits[item.id].original">儲存文件名稱</button><button type="button" class="button secondary" :disabled="busy" @click="delete titleEdits[item.id]">取消編輯</button></form><button v-else-if="editable" class="text-button" :disabled="busy" @click="editTitle(item)"><Pencil :size="14" />{{item.resourceType==='youtube'?'編輯回放名稱':'編輯文件名稱'}}</button><p v-if="item.resourceType!=='youtube'">{{Math.ceil(item.size/1024)}} KB · {{item.downloadCount}} 次下載</p>
<div class="resource-tags"><button type="button" v-for="tag in item.tags||[]" :key="tag" class="tag-chip" @click="chooseTag(tag)">{{tag}}</button><span v-if="!item.tags?.length" class="muted">未標記</span></div>
<form v-if="editable && tagEdits[item.id]" @submit.prevent="saveTags(item)"><label>教材標籤<input :aria-label="`${item.title||item.fileName} 標籤`" v-model="tagEdits[item.id].text" :list="`resource-tags-${course.id}`" maxlength="1000" placeholder="第1週, 案例分析" /></label><p class="field-hint">以逗號分隔；留白可清除全部標籤。</p><button class="button primary" :disabled="busy||!tagsChanged(tagEdits[item.id])">儲存標籤</button><button type="button" class="button secondary" :disabled="busy" @click="delete tagEdits[item.id]">取消標籤編輯</button></form><button v-else-if="editable" type="button" class="text-button" :disabled="busy" @click="editTags(item)"><Pencil :size="14" />編輯標籤</button>
<p class="field-hint">新增日期：{{new Date(item.createdAt).toLocaleDateString()}}</p><p v-if="item.resourceType==='youtube'" class="submission-notice">YouTube 課程回放 · 請進入平台播放器觀看及計時。</p><p v-if="editable&&item.resourceType==='youtube'" class="field-hint">逐字稿可於回放頁新增或編輯。</p>
<FilePreview v-if="item.resourceType!=='youtube'" :endpoint="`${base}/resources/${item.id}/preview`" :file-name="item.title || item.fileName" :content-type="item.previewType" @expanded-change="expandedResources[item.id]=$event" />
<RouterLink v-if="item.learningType" :to="`/courses/${course.id}/materials/${item.id}`" class="button secondary">{{item.resourceType==='youtube'?'觀看課程回放／計時':'進入教材學習／計時'}}</RouterLink><button v-if="item.resourceType!=='youtube'" class="button secondary" :disabled="busy" @click="download(item)">下載檔案</button><button v-if="staff&&item.learningType" type="button" class="button secondary" @click="statsResource=item">查看學習時間</button></article></div>
<p v-if="!resources.length" class="muted">尚無可使用的課程資源。</p><p v-else-if="!visibleResources.length" class="muted">沒有符合條件的教材，請調整標籤或搜尋文字。</p></section><ResourceLearningStats v-if="section==='resources'&&statsResource" :key="statsResource.id" :course-id="course.id" :resource-id="statsResource.id" :title="statsResource.title||statsResource.fileName" @close="statsResource=null" /></template>
<template v-else>
<section class="surface form-surface"><h2>課程學生名單</h2><div class="table-scroll"><table class="data-table"><thead><tr><th>學號</th><th>姓名</th></tr></thead><tbody><tr v-for="m in classmates" :key="m.id"><td>{{m.studentNumber || '未填學號'}}</td><td>{{m.displayName}}</td></tr></tbody></table></div><p v-if="!classmates.length" class="muted">目前沒有學生。</p></section>
<section class="surface form-surface"><h2>課程小組設定</h2><p class="muted">由本課程教師／助教安排，每人每課至多一個小組；測驗與 PDF 作業仍個別提交。</p>
<form v-if="editable" @submit.prevent="saveGroup"><label>小組名稱<input v-model="group.name" required maxlength="100" /></label><fieldset><legend>選擇本課學生</legend><label class="choice-label" v-for="m in groupCandidates" :key="m.id" style="display:block"><input type="checkbox" v-model="group.members" :value="m.id" />{{m.studentNumber}} {{m.displayName}}</label><p v-if="!groupCandidates.length">請先加入課程學生。</p></fieldset><button class="button primary" :disabled="busy">{{group.id?'更新小組':'建立小組'}}</button><button type="button" class="button secondary" :disabled="busy" @click="group={name:'',members:[]}">清除編輯</button></form>
<article v-for="item in groups" :key="item.id" class="question-editor"><h3>{{item.name}}</h3><p>{{item.members.map(m=>m.displayName).join('、') || '尚無成員'}}</p><button v-if="editable" class="button secondary" :disabled="busy" @click="group={id:item.id,name:item.name,members:item.members.map(m=>m.id)}">編輯小組</button></article>
</section></template>
</template>

<style scoped>
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
