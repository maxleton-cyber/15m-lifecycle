<script setup lang="ts">
import type { AgentTaskProgress } from '../types'

defineProps<{
  open: boolean
  progress: AgentTaskProgress | null
}>()

const stages = [
  { key: 'queued', label: '任务排队' },
  { key: 'parsing', label: '解析位置' },
  { key: 'isochrone', label: '批量算路' },
  { key: 'poi', label: '地点检索' },
  { key: 'report', label: 'AI 总结' },
  { key: 'done', label: '完成' },
]

function stageIndex(stage: string | undefined) {
  if (!stage) return -1
  if (stage === 'processing') return stages.findIndex((s) => s.key === 'queued')
  return stages.findIndex((s) => s.key === stage)
}

function normalizeStage(stage: string | undefined) {
  if (stage === 'processing') return 'queued'
  return stage
}
</script>

<template>
  <Teleport to="body">
    <div
      v-if="open"
      class="progress-overlay"
      role="dialog"
      aria-modal="true"
      aria-labelledby="progress-dialog-title"
      aria-live="polite"
    >
      <div class="progress-dialog">
        <div class="progress-spinner" aria-hidden="true" />
        <h2 id="progress-dialog-title">体检进行中</h2>
        <p class="progress-msg">
          {{ progress?.message ?? '正在准备体检任务…' }}
        </p>

        <ol class="stage-list">
          <li
            v-for="(s, i) in stages"
            :key="s.key"
            :class="{
              done: stageIndex(progress?.stage) > i,
              active: normalizeStage(progress?.stage) === s.key,
            }"
          >
            <span class="stage-marker">
              <span v-if="stageIndex(progress?.stage) > i" class="check">✓</span>
              <span v-else class="stage-dot" />
            </span>
            <span class="stage-label">{{ s.label }}</span>
            <span
              v-if="normalizeStage(progress?.stage) === s.key"
              class="stage-status"
            >进行中</span>
          </li>
        </ol>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.progress-overlay {
  position: fixed;
  inset: 0;
  z-index: 1000;
  display: grid;
  place-items: center;
  padding: 1.25rem;
  background: rgba(10, 18, 24, 0.72);
  backdrop-filter: blur(6px);
}

.progress-dialog {
  width: min(100%, 420px);
  padding: 1.75rem 1.5rem 1.5rem;
  border-radius: 16px;
  border: 1px solid rgba(184, 149, 107, 0.28);
  background:
    linear-gradient(165deg, rgba(28, 48, 52, 0.98), rgba(16, 28, 34, 0.98));
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.45);
  text-align: center;
}

.progress-spinner {
  width: 40px;
  height: 40px;
  margin: 0 auto 1rem;
  border-radius: 50%;
  border: 3px solid rgba(184, 149, 107, 0.22);
  border-top-color: var(--gold, #c4a574);
  animation: spin 0.9s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.progress-dialog h2 {
  font-family: var(--font-display);
  font-size: 1.25rem;
  color: var(--white, #f5f2ea);
  margin: 0 0 0.5rem;
}

.progress-msg {
  margin: 0 0 1.25rem;
  font-size: 0.9rem;
  line-height: 1.5;
  color: var(--mist, #b7c0c4);
  min-height: 2.7em;
}

.stage-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.55rem;
  text-align: left;
}

.stage-list li {
  display: grid;
  grid-template-columns: 1.25rem 1fr auto;
  align-items: center;
  gap: 0.65rem;
  padding: 0.55rem 0.7rem;
  border-radius: 10px;
  font-size: 0.875rem;
  color: var(--mist, #b7c0c4);
  background: rgba(255, 255, 255, 0.03);
}

.stage-list li.done {
  color: var(--jade-glow, #6fcf9c);
}

.stage-list li.active {
  color: var(--gold, #c4a574);
  font-weight: 600;
  background: rgba(196, 165, 116, 0.12);
  border: 1px solid rgba(196, 165, 116, 0.28);
}

.stage-marker {
  display: grid;
  place-items: center;
  width: 1.25rem;
  height: 1.25rem;
}

.stage-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: currentColor;
  opacity: 0.45;
}

.stage-list li.active .stage-dot {
  opacity: 1;
  box-shadow: 0 0 8px currentColor;
  animation: pulse 1.2s ease-in-out infinite;
}

.check {
  font-size: 0.75rem;
  font-weight: 700;
}

.stage-status {
  font-size: 0.75rem;
  font-weight: 500;
  opacity: 0.9;
}

@keyframes pulse {
  0%,
  100% {
    transform: scale(1);
    opacity: 1;
  }
  50% {
    transform: scale(1.25);
    opacity: 0.7;
  }
}
</style>
