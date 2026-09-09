<script setup lang="ts">
import { computed } from 'vue'
import type { QuotaStatus } from '../types'

const props = defineProps<{
  quotas: QuotaStatus[]
}>()

const poiQuota = computed(() =>
  props.quotas.find((q) => q.apiName === 'poi_search'),
)

const isLow = computed(() => {
  const q = poiQuota.value
  return q ? q.remaining < q.dailyLimit * 0.2 : false
})
</script>

<template>
  <div v-if="poiQuota" class="quota-banner" :class="{ low: isLow }" role="status">
    <span class="quota-label">今日 POI 检索</span>
    <span class="quota-value">
      剩余 <strong>{{ poiQuota.remaining }}</strong> / {{ poiQuota.dailyLimit }} 次
    </span>
    <span v-if="isLow" class="quota-warn">配额紧张，重复分析将优先使用缓存</span>
  </div>
</template>

<style scoped>
.quota-banner {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem 1rem;
  padding: 0.5rem 1rem;
  background: var(--ink-soft);
  border-bottom: 1px solid rgba(184, 149, 107, 0.2);
  font-size: 0.8125rem;
  color: var(--mist);
}

.quota-banner.low {
  border-bottom-color: var(--coral);
  color: var(--paper);
}

.quota-label {
  font-family: var(--font-mono);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  font-size: 0.6875rem;
}

.quota-value strong {
  color: var(--gold);
  font-weight: 600;
}

.quota-warn {
  color: var(--coral);
  font-size: 0.75rem;
}
</style>
