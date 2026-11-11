<script setup>
import {computed,onMounted,ref} from 'vue'
import {api} from '../api'
const props=defineProps({report:Object,submission:Object,base:String,staff:Boolean})
const url=props.base+'/submissions/'+props.submission.id
const reviews=ref([]),votes=ref([]),forms=ref({}),vote=ref({choice:'pending',explanation:''}),message=ref(''),ready=ref(false),busy=ref(false)
const members=computed(()=>props.submission.members)
const name=id=>members.value.find(m=>m.id===id)?.name
const pending=computed(()=>members.value.filter(m=>reviews.value.filter(r=>r.reviewer===m.id).length<members.value.length-1))
const consensus=computed(()=>votes.value.some(v=>v.choice==='review')?'需要教師確認':votes.value.filter(v=>v.choice==='agree').length===members.value.length?'全組同意同分':'尚未完成確認')
const choices={agree:'同意全組採相同分數',review:'希望教師依個人貢獻評估',pending:'尚未表態'}
function average(id,i){const rows=reviews.value.filter(r=>r.subject===id);return rows.length?(rows.reduce((sum,r)=>sum+r.ratings[i],0)/rows.length).toFixed(1):'—'}
onMounted(async()=>{try{
 const data=await api(url+'/evaluation');reviews.value=data.reviews;votes.value=data.votes
 for(const m of members.value)forms.value[m.id]=data.reviews.find(r=>r.subject===m.id)||{ratings:props.report.dimensions.map(()=>null),comment:''}
 if(!props.staff && data.votes.length)vote.value=data.votes[0]
 ready.value=true
}catch(e){message.value=e.message}})
async function save(path,body){busy.value=true;try{await api(url+path,{method:'PUT',body});message.value='已儲存'}catch(e){message.value=e.message}finally{busy.value=false}}
</script>
<template><section><h3>組員貢獻評量</h3><p>回覆供教師及有評分權限的助教參考。小組人數少時，教師可能從內容辨認填答者。</p>
<p role="status">{{message}}</p>
<template v-if="ready && staff"><h4>完成情況</h4><p>尚未完成：{{pending.map(m=>m.name).join('、')||'無'}}</p>
<h4>各面向平均（五級量表）</h4><p v-for="m in members" :key="m.id">{{m.name}} · {{report.dimensions.map((d,i)=>d+' '+average(m.id,i)).join('、')}}</p>
<details><summary>個別回覆（{{reviews.length}}）</summary><p v-for="r in reviews" :key="r.reviewer+r.subject">{{name(r.reviewer)}} 評 {{name(r.subject)}} · {{report.dimensions.map((d,i)=>d+' '+r.ratings[i]).join('、')}} · {{r.comment}}</p></details>
<template v-if="report.evaluationMode==='consensus'"><h4>{{consensus}}</h4><p v-for="m in members" :key="m.id">{{m.name}} · {{choices[votes.find(v=>v.voter===m.id)?.choice||'pending']}} {{votes.find(v=>v.voter===m.id)?.explanation}}</p></template>
</template><template v-else-if="ready"><form v-for="m in members.filter(x=>!x.self)" :key="m.id" @submit.prevent="save('/reviews/'+m.id,forms[m.id])"><fieldset><legend>{{m.name}}</legend>
<label v-for="(d,i) in report.dimensions" :key="d">{{d}}<select required v-model="forms[m.id].ratings[i]"><option disabled :value="null">選擇分數</option><option v-for="n in 5" :key="n" :value="n">{{n}}</option></select></label>
<label>意見（選填）<textarea maxlength="500" v-model="forms[m.id].comment"></textarea></label><button class="button secondary" :disabled="busy">儲存對此組員的評量</button></fieldset></form>
<form v-if="report.evaluationMode==='consensus'" @submit.prevent="save('/vote',vote)"><fieldset><legend>我的同分意願</legend><label v-for="(label,key) in choices" :key="key"><input type="radio" v-model="vote.choice" :value="key">{{label}}</label><label>說明（選填）<textarea maxlength="500" v-model="vote.explanation"></textarea></label><button class="button secondary" :disabled="busy">儲存我的表態</button></fieldset></form>
</template></section></template>
