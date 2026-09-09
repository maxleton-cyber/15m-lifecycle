<script setup lang="ts">
import { computed, ref } from 'vue'
import { formatCoord } from '../utils/coord'
import type { CoordSystem, LatLng, SelectedLocation } from '../types'

const props = defineProps<{
  mouseCoords: LatLng | null
  centerCoords: SelectedLocation | null
  displayCoordSystem?: CoordSystem
}>()

const expanded = ref(false)

const coordLabel = computed(() => {
  const system = props.displayCoordSystem ?? props.centerCoords?.coordSystem ?? 'bd09'
  if (system === 'wgs84') return 'WGS84'
  if (system === 'gcj02') return 'GCJ-02'
  return 'BD-09'
})
</script>

<template>
  <div class="coord-display" aria-label="坐标信息">
    <button type="button" class="coord-toggle" @click="expanded = !expanded">
      <span>坐标信息（{{ coordLabel }}）</span>
      <span class="coord-toggle-icon">{{ expanded ? '▲' : '▼' }}</span>
    </button>
    <div v-show="expanded" class="coord-grid">
      <div class="coord-block">
        <span class="coord-label">鼠标位置</span>
        <div class="coord-values">
          <span>
            纬度
            <strong>{{ formatCoord(mouseCoords?.lat) }}</strong>
          </span>
          <span>
            经度
            <strong>{{ formatCoord(mouseCoords?.lng) }}</strong>
          </span>
        </div>
      </div>
      <div class="coord-block">
        <span class="coord-label">分析中心</span>
        <div class="coord-values">
          <span>
            纬度
            <strong>{{ formatCoord(centerCoords?.lat) }}</strong>
          </span>
          <span>
            经度
            <strong>{{ formatCoord(centerCoords?.lng) }}</strong>
          </span>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.coord-display {
  background: var(--ink);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: var(--radius-md);
  padding: 0.5rem 0.75rem;
}

.coord-toggle {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  padding: 0.25rem 0;
  background: none;
  border: none;
  color: var(--jade-glow);
  font-family: var(--font-mono);
  font-size: 0.6875rem;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  cursor: pointer;
}

.coord-toggle-icon {
  font-size: 0.625rem;
  color: var(--mist);
}

.coord-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 0.625rem;
  margin-top: 0.625rem;
  padding-top: 0.625rem;
  border-top: 1px solid rgba(255, 255, 255, 0.06);
}

@media (min-width: 480px) {
  .coord-grid {
    grid-template-columns: 1fr 1fr;
  }
}

.coord-block {
  padding: 0.625rem 0.75rem;
  background: var(--ink-soft);
  border-radius: var(--radius-sm);
  border: 1px solid rgba(255, 255, 255, 0.06);
}

.coord-label {
  display: block;
  font-size: 0.75rem;
  color: var(--mist);
  margin-bottom: 0.375rem;
}

.coord-values {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.75rem;
  color: var(--mist);
}

.coord-values strong {
  display: inline-block;
  margin-left: 0.375rem;
  font-family: var(--font-mono);
  font-size: 0.875rem;
  color: var(--gold);
  font-weight: 500;
}
</style>
