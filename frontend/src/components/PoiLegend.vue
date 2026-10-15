<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { BlindAreaAnalysis, CategoryCoverage } from '../types'
import { blindLevelStyle, getCategoryMeta } from '../constants/poiCategories'

const props = defineProps<{
  categories: CategoryCoverage[]
  visible: string[]
  blindArea?: BlindAreaAnalysis | null
  /** 默认是否展开 */
  defaultOpen?: boolean
}>()

const blindLevels = [1, 2, 3, 4, 5, 6, 7]
const open = ref(props.defaultOpen !== false)

watch(
  () => props.categories.length,
  (n) => {
    if (n > 0 && props.defaultOpen !== false) open.value = true
  },
)

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

function togglePanel() {
  open.value = !open.value
}
</script>

<template>
  <div class="legend-dock" :class="{ open, closed: !open }">
    <!-- 按钮在弹窗左侧，与弹窗一起贴紧地图右边 -->
    <div class="legend-cluster">
      <button
        type="button"
        class="legend-fab"
        :aria-expanded="open"
        aria-controls="map-legend-panel"
        :title="open ? '收起图例' : '展开图例'"
        @click="togglePanel"
      >
        {{ open ? '⟩' : '图例' }}
      </button>

      <div
        v-show="open"
        id="map-legend-panel"
        class="legend-panel"
        role="region"
        aria-label="设施图例与盲区灰度"
      >
        <div class="legend-header">
          <span class="legend-title">设施图例</span>
          <button type="button" class="legend-toggle-all" @click="toggleAll">
            {{ allVisible ? '全部隐藏' : '全部显示' }}
          </button>
        </div>
        <p class="legend-hint">点击切换显示，双击仅显示该类</p>
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

        <div v-if="blindArea" class="blind-legend">
          <span class="legend-title">盲区灰度</span>
          <p class="legend-hint">灰色越深表示缺失类别越多</p>
          <div class="blind-scale" role="list">
            <span
              v-for="level in blindLevels"
              :key="level"
              class="blind-swatch"
              role="listitem"
              :title="`缺 ${level} 类`"
              :style="{ background: blindLevelStyle(level).cssBackground }"
            >
              缺{{ level }}
            </span>
          </div>
          <p v-if="blindArea.blindAreaRatio >= 0" class="blind-ratio">
            盲区占比 {{ (blindArea.blindAreaRatio * 100).toFixed(0) }}%
          </p>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.legend-dock {
  position: absolute;
  top: 0.75rem;
  right: 0.75rem;
  z-index: 600;
  pointer-events: none;
  max-height: calc(100% - 1.5rem);
}

.legend-cluster {
  display: flex;
  flex-direction: row;
  align-items: flex-start;
  gap: 0.4rem;
  pointer-events: auto;
  max-height: inherit;
}

.legend-fab {
  flex-shrink: 0;
  min-width: 2.25rem;
  height: 2.25rem;
  padding: 0 0.65rem;
  border-radius: 8px;
  border: 1px solid rgba(184, 149, 107, 0.35);
  background: rgba(12, 21, 32, 0.92);
  color: var(--gold, #c4a574);
  font-size: 0.8125rem;
  font-weight: 600;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.35);
  cursor: pointer;
}

.legend-fab:hover {
  background: rgba(28, 42, 52, 0.96);
}

.legend-panel {
  width: 280px;
  max-height: inherit;
  overflow-y: auto;
  padding: 0.75rem;
  background: rgba(12, 21, 32, 0.92);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  box-shadow: 0 8px 28px rgba(0, 0, 0, 0.4);
  backdrop-filter: blur(8px);
}

.legend-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 0.375rem;
  gap: 0.5rem;
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
  background: transparent;
  cursor: pointer;
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
  flex-direction: column;
  gap: 0.4rem;
}

.legend-chip {
  display: flex;
  align-items: center;
  gap: 0.375rem;
  width: 100%;
  padding: 0.375rem 0.5rem;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: var(--radius-sm);
  color: var(--paper);
  font-size: 0.8125rem;
  text-align: left;
  cursor: pointer;
  transition: opacity 0.2s, border-color 0.2s;
}

.legend-chip.off {
  opacity: 0.4;
  filter: grayscale(0.8);
}

.legend-chip:not(.off) {
  border-color: rgba(45, 106, 90, 0.45);
}

.legend-chip.blind:not(.off) {
  border-color: rgba(196, 92, 62, 0.5);
}

.legend-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  flex-shrink: 0;
  background: linear-gradient(145deg, var(--poi-color), #1a2332);
  border: 2px solid rgba(255, 255, 255, 0.85);
  border-radius: 8px 8px 8px 2px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.4);
  transform: rotate(-4deg);
}

.legend-icon-emoji {
  font-size: 13px;
  line-height: 1;
  transform: rotate(4deg);
}

.legend-label {
  flex: 1;
  min-width: 0;
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

.blind-legend {
  margin-top: 0.875rem;
  padding-top: 0.75rem;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
}

.blind-scale {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
  margin-bottom: 0.5rem;
}

.blind-swatch {
  min-width: 2.1rem;
  padding: 0.2rem 0.25rem;
  border-radius: 3px;
  border: 1px solid rgba(255, 255, 255, 0.12);
  font-size: 0.625rem;
  color: #1a1a1a;
  text-align: center;
}

.blind-ratio {
  margin-top: 0.35rem;
  font-family: var(--font-mono);
  font-size: 0.6875rem;
  color: var(--mist);
}

@media (max-width: 640px) {
  .legend-dock {
    top: auto;
    bottom: 0.75rem;
    max-height: 45%;
  }

  .legend-panel {
    width: min(260px, calc(100vw - 5rem));
  }
}
</style>
