<script setup>
import {computed,ref} from 'vue'
const props=defineProps({options:{type:Array,default:()=>[]},modelValue:{type:String,default:''},disabled:Boolean})
const emit=defineEmits(['update:modelValue','remove'])
const list=ref(null)
const entryIndex=computed(()=>Math.max(0,props.options.findIndex(option=>option.id===props.modelValue)))
function select(index){if(!props.disabled&&props.options[index])emit('update:modelValue',props.options[index].id)}
function remove(index){if(!props.disabled&&props.options[index]?.removable===true&&!props.options[index].removeDisabled)emit('remove',props.options[index].id)}
function navigate(index,event){
 if(props.disabled||!['ArrowUp','ArrowDown','Home','End'].includes(event.key))return
 event.preventDefault()
 const next=event.key==='Home'?0:event.key==='End'?props.options.length-1:Math.max(0,Math.min(props.options.length-1,index+(event.key==='ArrowDown'?1:-1)))
 select(next)
 const button=list.value?.querySelectorAll('.round-option')[next]
 button?.focus({preventScroll:true});button?.scrollIntoView?.({block:'nearest'})
}
</script>
<template>
 <ul ref="list" class="round-list revision-history-list" aria-label="作答版本">
  <li v-for="(option,index) in options" :key="option.id" :class="{'has-remove':option.removable===true}">
   <button type="button" class="round-option" :data-round="option.id" :aria-current="modelValue===option.id?'true':undefined" :tabindex="index===entryIndex?0:-1" :disabled="disabled" @click="select(index)" @keydown="navigate(index,$event)"><strong>{{option.title}}</strong><small v-for="(detail,detailIndex) in option.details" :key="detailIndex">{{detail}}</small></button>
   <button v-if="option.removable===true" type="button" class="round-remove" :data-remove-round="option.id" :aria-label="`將 ${option.title} 移至垃圾桶`" title="移至垃圾桶" :tabindex="index===entryIndex?0:-1" :disabled="disabled||option.removeDisabled" @click="remove(index)">×</button>
  </li>
 </ul>
</template>
<style scoped>
.revision-history-list{display:grid;gap:8px;list-style:none;min-width:0;max-width:100%;margin:14px 0;padding:0}.revision-history-list li{position:relative;min-width:0}.round-option{display:grid;gap:6px;width:100%;min-width:0;min-height:44px;padding:12px;text-align:left;white-space:normal;overflow-wrap:anywhere;border:1px solid var(--line);border-radius:8px;background:var(--surface);color:var(--text);font:inherit;cursor:pointer;box-sizing:border-box}.has-remove .round-option{padding-right:48px}.round-option strong{font-size:1.1rem;font-weight:700;line-height:1.5}.round-option small{font-size:.8rem;line-height:1.6;color:var(--muted)}.round-option[aria-current=true]{border-color:var(--accent);background:var(--accent-soft)}.round-option:focus-visible,.round-remove:focus-visible{outline:2px solid var(--accent);outline-offset:2px}.round-option:disabled,.round-remove:disabled{cursor:default}.round-remove{position:absolute;top:4px;right:4px;display:grid;place-items:center;width:36px;height:36px;padding:0;border:1px solid transparent;border-radius:6px;background:transparent;color:var(--muted);font:inherit;font-size:1.35rem;line-height:1;cursor:pointer}.round-remove:hover:not(:disabled){border-color:var(--line);background:var(--surface);color:var(--text)}.round-remove:disabled{opacity:.5}
</style>
