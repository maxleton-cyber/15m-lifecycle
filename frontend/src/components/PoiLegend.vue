<script setup lang="ts">
import { computed } from 'vue'
import type { CategoryCoverage } from '../types'
import { getCategoryMeta } from '../constants/poiCategories'

const props = defineProps<{
  categories: CategoryCoverage[]
  visible: string[]
}>()

const emit = defineEmits<{
  'update:visible': [categories: string[]]
}>()

const allCategoryIds = computed(() => props.categories.map((c) => c.category))

const allVisible = computed(
  () => props.visible.length === allCategoryIds.value.length,
)

function isVisible(category: string) {
  return props.visible.includes(category)
}

function toggle(category: string) {
  const next = isVisible(category)
    ? props.visible.filter((c) => c !== category)
    : [...props.visible, category]
  emit('update:visible', next)
}

function toggleAll() {
  emit(
    'update:visible',
    allVisible.value ? [] : [...allCategoryIds.value],
  )
}

function showOnly(category: string) {
  emit('update:visible', [category])
}
</script>

<template>
  <div class="legend-panel">
    <div class="legend-header">
      <span class="legend-title">设施图例</span>
      <button type="button" class="legend-toggle-all" @click="toggleAll">
        {{ allVisible ? '全部隐藏' : '全部显示' }}
      </button>
    </div>
    <p class="legend-hint">点击图标切换显示/隐藏，双击仅显示该类别</p>
    <div class="legend-grid" role="list">
      <button
        v-for="cat in categories"
        :key="cat.category"
        type="button"
        class="legend-chip"
        :class="{
          off: !isVisible(cat.category),
          blind: cat.isBlindSpot,
        }"
        role="listitem"
        :aria-pressed="isVisible(cat.category)"
        @click="toggle(cat.category)"
        @dblclick="showOnly(cat.category)"
      >
        <span
          class="legend-icon"
          :style="{ '--poi-color': getCategoryMeta(cat.category).color }"
        >
          <span class="legend-icon-emoji">{{ getCategoryMeta(cat.category).emoji }}</span>
        </span>
        <span class="legend-label">{{ cat.label }}</span>
        <span class="legend-count">{{ cat.count }}</span>
        <span v-if="cat.isBlindSpot" class="legend-badge">盲区</span>
      </button>
    </div>
  </div>
</template>

<style scoped>
.legend-panel {
  padding: 0.75rem;
  background: rgba(12, 21, 32, 0.85);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: var(--radius-md);
}

.legend-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 0.375rem;
}

.legend-title {
  font-family: var(--font-display);
  font-size: 0.875rem;
  color: var(--paper);
}

.legend-toggle-all {
  font-size: 0.75rem;
  color: var(--jade-glow);
  padding: 0.25rem 0.5rem;
  border: 1px solid rgba(45, 106, 90, 0.4);
  border-radius: var(--radius-sm);
}

.legend-toggle-all:hover {
  background: rgba(45, 106, 90, 0.2);
}

.legend-hint {
  font-size: 0.6875rem;
  color: var(--mist);
  margin-bottom: 0.625rem;
}

.legend-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.legend-chip {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.375rem 0.5rem;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: var(--radius-sm);
  color: var(--paper);
  font-size: 0.8125rem;
  transition: opacity 0.2s, border-color 0.2s;
}

.legend-chip.off {
  opacity: 0.4;
  filter: grayscale(0.8);
}

.legend-chip:not(.off) {
  border-color: var(--poi-color, var(--jade));
  box-shadow: 0 0 0 1px rgba(45, 106, 90, 0.2);
}

.legend-chip.blind:not(.off) {
  border-color: rgba(196, 92, 62, 0.5);
}

.legend-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  background: linear-gradient(145deg, var(--poi-color), #1a2332);
  border: 2px solid rgba(255, 255, 255, 0.85);
  border-radius: 8px 8px 8px 2px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.4);
  transform: rotate(-4deg);
}

.legend-icon-emoji {
  font-size: 14px;
  line-height: 1;
  transform: rotate(4deg);
}

.legend-count {
  font-family: var(--font-mono);
  font-size: 0.75rem;
  color: var(--mist);
}

.legend-badge {
  font-size: 0.625rem;
  padding: 0.1rem 0.35rem;
  background: var(--coral);
  color: var(--white);
  border-radius: 2px;
}
</style>
