<script setup>
import {ref,onMounted} from 'vue'
import {api} from '../api'
const props=defineProps({form:Object,course:String})
const groups=ref([]),error=ref('')
onMounted(async()=>{try{groups.value=await api('/courses/'+props.course+'/groups')}catch(e){error.value=e.message}})
</script>
<template>
<label>繳交方式<select v-model="form.submissionMode"><option value="individual">個人作業</option><option value="group">小組作業</option></select></label>
<label><input type="checkbox" v-model="form.allowLate">允許截止後補交</label>
<fieldset v-if="form.submissionMode==='group'"><legend>指定課程小組</legend>
<p v-if="error" role="alert">{{error}}</p><label v-for="g in groups" :key="g.id"><input type="checkbox" v-model="form.groupIds" :value="g.id">{{g.name}}</label>
<p v-if="!groups.length">請先到課程分組建立小組。</p>
<label><input type="checkbox" v-model="form.allowDelegate">允許組員代組長繳交</label>
<label>貢獻評量<select v-model="form.evaluationMode"><option value="off">不啟用</option><option value="simple">簡易組員評量</option><option value="consensus">組員評量及同分共識</option></select></label>
<template v-if="form.evaluationMode!=='off'"><label v-for="(_,i) in form.dimensions" :key="i">評量面向 {{i+1}}<input v-model.trim="form.dimensions[i]" required maxlength="40"><button type="button" :disabled="form.dimensions.length===1" @click="form.dimensions.splice(i,1)">移除面向</button></label>
<button type="button" v-if="form.dimensions.length<4" @click="form.dimensions.push('')">新增面向</button></template>
</fieldset></template>
