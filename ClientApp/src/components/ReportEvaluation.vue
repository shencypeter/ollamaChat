<script setup>
import {computed,ref,watch} from 'vue'
import {api} from '../api'
const props=defineProps({report:Object,submission:Object,base:String,staff:Boolean})
const reviews=ref([]),votes=ref([]),forms=ref({}),vote=ref({choice:'pending',explanation:''})
const message=ref(''),error=ref(''),ready=ref(false),loading=ref(false),busy=ref(false),saved=ref([])
const members=computed(()=>props.submission.members||[])
const url=computed(()=>props.base+'/submissions/'+props.submission.id)
const name=id=>members.value.find(m=>m.id===id)?.name||'繳交時組員'
const pending=computed(()=>members.value.filter(m=>reviews.value.filter(r=>r.reviewer===m.id).length<members.value.length-1))
const consensus=computed(()=>votes.value.some(v=>v.choice==='review')?'需要教師確認':votes.value.filter(v=>v.choice==='agree').length===members.value.length?'全組同意同分':'尚未完成確認')
const choices={agree:'同意全組採相同分數',review:'希望教師依個人貢獻評估',pending:'尚未表態'}
function average(id,i){const rows=reviews.value.filter(r=>r.subject===id);return rows.length?(rows.reduce((sum,r)=>sum+r.ratings[i],0)/rows.length).toFixed(1):'—'}
let request=0
async function load(){
 const token=++request;ready.value=false;loading.value=true;error.value='';message.value='';saved.value=[]
 try{
  const data=await api(url.value+'/evaluation');if(token!==request)return
  reviews.value=data.reviews;votes.value=data.votes
  forms.value=Object.fromEntries(members.value.map(m=>{const review=data.reviews.find(r=>r.subject===m.id);return [m.id,{ratings:review?[...review.ratings]:props.report.dimensions.map(()=>null),comment:review?.comment||''}]}))
  vote.value={choice:data.votes[0]?.choice||'pending',explanation:data.votes[0]?.explanation||''}
  saved.value=data.reviews.map(r=>r.subject);ready.value=true
 }catch(e){if(token===request)error.value=e.message}
 finally{if(token===request)loading.value=false}
}
watch(()=>[props.base,props.submission.id],load,{immediate:true})
async function save(path,body,subject=null){
 busy.value=true;error.value='';message.value='';const endpoint=url.value
 try{await api(endpoint+path,{method:'PUT',body});if(endpoint!==url.value)return;if(subject&&!saved.value.includes(subject))saved.value.push(subject);message.value=subject?`已儲存對${name(subject)}的評量。`:'已儲存我的同分意願。'}
 catch(e){if(endpoint===url.value)error.value=e.message}
 finally{busy.value=false}
}
</script>
<template>
 <section class="report-evaluation" :aria-busy="loading||busy">
  <h3>組員回饋（評分參考）</h3>
  <p>回覆供教師及有評分權限的助教參考，不會自動換算或改動成績。小組人數少時，教師可能從內容辨認填答者。</p>
  <p v-if="report.evaluationMode==='consensus'" class="field-hint">本作業同時收集組員互評與同分意願。互評是否完成與同分表態分開呈現，最終成績由教師評定。</p>
  <p v-if="loading" role="status">載入組員回饋中…</p>
  <div v-if="error"><p class="error-box" role="alert">{{error}}</p><button v-if="!ready" type="button" class="button secondary" aria-label="重新載入組員回饋" @click="load">重新載入</button></div>
  <p v-if="message" role="status" class="success-box">{{message}}</p>
  <template v-if="ready && staff">
   <h4>互評完成情況</h4><p v-if="members.length<2">單人小組，無其他組員可互評。</p><template v-else><p>已收 {{reviews.length}} / {{members.length*(members.length-1)}} 份組員評量</p><p>尚未完成：{{pending.map(m=>m.name).join('、')||'無'}}</p></template>
   <h4>各面向平均（1–5 分）</h4><p class="field-hint">— 表示尚無回覆。平均值供判斷貢獻參考，不是個人成績。</p><p v-for="m in members" :key="m.id">{{m.name}} · {{report.dimensions.map((d,i)=>d+' '+average(m.id,i)).join('、')}}</p>
   <details><summary>個別回覆（{{reviews.length}}）</summary><p v-if="!reviews.length" class="empty-inline">尚無組員回覆。</p><p v-for="r in reviews" :key="r.reviewer+r.subject">{{name(r.reviewer)}} 評 {{name(r.subject)}} · {{report.dimensions.map((d,i)=>d+' '+r.ratings[i]).join('、')}} · {{r.comment||'未填文字意見'}}</p></details>
   <template v-if="report.evaluationMode==='consensus'"><h4>同分意願：{{consensus}}</h4><p v-for="m in members" :key="m.id">{{m.name}} · {{choices[votes.find(v=>v.voter===m.id)?.choice||'pending']}} {{votes.find(v=>v.voter===m.id)?.explanation}}</p></template>
  </template>
  <template v-else-if="ready">
   <p v-if="members.length<2">單人小組，無其他組員可互評。</p><p v-else class="field-hint">請評估每位其他組員；1 分最低，5 分最高。每位組員分別儲存，之後仍可修改自己的回覆。</p>
   <form v-for="m in members.filter(x=>!x.self)" :key="m.id" @submit.prevent="save('/reviews/'+m.id,forms[m.id],m.id)"><fieldset :disabled="busy"><legend>{{m.name}}</legend>
    <p class="field-hint">{{saved.includes(m.id)?'已有儲存回覆；修改後請再次儲存。':'尚未儲存此組員的評量。'}}</p>
    <label v-for="(d,i) in report.dimensions" :key="d">{{d}}<select required v-model="forms[m.id].ratings[i]"><option disabled :value="null">選擇分數</option><option v-for="n in 5" :key="n" :value="n">{{n}} 分</option></select></label>
    <label>意見（選填）<textarea maxlength="500" v-model="forms[m.id].comment"></textarea></label><button class="button secondary" :disabled="busy">儲存對此組員的評量</button>
   </fieldset></form>
   <form v-if="report.evaluationMode==='consensus'" @submit.prevent="save('/vote',vote)"><fieldset :disabled="busy"><legend>我的同分意願</legend><label v-for="(label,key) in choices" :key="key"><input type="radio" v-model="vote.choice" :value="key">{{label}}</label><label>說明（選填）<textarea maxlength="500" v-model="vote.explanation"></textarea></label><button class="button secondary" :disabled="busy">儲存我的表態</button></fieldset></form>
  </template>
 </section>
</template>
