<script setup>
import {computed,ref,reactive,nextTick,onMounted,onBeforeUnmount} from 'vue'
import {onBeforeRouteLeave,onBeforeRouteUpdate} from 'vue-router'
import {markNotificationsRead} from '../composables/notifications'
import {api} from '../api'
import {filterMaterialMetadata} from '../utils/materialMetadata'
import {groupResourcesByUnit,searchResources} from '../utils/courseResourceList'
import {materialFormat} from '../utils/materialFormat'
import ResourceLearningStats from './ResourceLearningStats.vue'
import ResourceVisual from './ResourceVisual.vue'
import FilePreview from './FilePreview.vue'
import {Pencil,Play,UsersRound} from '@lucide/vue'

const props=defineProps({course:Object,staff:Boolean,section:{type:String,default:'resources'},showClassmates:{type:Boolean,default:true},lockedUnitId:{type:String,default:''},creatorOnly:Boolean})
const emit=defineEmits(['changed'])
const loading=ref(true),resources=ref([]),groups=ref([]),classmates=ref([]),units=ref([]),error=ref(''),notice=ref(''),busy=ref(false)
const file=ref(null),group=ref({name:'',members:[]}),titleEdits=reactive({}),metadataEdits=reactive({}),statsResource=ref(null)
const uploadTitle=ref(''),sourceKind=ref('file'),youtubeUrl=ref(''),uploadTranscript=ref(''),notifyStudents=ref(false),uploadUnit=ref(''),uploadDate=ref(''),uploadSummary=ref('')
const search=ref(''),dateFrom=ref(''),dateTo=ref(''),undated=ref(false),materialSort=ref('teaching_desc')
const base=`/courses/${props.course.id}`
const editable=computed(()=>props.staff && (props.section!=='groups'||!props.course.taRestrictions?.includes('assessments')) && (props.section!=='resources'||!props.course.taRestrictions?.includes('materials')) && props.course.status!=='archived')
const rangeError=computed(()=>dateFrom.value&&dateTo.value&&dateFrom.value>dateTo.value?'授課日期起日不能晚於迄日。':'')
const visibleResources=computed(()=>filterMaterialMetadata(searchResources(props.lockedUnitId?resources.value.filter(item=>item.unitId===props.lockedUnitId):resources.value,search.value),{from:dateFrom.value,to:dateTo.value,undated:undated.value,sort:materialSort.value}))
const resourceSections=computed(()=>groupResourcesByUnit(visibleResources.value,units.value).filter(section=>section.items.length))
const membership=computed(()=>new Map(groups.value.flatMap(g=>g.members.map(m=>[m.id,g]))))
const groupCandidates=computed(()=>classmates.value)

function metadataChanged(edit){return edit.date!==edit.originalDate||edit.summary!==edit.originalSummary}
function editMetadata(item){metadataEdits[item.id]={date:item.teachingDate||'',summary:item.summary||'',originalDate:item.teachingDate||'',originalSummary:item.summary||'',expectedVersion:item.metadataVersion}}
async function saveMetadata(item){await run(async()=>{const edit=metadataEdits[item.id];try{await api(`${base}/resources/${item.id}/metadata`,{method:'PUT',body:{teachingDate:edit.date||null,summary:edit.summary,expectedVersion:edit.expectedVersion}})}catch(cause){if(cause.status===409)await load();throw cause}delete metadataEdits[item.id];notice.value='授課日期與摘要已儲存。'})}
function editTitle(item){titleEdits[item.id]={title:item.title||'',original:item.title||'',expectedVersion:item.titleVersion}}
async function saveTitle(item){await run(async()=>{const edit=titleEdits[item.id];try{await api(`${base}/resources/${item.id}/title`,{method:'PUT',body:{title:edit.title,expectedVersion:edit.expectedVersion}})}catch(cause){if(cause.status===409)await load();throw cause}delete titleEdits[item.id];notice.value='文件名稱已儲存，原始檔案保持不變。'})}
const dirtyTitles=computed(()=>Object.values(titleEdits).some(edit=>edit.title!==edit.original)||Object.values(metadataEdits).some(metadataChanged))
function titleGuard(){return !dirtyTitles.value || confirm('教材有尚未儲存的修改，確定離開？')}
onBeforeRouteLeave(titleGuard);onBeforeRouteUpdate(titleGuard)
function titleUnload(event){if(dirtyTitles.value){event.preventDefault();event.returnValue=''}}
window.addEventListener('beforeunload',titleUnload);onBeforeUnmount(()=>window.removeEventListener('beforeunload',titleUnload))

async function load(){
 try{
  if(props.section==='resources'){
   const [items,curriculum]=await Promise.all([api(`${base}/resources`),api(`${base}/curriculum`)])
   resources.value=items;units.value=curriculum?.units||[];if(props.lockedUnitId)uploadUnit.value=props.lockedUnitId
   if(!props.staff&&!props.course.canPractice){await nextTick();await markNotificationsRead('material',visibleResources.value.filter(item=>item.notifyStudents).map(item=>item.id))}
  }else [groups.value,classmates.value]=await Promise.all([api(`${base}/groups`),props.staff?api(`${base}/classmates`):[]])
 }catch(cause){error.value=cause.message}finally{loading.value=false}
}
async function run(action){busy.value=true;error.value='';notice.value='';try{await action();await load()}catch(cause){error.value=cause.message}finally{busy.value=false}}
async function upload(){
 await run(async()=>{
  if(!uploadUnit.value)throw new Error('請先選擇所屬單元。')
  if(sourceKind.value==='youtube'){
   await api(`${base}/resources/youtube`,{method:'POST',body:{title:uploadTitle.value,url:youtubeUrl.value,notifyStudents:notifyStudents.value,unitId:uploadUnit.value,transcript:uploadTranscript.value,teachingDate:uploadDate.value||null,summary:uploadSummary.value}})
   youtubeUrl.value='';uploadTranscript.value=''
  }else{
   const body=new FormData();body.append('file',file.value);body.append('notifyStudents',notifyStudents.value);body.append('title',uploadTitle.value);body.append('teachingDate',uploadDate.value);body.append('summary',uploadSummary.value);body.append('unitId',uploadUnit.value)
   await api(`${base}/resources`,{method:'POST',body});file.value=null
  }
  uploadTitle.value='';uploadDate.value='';uploadSummary.value='';notifyStudents.value=false;emit('changed');notice.value='教材已新增。'
 })
}
async function moveResource(item,unitId){if(unitId===item.unitId)return;await run(async()=>{await api(`${base}/units/activities/material/${item.id}`,{method:'PUT',body:{unitId}});emit('changed');notice.value='教材已移至指定單元。'})}
async function download(item){await run(async()=>{const blob=await api(`${base}/resources/${item.id}/download`,{method:'POST',download:true});const url=URL.createObjectURL(blob),link=document.createElement('a');link.href=url;link.download=item.fileName;link.click();setTimeout(()=>URL.revokeObjectURL(url),1000)})}
function sizeLabel(item){return item.resourceType==='youtube'?'YouTube':`${Math.max(1,Math.ceil(item.size/1024))} KB`}
function videoResource(item){return item.resourceType==='youtube'||item.learningType?.startsWith('video/')}
function assignedElsewhere(id){const owner=membership.value.get(id);return owner&&owner.id!==group.value.id}
async function dissolve(item){if(confirm('解散「'+item.name+'」？已繳交的作業會保留成員紀錄。'))await run(async()=>{await api(base+'/groups/'+item.id,{method:'DELETE'});group.value={name:'',members:[]}})}
async function saveGroup(){await run(async()=>{await api(`${base}/groups${group.value.id?'/'+group.value.id:''}`,{method:group.value.id?'PUT':'POST',body:group.value});group.value={name:'',members:[]};notice.value='小組已儲存。'})}
defineExpose({reload:load})
onMounted(load)
</script>

<template>
<p v-if="error" class="error-box" role="alert">{{error}}</p><p v-if="notice" role="status">{{notice}}</p>
<template v-if="section==='resources'">
 <section class="surface form-surface course-resources">
  <div v-if="!creatorOnly" class="section-title"><div><h2>課程資源</h2><p class="muted">教材依課程單元整齊排列；圖片顯示縮圖，其他格式使用清楚的文件圖示。</p></div></div>
  <details v-if="editable&&units.length" class="add-resource" :open="creatorOnly"><summary>新增教材</summary><form @submit.prevent="upload">
   <label>教材來源<select aria-label="教材來源" v-model="sourceKind" :disabled="busy"><option value="file">上傳檔案</option><option value="youtube">YouTube 課程回放</option></select></label>
   <label v-if="!lockedUnitId">所屬單元<select aria-label="新增教材所屬單元" v-model="uploadUnit" required><option disabled value="">請選擇單元</option><option v-for="unit in units" :key="unit.id" :value="unit.id">{{unit.name}}</option></select></label>
   <label>{{sourceKind==='youtube'?'回放名稱':'文件名稱（選填）'}}<input :aria-label="sourceKind==='youtube'?'回放名稱':'上傳文件名稱'" v-model="uploadTitle" :required="sourceKind==='youtube'" maxlength="200" placeholder="例如：案例分析講義" /></label>
   <label v-if="sourceKind==='file'">上傳檔案（最多 20 MB）<input type="file" required @change="file=$event.target.files[0]" /></label>
   <label v-else>YouTube 網址<input aria-label="YouTube 網址" type="url" v-model="youtubeUrl" required maxlength="2000" placeholder="https://www.youtube.com/watch?v=…" /></label>
   <label v-if="sourceKind==='youtube'">逐字稿（選填）<textarea aria-label="新增回放逐字稿" v-model="uploadTranscript" rows="6" maxlength="100000" /></label>
   <label>授課日期（選填）<input aria-label="新增教材授課日期" type="date" v-model="uploadDate" /></label>
   <label>教師摘要（選填）<textarea aria-label="新增教材教師摘要" v-model="uploadSummary" rows="4" maxlength="10000" placeholder="本次教學重點" /></label>
   <label><input type="checkbox" v-model="notifyStudents" />通知學生有新教材</label>
   <button type="submit" class="button primary" :disabled="busy||!uploadUnit||(sourceKind==='file'?!file:!youtubeUrl)">{{sourceKind==='youtube'?'新增回放':'上傳'}}</button>
  </form></details>
  <p v-else-if="editable&&!units.length" class="submission-notice">請先建立課程單元，再新增教材。 <RouterLink :to="{query:{section:'units'}}">前往單元管理 →</RouterLink></p>

  <div v-if="!creatorOnly"><label>搜尋課程資源<input aria-label="搜尋課程資源" v-model="search" type="search" placeholder="輸入教材名稱、原始檔名或摘要" /></label>
  <details class="material-filters"><summary>日期與排序</summary><div class="form-grid">
   <label>授課日期起日<input aria-label="授課日期起日" type="date" v-model="dateFrom" :disabled="undated" /></label>
   <label>授課日期迄日<input aria-label="授課日期迄日" type="date" v-model="dateTo" :disabled="undated" /></label>
   <label>教材排序<select aria-label="教材排序" v-model="materialSort"><option value="teaching_desc">授課日期新到舊</option><option value="teaching_asc">授課日期舊到新</option><option value="uploaded_desc">上傳日期新到舊</option></select></label>
  </div><label class="choice-label"><input type="checkbox" v-model="undated" @change="dateFrom='';dateTo=''" />只看未指定授課日期</label><p v-if="rangeError" class="error-box" role="alert">{{rangeError}}</p></details>
  <p v-if="loading" role="status">載入教材中…</p><p v-else class="field-hint">顯示 {{visibleResources.length}} / {{resources.length}} 份教材</p>

  <section v-for="resourceUnit in resourceSections" :key="resourceUnit.key" class="resource-unit-section" :class="{'legacy-unit':resourceUnit.legacy}">
   <header class="resource-unit-heading"><div><h3>{{resourceUnit.name}} <span>{{resourceUnit.items.length}} 份教材</span></h3><p v-if="resourceUnit.description">{{resourceUnit.description}}</p></div></header>
   <ol class="resource-list"><li v-for="item in resourceUnit.items" :key="item.id" class="resource-row">
    <ResourceVisual :item="item" :endpoint="`${base}/resources/${item.id}/preview`" />
    <div class="resource-info">
     <div class="resource-title-line"><strong>{{item.title||item.fileName}}</strong><span>{{materialFormat(item).label}}</span></div>
     <p v-if="item.title&&item.resourceType!=='youtube'" class="resource-original-name">{{item.fileName}}</p>
     <div class="resource-meta"><span>{{sizeLabel(item)}}</span><span v-if="item.resourceType!=='youtube'">{{item.downloadCount}} 次下載</span><span>授課 {{item.teachingDate||'未指定'}}</span><span>上傳 {{new Date(item.createdAt).toLocaleDateString()}}</span></div>
     <p v-if="item.summary" class="resource-summary">{{item.summary}}</p><p v-else class="resource-summary muted">尚無教師摘要</p>
     <details v-if="editable||item.previewType" class="resource-details"><summary>更多資訊與管理</summary>
      <FilePreview v-if="item.resourceType!=='youtube'&&item.previewType" compact active :endpoint="`${base}/resources/${item.id}/preview`" :file-name="item.title||item.fileName" :content-type="item.previewType" />
      <form v-if="editable&&titleEdits[item.id]" @submit.prevent="saveTitle(item)"><label>文件名稱<input :aria-label="`${item.fileName} 文件名稱`" v-model="titleEdits[item.id].title" maxlength="200" /></label><button class="button primary" :disabled="busy||titleEdits[item.id].title===titleEdits[item.id].original">儲存名稱</button><button type="button" class="button secondary" @click="delete titleEdits[item.id]">取消</button></form>
      <button v-else-if="editable" class="text-button" :disabled="busy" @click="editTitle(item)"><Pencil :size="14" />編輯名稱</button>
      <form v-if="editable&&metadataEdits[item.id]" @submit.prevent="saveMetadata(item)"><label>授課日期<input :aria-label="`${item.title||item.fileName} 授課日期`" type="date" v-model="metadataEdits[item.id].date" /></label><label>教師摘要<textarea :aria-label="`${item.title||item.fileName} 教師摘要`" rows="5" maxlength="10000" v-model="metadataEdits[item.id].summary" /></label><button class="button primary" :disabled="busy||!metadataChanged(metadataEdits[item.id])">儲存日期與摘要</button><button type="button" class="button secondary" @click="delete metadataEdits[item.id]">取消</button></form>
      <button v-else-if="editable" class="text-button" @click="editMetadata(item)"><Pencil :size="14" />編輯日期與摘要</button>
     </details>
    </div>
    <div class="resource-actions">
     <RouterLink :to="`/courses/${course.id}/materials/${item.id}`" class="button primary"><Play v-if="item.resourceType==='youtube'" :size="16" />{{item.resourceType==='youtube'?'觀看回放':'開啟教材'}}</RouterLink>
     <button v-if="item.resourceType!=='youtube'&&!(course.canPractice&&!staff)" class="button secondary" :disabled="busy" @click="download(item)">下載</button>
     <button v-if="staff&&!course.taRestrictions?.includes('statistics')" class="button secondary" :disabled="busy" @click="statsResource=item"><UsersRound :size="16" />{{videoResource(item)?'觀看紀錄':'點閱紀錄'}}</button>
     <label v-if="editable" class="resource-move">所屬單元<select :aria-label="`${item.title||item.fileName} 所屬單元`" :value="item.unitId||''" :disabled="busy" @change="moveResource(item,$event.target.value)"><option v-if="!item.unitId" disabled value="">未歸類</option><option v-for="unit in units" :key="unit.id" :value="unit.id">{{unit.name}}</option></select></label>
    </div>
   </li></ol>
  </section>
  <p v-if="!loading&&!resources.length" class="muted">尚無可使用的課程教材。</p><p v-else-if="!loading&&!visibleResources.length" class="muted">沒有符合條件的教材，請調整搜尋或日期條件。</p></div>
 </section>
 <ResourceLearningStats v-if="statsResource" :key="statsResource.id" :course-id="course.id" :resource-id="statsResource.id" :title="statsResource.title||statsResource.fileName" @close="statsResource=null" />
</template>

<template v-else>
 <section class="surface form-surface"><h2>課程小組設定</h2><p class="muted">由本課程教師／助教安排，每人每課至多一個小組。教師可指定小組共同繳交 PDF 作業。</p>
  <form v-if="editable" @submit.prevent="saveGroup"><label>小組名稱<input v-model="group.name" required maxlength="100" /></label><fieldset><legend>選擇本課學生</legend><label class="choice-label" v-for="member in groupCandidates" :key="member.id" style="display:block"><input type="checkbox" v-model="group.members" :disabled="!!assignedElsewhere(member.id)" :value="member.id" @change="!group.members.includes(group.leaderId)&&(group.leaderId=null)" />{{member.studentNumber}} {{member.displayName}} · {{membership.get(member.id)?.name||'未分組'}}</label><p v-if="!groupCandidates.length">請先加入課程學生。</p></fieldset><label>組長<select required v-model="group.leaderId"><option disabled :value="null">請選擇組長</option><option v-for="member in classmates.filter(member=>group.members.includes(member.id))" :key="member.id" :value="member.id">{{member.displayName}}</option></select></label><button class="button primary" :disabled="busy">{{group.id?'更新小組':'建立小組'}}</button><button type="button" class="button secondary" :disabled="busy" @click="group={name:'',members:[]}">清除編輯</button></form>
  <article v-for="item in groups" :key="item.id" class="question-editor"><h3>{{item.name}}</h3><p>{{item.members.map(member=>member.displayName+(member.isLeader?'（組長）':'')).join('、')||'尚無成員'}}</p><button v-if="editable" class="button secondary" :disabled="busy" @click="group={id:item.id,name:item.name,leaderId:item.members.find(member=>member.isLeader)?.id||null,members:item.members.map(member=>member.id)}">編輯小組</button> <button v-if="editable" class="button secondary" @click="dissolve(item)">解散小組</button></article>
 </section>
</template>
</template>

<style scoped>
.course-resources{min-width:0}.add-resource{margin:14px 0}.material-filters{margin:12px 0}.resource-unit-section{margin-top:22px;border-top:1px solid var(--line);padding-top:14px}.resource-unit-section.legacy-unit{border:1px solid color-mix(in srgb,var(--warning,#c27a00) 35%,var(--line));border-radius:10px;padding:14px}.resource-unit-heading{display:flex;justify-content:space-between;gap:12px;margin-bottom:10px}.resource-unit-heading h3{margin:0;display:flex;align-items:center;gap:9px}.resource-unit-heading h3 span{font-size:12px;font-weight:600;color:var(--muted)}.resource-unit-heading p{margin:5px 0 0;color:var(--muted)}.resource-list{list-style:none;padding:0;margin:0;display:grid;gap:8px}.resource-row{display:grid;grid-template-columns:88px minmax(0,1fr) minmax(150px,auto);gap:14px;align-items:start;padding:12px;border:1px solid var(--line);border-radius:10px;background:var(--surface);min-width:0}.resource-info{min-width:0}.resource-title-line{display:flex;gap:8px;align-items:center;min-width:0}.resource-title-line strong{font-size:16px;overflow-wrap:anywhere}.resource-title-line span{flex:none;padding:2px 7px;border-radius:999px;background:var(--accent-soft);color:var(--accent);font-size:11px;font-weight:700}.resource-original-name{margin:3px 0;color:var(--muted);font-size:12px;overflow-wrap:anywhere}.resource-meta{display:flex;gap:6px 14px;flex-wrap:wrap;margin-top:7px;color:var(--muted);font-size:12px}.resource-summary{display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;margin:8px 0 0;line-height:1.45;overflow-wrap:anywhere}.resource-details{margin-top:8px}.resource-details>summary{cursor:pointer;color:var(--accent);font-size:13px}.resource-actions{display:flex;flex-direction:column;align-items:stretch;gap:7px;min-width:150px}.resource-actions .button{justify-content:center;margin:0}.resource-move{font-size:12px}.resource-move select{margin-top:3px;min-width:0;width:100%}
@media(max-width:760px){.resource-row{grid-template-columns:72px minmax(0,1fr);gap:10px}.resource-row :deep(.resource-visual){width:72px;height:58px;flex-basis:72px}.resource-actions{grid-column:1/-1;display:flex;flex-direction:row;flex-wrap:wrap;min-width:0}.resource-actions .button{flex:1 1 130px}.resource-move{flex:1 1 100%}.resource-title-line{align-items:flex-start}.resource-meta{gap:4px 10px}}
</style>
