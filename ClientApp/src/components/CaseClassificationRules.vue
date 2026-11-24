<script setup>
import {computed,nextTick,onBeforeUnmount,ref,useId,watch} from 'vue'
import {X} from '@lucide/vue'
import {api} from '../api'
import {auth} from '../composables/auth'
import {experience} from '../composables/experience'
const props=defineProps({course:{type:Object,required:true},config:{type:Object,required:true}})
const emit=defineEmits(['saved'])
const dialog=ref(null),opener=ref(null),closeButton=ref(null),titleId=useId()
const editingCode=ref(null),adding=ref(false)
const opened=ref(false),loading=ref(false),saving=ref(false),loaded=ref(false),error=ref(''),notice=ref(''),version=ref(null),definitions=ref([]),savedCodes=ref(new Set()),updatedAt=ref(null),updatedBy=ref(null),newCode=ref(''),newLabel=ref(''),newPhase=ref('A')
const authorized=computed(()=>props.course.courseRole==='teacher'&&props.config.canMaintainRules===true&&!experience.student)
const phases=[{key:'A',label:'病前功能'},{key:'B',label:'疾病病程'},{key:'C',label:'功能評估結果'},{key:'D',label:'心理社會條件'}]
let generation=0,controller,destroyed=false,savedData=null
function normalize(item){return {...item,keywordsText:(item.keywords||[]).join('\n')}}
function accept(data){savedData=data;version.value=data.version;definitions.value=(data.definitions||[]).map(normalize);savedCodes.value=new Set(definitions.value.map(item=>item.code));updatedAt.value=data.updatedAt;updatedBy.value=data.updatedBy;loaded.value=true}
const currentItem=computed(()=>definitions.value.find(item=>item.code===editingCode.value))
function resetItem(){editingCode.value=null;adding.value=false;newCode.value='';newLabel.value='';newPhase.value='A'}
async function editItem(code){
 if(saving.value||loading.value)return
 editingCode.value=code;error.value='';notice.value=''
 await nextTick();dialog.value?.querySelector('.rule-definition input:not([type="checkbox"])')?.focus()
}
async function newItem(){
 adding.value=true;error.value='';notice.value=''
 await nextTick();dialog.value?.querySelector('.new-rule select')?.focus()
}
async function finishItem(discard=true){
 if(discard&&saving.value)return
 const previous=editingCode.value
 if(discard&&savedData){accept(savedData);error.value='';notice.value=''}
 resetItem()
 await nextTick()
 if(!opened.value||editingCode.value||adding.value)return
 const target=[...(dialog.value?.querySelectorAll('[data-edit-rule]')||[])].find(el=>el.dataset.editRule===previous)
 ;(target||dialog.value?.querySelector('[data-action="new-rule"]'))?.focus({preventScroll:true})
}
async function load(){
 if(!authorized.value||loading.value||saving.value)return
 const g=++generation;controller?.abort();controller=new AbortController();loading.value=true;error.value='';notice.value=''
 try{const data=await api(`/courses/${props.course.id}/case-analysis/rules`,{signal:controller.signal});if(!destroyed&&g===generation&&authorized.value){accept(data);if(editingCode.value||adding.value)void finishItem(false)}}catch(e){if(!destroyed&&g===generation&&e.name!=='AbortError')error.value=e.message||'分類規則載入失敗。'}finally{if(!destroyed&&g===generation)loading.value=false}
}
async function openEditor(){
 if(!authorized.value||opened.value)return
 opened.value=true
 await nextTick()
 if(!authorized.value||!opened.value||!dialog.value)return
 if(typeof dialog.value.showModal==='function')dialog.value.showModal();else dialog.value.setAttribute('open','')
 closeButton.value?.focus()
 if(!loaded.value)void load();else controller=new AbortController()
}
function hideDialog(){
 if(dialog.value?.open){if(typeof dialog.value.close==='function')dialog.value.close();else dialog.value.removeAttribute('open')}
 if(opener.value?.isConnected)opener.value.focus({preventScroll:true})
}
function closeEditor(){
 if(saving.value||!opened.value)return
 opened.value=false;generation++;controller?.abort();loading.value=false
 if(savedData)accept(savedData)
 resetItem();error.value='';notice.value=''
 hideDialog()
}
function keepFocus(event){
 if(event.key!=='Tab')return
 const controls=[...dialog.value.querySelectorAll('button,input,textarea,select,summary,[tabindex="0"]')].filter(el=>!el.matches(':disabled')&&el.tabIndex>=0&&el.getClientRects().length)
 const first=controls[0],last=controls.at(-1)
 if(event.shiftKey&&document.activeElement===first){event.preventDefault();last?.focus()}
 else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first?.focus()}
}
const control=/[\u0000-\u001f\u007f]/
function payloadDefinitions(){return definitions.value.map(({keywordsText,...item})=>({...item,label:item.label.trim(),description:(item.description||'').trim(),keywords:keywordsText.split(/\r?\n|[,，]/).map(value=>value.trim()).filter(Boolean)}))}
const validation=computed(()=>{
 if(!loaded.value)return ''
 if(!definitions.value.length||definitions.value.length>128)return '規則數量需介於 1 至 128。'
 for(const item of payloadDefinitions()){
  if(!item.label||item.label.length>100||control.test(item.label))return `${item.code} 分類名稱需為 1 至 100 字。`
  if(item.description.length>500)return `${item.code} 說明不可超過 500 字。`
  if(item.keywords.length>20||item.keywords.some(word=>word.length>50||control.test(word))||new Set(item.keywords).size!==item.keywords.length)return `${item.code} 關鍵詞最多 20 個、每個最多 50 字且不可重複。`
 }
 if(!definitions.value.some(item=>item.enabled&&!['A2c','B2c'].includes(item.code)&&(!/[a-z]$/.test(item.code)||definitions.value.find(parent=>parent.code===item.code.slice(0,-1))?.enabled)))return '至少需有一個啟用分類。'
 if(new TextEncoder().encode(JSON.stringify(payloadDefinitions())).length>16000)return '完整規則超過容量，請縮短名稱、說明或關鍵詞。'
 return ''
})
const invalid=computed(()=>!loaded.value||Boolean(validation.value))
function add(){
 const code=newCode.value.trim(),label=newLabel.value.trim()
 if(!code||!label)return
 if(!new RegExp(`^${newPhase.value}[1-9][0-9]{0,2}[a-z]?$`).test(code)){error.value='新增編碼需符合所選階段，例如 A1 或 A1a。';return}
 if(definitions.value.some(item=>item.code.toLowerCase()===code.toLowerCase())){error.value='此編碼已存在，請直接修改既有規則。';return}
 if(/[a-z]$/.test(code)&&!definitions.value.some(item=>item.code===code.slice(0,-1))){error.value='請先建立對應的父分類，再新增子分類。';return}
 if(label.length>100||control.test(label)){error.value='分類名稱需為 1 至 100 字且不可包含控制字元。';return}
 if(definitions.value.length>=128){error.value='規則數量最多 128。';return}
 definitions.value.push(normalize({code,label,phase:newPhase.value,description:'',keywords:[],enabled:!['A2c','B2c'].includes(code)}));newCode.value='';newLabel.value='';adding.value=false;void editItem(code);notice.value='新增分類尚未儲存，請完成此項設定後儲存。'
}
function remove(code){if(savedCodes.value.has(code))return;void finishItem()}
async function save(){
 if(!authorized.value||!currentItem.value||saving.value||loading.value||invalid.value)return
 const g=generation;saving.value=true;error.value='';notice.value=''
 const nextDefinitions=payloadDefinitions()
 try{const data=await api(`/courses/${props.course.id}/case-analysis/rules`,{method:'PUT',signal:controller.signal,body:{expectedVersion:version.value,definitions:nextDefinitions}});if(!destroyed&&g===generation&&authorized.value){accept(data);notice.value='分類規則已儲存；後續 AI 分析使用新版本，既有分析與正式提交保留原快照。';void finishItem(false);emit('saved',data)}}catch(e){if(!destroyed&&g===generation&&e.name!=='AbortError')error.value=e.message||'儲存失敗，請重試。'}finally{if(!destroyed&&g===generation)saving.value=false}
}
watch([()=>props.course.id,()=>props.course.courseRole,()=>props.config.canMaintainRules,()=>experience.student,()=>auth.user?.id],()=>{generation++;controller?.abort();opened.value=false;hideDialog();loaded.value=false;loading.value=false;saving.value=false;savedData=null;definitions.value=[];resetItem();error.value='';notice.value=''},{flush:'sync'})
onBeforeUnmount(()=>{destroyed=true;generation++;controller?.abort();hideDialog()})
</script>

<template>
 <div v-if="authorized" class="rules-entry" data-case-rules-editor>
  <button ref="opener" type="button" class="button primary" data-action="edit-rules" aria-haspopup="dialog" :aria-expanded="opened" @click="openEditor">編輯分類說明</button>
  <dialog ref="dialog" class="case-rules-editor" :aria-labelledby="titleId" aria-modal="true" @cancel.prevent="closeEditor" @close="()=>!dialog?.open&&closeEditor()" @keydown="keepFocus">
  <header class="rules-header"><h2 :id="titleId">編輯分類說明</h2><button ref="closeButton" type="button" class="button secondary" data-action="close-rules" aria-label="關閉分類說明編輯" :disabled="saving" @click="closeEditor"><X :size="20" aria-hidden="true"/></button></header>
  <div class="rules-body">
  <p class="field-hint">修改分類名稱、說明、關鍵詞與啟用狀態，儲存為新版本並用於後續 AI 分析。歷史分析與正式提交保留原規則。關鍵詞每行一個。既有分類可停用；停用父分類時，子分類也暫停使用，但保留各自設定。</p>
  <p v-if="loading" role="status">載入分類規則中…</p><p v-if="error" role="alert" class="error-text">{{error}}</p><p v-if="notice" role="status">{{notice}}</p>
  <template v-if="loaded">
   <p class="muted">規則版本：{{version}}<span v-if="updatedAt"> · {{new Date(updatedAt).toLocaleString('zh-TW')}}</span><span v-if="updatedBy"> · 修改者 {{updatedBy}}</span></p>
   <fieldset :disabled="saving||loading">
    <div v-if="!editingCode&&!adding" class="rules-list">
     <button type="button" class="button primary" data-action="new-rule" @click="newItem">新增分類</button>
     <section v-for="phase in phases" :key="phase.key" class="rules-phase"><h3>{{phase.key}} {{phase.label}}（{{definitions.filter(item=>item.phase===phase.key).length}}）</h3>
      <div v-for="item in definitions.filter(item=>item.phase===phase.key)" :key="item.code" class="rule-summary"><strong>{{item.code}}</strong><span class="rule-title">{{item.label}}<small v-if="!item.enabled" class="muted"> · 已停用</small></span><button type="button" class="button secondary" :data-edit-rule="item.code" :aria-label="`編輯 ${item.code} ${item.label}`" @click="editItem(item.code)">編輯</button></div>
     </section>
    </div>
     <div v-if="currentItem" class="rule-definition" :data-rule-code="currentItem.code">
      <template v-for="item in [currentItem]" :key="item.code">
      <strong>{{item.code}}</strong><label>分類名稱<input v-model="item.label" maxlength="100"></label><label>分類說明<textarea v-model="item.description" rows="2" maxlength="500"/></label><label>關鍵詞<textarea v-model="item.keywordsText" rows="2" maxlength="4000"/></label><label class="rule-enabled"><input v-model="item.enabled" type="checkbox" :disabled="['A2c','B2c'].includes(item.code)">啟用此分類<span v-if="['A2c','B2c'].includes(item.code)">（此分類固定停用）</span></label><button v-if="!savedCodes.has(item.code)" type="button" class="button secondary" data-action="remove-rule" @click="remove(item.code)">移除未儲存分類</button>
      </template>
     </div>
    <div v-if="adding" class="new-rule"><strong>新增分類或子分類</strong><label>階段<select v-model="newPhase"><option v-for="phase in phases" :key="phase.key" :value="phase.key">{{phase.key}} {{phase.label}}</option></select></label><label>編碼<input v-model="newCode" placeholder="例如 A1a" maxlength="20"></label><label>名稱<input v-model="newLabel" maxlength="100"></label><button type="button" class="button secondary" data-action="add-rule" :disabled="!newCode.trim()||!newLabel.trim()" @click="add">繼續編輯分類</button></div>
   </fieldset>
   <p v-if="validation" class="error-text" role="status">{{validation}}</p>
  </template>
  <button type="button" class="button secondary" data-action="reload-rules" :disabled="loading||saving" @click="load">重新載入規則（捨棄未儲存修改）</button>
  </div>
  <footer class="rules-footer"><span v-if="editingCode||adding" class="field-hint">返回清單或關閉會捨棄此項未儲存的修改。</span><button v-if="editingCode||adding" type="button" class="button secondary" data-action="back-rules" :disabled="saving||loading" @click="finishItem()">取消並返回清單</button><button type="button" class="button secondary" data-action="cancel-rules" :disabled="saving" @click="closeEditor">{{saving?'儲存中…':'關閉'}}</button><button v-if="currentItem" type="button" class="button primary" data-action="save-rules" :disabled="saving||loading||invalid" @click="save">{{saving?'儲存中…':'儲存此分類'}}</button></footer>
 </dialog>
 </div>
</template>

<style scoped>
.rules-entry{margin:18px 0;min-width:0}.case-rules-editor{border:1px solid var(--line);border-radius:12px;padding:0;width:min(1000px,calc(100% - 32px));max-width:calc(100% - 32px);max-height:calc(100dvh - 32px);box-sizing:border-box;color:var(--text);background:var(--surface);overflow:hidden}.case-rules-editor[open]{display:flex;flex-direction:column}.case-rules-editor::backdrop{background:#0f172a99}.rules-header,.rules-footer{display:flex;align-items:center;gap:12px;flex-shrink:0;padding:16px 20px}.rules-header{justify-content:space-between;border-bottom:1px solid var(--line)}.rules-header h2{font-size:1.2rem;margin:0}.rules-header button{display:inline-flex;align-items:center;justify-content:center;min-width:44px;min-height:44px;padding:8px}.rules-body{padding:0 20px 20px;overflow-y:auto;min-height:0;overflow-wrap:anywhere}.rules-footer{justify-content:flex-end;flex-wrap:wrap;border-top:1px solid var(--line)}.rules-footer .field-hint{margin-right:auto}.case-rules-editor summary{cursor:pointer;min-height:44px;line-height:1.5;padding:10px 0;box-sizing:border-box}.case-rules-editor fieldset{border:0;padding:0;margin:0;min-width:0}.case-rules-editor label{display:grid;gap:6px;margin:10px 0}.case-rules-editor input:not([type=checkbox]),.case-rules-editor textarea,.case-rules-editor select{width:100%;box-sizing:border-box;min-width:0}.case-rules-editor textarea{resize:vertical}.rule-definition,.new-rule{border-top:1px solid var(--line);padding:14px 0;overflow-wrap:anywhere}.rule-enabled{display:flex!important;align-items:center;flex-wrap:wrap}.rules-phase{margin:10px 0}.error-text{color:var(--danger)}.rules-entry>.button.primary,.case-rules-editor .button.primary{background:var(--action);border-color:var(--action);color:#fff}.rules-entry>.button.primary:hover,.case-rules-editor .button.primary:hover{background:var(--action-hover);border-color:var(--action-hover)}.case-rules-editor :is(button,summary):focus-visible{outline:2px solid var(--accent);outline-offset:2px}
@media(max-width:600px){.rules-header,.rules-footer{padding:12px}.rules-body{padding:0 12px 12px}.rules-footer .field-hint{flex-basis:100%}.rules-footer .button{min-height:44px}}
.rules-phase h3{font-size:1rem;margin:18px 0 8px}.rule-summary{display:flex;gap:12px;align-items:center;padding:8px 0;border-top:1px solid var(--line)}.rule-title{flex:1;min-width:0}.rule-summary>.button{flex-shrink:0;min-height:44px}.case-rules-editor .rule-summary>.button{color:var(--accent);border-color:var(--accent);background:transparent}.case-rules-editor .rule-summary>.button:hover{background:var(--accent-soft)}
</style>
