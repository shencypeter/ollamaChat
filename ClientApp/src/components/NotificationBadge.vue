<script setup>
defineProps({count:{type:Number,default:0},label:{type:String,default:'未讀通知'}})
</script>
<template><span v-if="count>0" class="notification-badge" :aria-label="`${label} ${count} 筆`" :title="`${label} ${count} 筆`">{{count>99?'99+':count}}</span></template>
