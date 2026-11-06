<script setup>
import {computed,ref,reactive,onMounted,onBeforeUnmount} from 'vue'
import {api} from '../api'
import FilePreview from './FilePreview.vue'
import {Pencil} from '@lucide/vue'
import {onBeforeRouteLeave,onBeforeRouteUpdate} from 'vue-router'
const props=defineProps({course:Object,staff:Boolean,section:{type:String,default:'resources'},showClassmates:{type:Boolean,default:true}})
const emit=defineEmits(['changed'])
const resources=ref([]),groups=ref([]),classmates=ref([]),error=ref(''),notice=ref(''),busy=ref(false),file=ref(null),group=ref({name:'',members:[]})
const expandedResources=reactive({}),titleEdits=reactive({}),uploadTitle=ref('')
const base=`/courses/${props.course.id}`
const editable=computed(()=>props.staff && props.course.status!=='archived')
const groupCandidates=computed(()=>classmates.value)
async function load(){try{if(props.section==='resources')resources.value=await api(`${base}/resources`);else [groups.value,classmates.value]=await Promise.all([api(`${base}/groups`),api(`${base}/classmates`)])}catch(e){error.value=e.message}}
async function run(action){busy.value=true;error.value='';notice.value='';try{await action();await load()}catch(e){error.value=e.message}finally{busy.value=false}}
async function upload(){await run(async()=>{const body=new FormData();body.append('file',file.value);body.append('title',uploadTitle.value);await api(`${base}/resources`,{method:'POST',body});file.value=null;uploadTitle.value='';emit('changed');notice.value='資源已上傳。'})}
function editTitle(item){titleEdits[item.id]={title:item.title||'',original:item.title||'',expectedVersion:item.titleVersion}}
async function saveTitle(item){await run(async()=>{const edit=titleEdits[item.id];try{await api(`${base}/resources/${item.id}/title`,{method:'PUT',body:{title:edit.title,expectedVersion:edit.expectedVersion}})}catch(e){if(e.status===409)await load();throw e}delete titleEdits[item.id];notice.value='文件名稱已儲存，原始檔案保持不變。'})}
const dirtyTitles=computed(()=>Object.values(titleEdits).some(e=>e.title!==e.original))
function titleGuard(){return !dirtyTitles.value || confirm('文件名稱有尚未儲存的修改，確定離開？')}
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
<section v-if="section==='resources'" class="surface form-surface">
<h2>教學資源／講義</h2>
<form v-if="editable" @submit.prevent="upload">
<label>文件名稱（選填）<input aria-label="上傳文件名稱" v-model="uploadTitle" maxlength="200" placeholder="例如：第一週案例分析講義" /></label><label>上傳檔案（最多 20 MB）<input type="file" required @change="file=$event.target.files[0]" /></label>

<button class="button primary" :disabled="busy || !file">上傳</button></form>
<div class="resource-grid"><article v-for="item in resources" :key="item.id" class="question-editor resource-card" :class="{'resource-expanded':expandedResources[item.id]}"><strong>{{item.title || item.fileName}}</strong><p v-if="item.title" class="field-hint resource-original-name">原始檔名：{{item.fileName}}</p><form v-if="editable && titleEdits[item.id]" @submit.prevent="saveTitle(item)"><label>文件名稱<input :aria-label="`${item.fileName} 文件名稱`" v-model="titleEdits[item.id].title" maxlength="200" :disabled="busy" /></label><p class="field-hint">留白則顯示原始檔名。原始下載檔名不變。</p><button class="button primary resource-title-save" :disabled="busy || titleEdits[item.id].title===titleEdits[item.id].original">儲存文件名稱</button><button type="button" class="button secondary" :disabled="busy" @click="delete titleEdits[item.id]">取消編輯</button></form><button v-else-if="editable" class="text-button" :disabled="busy" @click="editTitle(item)"><Pencil :size="14" />編輯文件名稱</button><p>{{Math.ceil(item.size/1024)}} KB · {{item.downloadCount}} 次下載</p>
<FilePreview :endpoint="`${base}/resources/${item.id}/preview`" :file-name="item.title || item.fileName" :content-type="item.previewType" @expanded-change="expandedResources[item.id]=$event" />
<RouterLink v-if="item.learningType" :to="`/courses/${course.id}/materials/${item.id}`" class="button secondary">進入教材學習／計時</RouterLink><button class="button secondary" :disabled="busy" @click="download(item)">下載檔案</button></article></div>
<p v-if="!resources.length" class="muted">尚無可使用的課程資源。</p></section>
<template v-else>
<section class="surface form-surface"><h2>課程學生名單</h2><div class="table-scroll"><table class="data-table"><thead><tr><th>學號</th><th>姓名</th></tr></thead><tbody><tr v-for="m in classmates" :key="m.id"><td>{{m.studentNumber || '未填學號'}}</td><td>{{m.displayName}}</td></tr></tbody></table></div><p v-if="!classmates.length" class="muted">目前沒有學生。</p></section>
<section class="surface form-surface"><h2>課程小組設定</h2><p class="muted">由本課程教師／助教安排，每人每課至多一個小組；測驗與 PDF 作業仍個別提交。</p>
<form v-if="editable" @submit.prevent="saveGroup"><label>小組名稱<input v-model="group.name" required maxlength="100" /></label><fieldset><legend>選擇本課學生</legend><label class="choice-label" v-for="m in groupCandidates" :key="m.id" style="display:block"><input type="checkbox" v-model="group.members" :value="m.id" />{{m.studentNumber}} {{m.displayName}}</label><p v-if="!groupCandidates.length">請先加入課程學生。</p></fieldset><button class="button primary" :disabled="busy">{{group.id?'更新小組':'建立小組'}}</button><button type="button" class="button secondary" :disabled="busy" @click="group={name:'',members:[]}">清除編輯</button></form>
<article v-for="item in groups" :key="item.id" class="question-editor"><h3>{{item.name}}</h3><p>{{item.members.map(m=>m.displayName).join('、') || '尚無成員'}}</p><button v-if="editable" class="button secondary" :disabled="busy" @click="group={id:item.id,name:item.name,members:item.members.map(m=>m.id)}">編輯小組</button></article>
</section></template>
</template>

<style scoped>
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
