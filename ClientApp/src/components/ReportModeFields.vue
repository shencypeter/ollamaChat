<script setup>
import {onMounted,ref} from 'vue'
import {Plus,Trash2} from '@lucide/vue'
import {api} from '../api'
const props=defineProps({form:Object,course:String})
const groups=ref([]),error=ref(''),loading=ref(true)
async function loadGroups(){
 loading.value=true;error.value=''
 try{groups.value=await api('/courses/'+props.course+'/groups')}
 catch(e){error.value=e.message}
 finally{loading.value=false}
}
onMounted(loadGroups)
</script>
<template>
 <label>繳交方式<select v-model="form.submissionMode"><option value="individual">個人作業</option><option value="group">小組作業</option></select></label>
 <label><input type="checkbox" v-model="form.allowLate">允許截止後補交</label>
 <fieldset v-if="form.submissionMode==='group'" class="report-group-options">
  <legend>小組繳交設定</legend>
  <h4>指定課程小組</h4>
  <p v-if="loading" role="status">載入課程小組中…</p>
  <div v-else-if="error"><p role="alert" class="error-box">{{error}}</p><button type="button" class="button secondary" aria-label="重新載入課程小組" @click="loadGroups">重新載入</button></div>
  <template v-else><label v-for="g in groups" :key="g.id"><input type="checkbox" v-model="form.groupIds" :value="g.id">{{g.name}}</label><p v-if="!groups.length">請先到課程分組建立小組。</p></template>
  <p class="field-hint">選取的小組須有有效組長；發布後以繳交時的組員名單計分。</p>
  <label><input type="checkbox" v-model="form.allowDelegate">允許組員代組長繳交</label>
  <h4>成績分配</h4>
  <p>先給小組基礎成績，所有組員預設同分。批改時可依個人貢獻做個別調整，並須填寫原因。</p>
  <h4>組員回饋蒐集</h4>
  <label>回饋方式<select v-model="form.evaluationMode" aria-label="組員回饋方式"><option value="off">不收集組員回饋</option><option value="simple">收集組員互評</option><option value="consensus">收集組員互評＋同分意願</option></select></label>
  <p v-if="form.evaluationMode==='off'" class="field-hint">不收集互評或同分意願，教師仍可給全組同分或依原因個別調整。</p>
  <p v-else-if="form.evaluationMode==='simple'" class="field-hint">每位組員評估其他組員的貢獻，供教師參考，不會自動換算成績。</p>
  <p v-else class="field-hint">同時收集組員互評與同分意願；每位組員可同意同分、請教師評估或保留意見。表態不會自動決定成績。</p>
  <fieldset v-if="form.evaluationMode!=='off'" class="report-dimensions">
   <legend>互評面向</legend><p class="field-hint">設定 1–4 個不同面向，每項最多 40 字。學生以 1–5 分填答；這些分數不是作業得分或成績權重。</p>
   <label v-for="(_,i) in form.dimensions" :key="i">評量面向 {{i+1}}<span class="report-dimension-control"><input v-model.trim="form.dimensions[i]" required maxlength="40" :aria-label="`評量面向 ${i+1}`" placeholder="例如：分工完成、討論參與"><button type="button" class="icon-button" :aria-label="`移除評量面向 ${i+1}`" :title="`移除評量面向 ${i+1}`" :disabled="form.dimensions.length===1" @click="form.dimensions.splice(i,1)"><Trash2 :size="16"/></button></span></label>
   <button type="button" class="button secondary" aria-label="新增評量面向" :disabled="form.dimensions.length>=4" @click="form.dimensions.push('')"><Plus :size="16"/>新增面向（{{form.dimensions.length}}/4）</button>
  </fieldset>
 </fieldset>
</template>
