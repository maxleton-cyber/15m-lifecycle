<script setup lang="ts">
import type { AgentTaskProgress } from '../types'

defineProps<{
  isLoading: boolean
  progress: AgentTaskProgress | null
  stage?: string
}>()

const stages = [
  { key: 'queued', label: '排队' },
  { key: 'parsing', label: '解析位置' },
  { key: 'isochrone', label: '计算等时圈' },
  { key: 'poi', label: '检索设施' },
  { key: 'report', label: '生成报告' },
  { key: 'done', label: '完成' },
]

function stageIndex(stage: string | undefined) {
  if (!stage) return -1
  return stages.findIndex((s) => s.key === stage)
}
</script>

<template>
  <div v-if="isLoading" class="progress-panel" role="status" aria-live="polite">
    <h3>体检进行中</h3>
    <p class="progress-msg">{{ progress?.message ?? '正在处理…' }}</p>
    <ol class="stage-list">
      <li
        v-for="(s, i) in stages"
        :key="s.key"
        :class="{
          done: stageIndex(progress?.stage) > i,
          active: progress?.stage === s.key,
        }"
      >
        <span class="stage-dot" />
        {{ s.label }}
      </li>
    </ol>
  </div>
</template>

<style scoped>
.progress-panel {
  padding: 1rem;
  background: rgba(45, 106, 90, 0.1);
  border: 1px solid rgba(45, 106, 90, 0.3);
  border-radius: var(--radius-md);
}

.progress-panel h3 {
  font-family: var(--font-display);
  font-size: 1rem;
  color: var(--jade-glow);
  margin-bottom: 0.5rem;
}

.progress-msg {
  font-size: 0.875rem;
  color: var(--paper);
  margin-bottom: 1rem;
}

.stage-list {
  list-style: none;
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem 1rem;
}

.stage-list li {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.8125rem;
  color: var(--mist);
}

.stage-list li.done {
  color: var(--jade-glow);
}

.stage-list li.active {
  color: var(--gold);
  font-weight: 600;
}

.stage-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: currentColor;
  opacity: 0.5;
}

.stage-list li.active .stage-dot {
  opacity: 1;
  box-shadow: 0 0 8px currentColor;
}
</style>
