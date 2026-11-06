<script setup>
const props=defineProps({question:Object,modelValue:{type:String,default:''},disabled:Boolean})
const emit=defineEmits(['update:modelValue'])
function selected(){try{return JSON.parse(props.modelValue || '[]')}catch{return []}}
function change(n,checked){const old=selected();emit('update:modelValue',JSON.stringify(props.question.questionType==='multiple'?(checked?[...old,n]:old.filter(x=>x!==n)):[n]))}
</script>
<template><textarea v-if="!question.questionType || question.questionType==='essay'" :aria-label="`題目 ${question.sortOrder || ''} 作答`" :value="modelValue" :disabled="disabled" rows="8" maxlength="16000" @input="emit('update:modelValue',$event.target.value)"></textarea><fieldset v-else :disabled="disabled"><legend>{{ question.questionType==='multiple'?'選擇所有適用答案':'選擇一個答案' }}</legend><label class="choice-label" v-for="(option,n) in question.options" :key="n" style="display:block;padding:8px"><input :type="question.questionType==='multiple'?'checkbox':'radio'" :name="`q-${question.id}`" :checked="selected().includes(n)" @change="change(n,$event.target.checked)" /> {{ option }}</label></fieldset></template>
