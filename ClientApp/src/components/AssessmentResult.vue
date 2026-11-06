<script setup>
import { computed } from 'vue'
import { CheckCircle2, AlertTriangle, HelpCircle, MinusCircle, FileQuestion, ShieldCheck } from '@lucide/vue'
const props = defineProps({ evaluation: { type: Object, required: true } })
const groups = computed(() => [
  { key: 'supportedClaims', title: '有據支持', icon: CheckCircle2, tone: 'supported' },
  { key: 'partiallySupportedClaims', title: '部分支持', icon: MinusCircle, tone: 'partial' },
  { key: 'unsupportedClaims', title: '缺乏支持或矛盾', icon: AlertTriangle, tone: 'unsupported' },
  { key: 'unverifiedClaims', title: '未核實的事實轉述', icon: HelpCircle, tone: 'unverified' },
  { key: 'importantOmissions', title: '重要遺漏', icon: FileQuestion, tone: 'omission' },
])
</script>
<template>
  <article class="evaluation-card"><header class="evaluation-heading"><div><span class="eyebrow">{{ evaluation.version === 'before' ? 'BEFORE' : evaluation.version === 'after' ? 'AFTER' : 'ASSESSMENT' }}</span><h3>{{ evaluation.version === 'before' ? '修改前版本' : evaluation.version === 'after' ? '修改後版本' : '本次作答' }}</h3></div><div class="score"><strong>{{ evaluation.score }}</strong><span>/ 4</span></div></header>
    <p v-if="!evaluation.caseEvidenceAvailable" class="provisional"><ShieldCheck :size="16" />暫定評分：未提供原始案例，事實與完整性尚未核實。</p>
    <section v-for="group in groups" :key="group.key" class="finding-group" :class="group.tone"><h4><component :is="group.icon" :size="17" />{{ group.title }}<span>{{ evaluation[group.key]?.length || 0 }}</span></h4><p v-if="!evaluation[group.key]?.length" class="no-findings">無列出項目</p><div v-for="(finding, index) in evaluation[group.key]" :key="index" class="finding"><strong>{{ finding.claim }}</strong><p><span>依據</span>{{ finding.evidence }}</p><p>{{ finding.explanation }}</p></div></section>
    <section class="rationale"><h4>臨床推理品質</h4><p>{{ evaluation.reasoningQuality }}</p><h4>評分理由</h4><p>{{ evaluation.justification }}</p></section>
  </article>
</template>
