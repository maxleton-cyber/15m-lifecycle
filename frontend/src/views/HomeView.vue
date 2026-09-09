<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { createAgentTask, getAgentTask, getQuotaStatus, pollAgentTask } from '../api'
import AddressSelector from '../components/AddressSelector.vue'
import AnalysisProgress from '../components/AnalysisProgress.vue'
import CoordDisplay from '../components/CoordDisplay.vue'
import MapPanel from '../components/MapPanel.vue'
import PoiLegend from '../components/PoiLegend.vue'
import QuotaBanner from '../components/QuotaBanner.vue'
import ReportPanel from '../components/ReportPanel.vue'
import { useAnalysisStore } from '../store/analysis'
import type { CoordSystem, LatLng, SelectedLocation } from '../types'
import { convertToBd09, normalizeLocation, parseCoordText } from '../utils/baiduCoord'

const route = useRoute()
const store = useAnalysisStore()

const inputMode = ref<'address' | 'coords'>('address')
const selectedLocation = ref<SelectedLocation | null>(null)
const mouseCoords = ref<LatLng | null>(null)
const coordLat = ref(39.996)
const coordLng = ref(116.481)
const coordInputSystem = ref<CoordSystem>('bd09')
const coordPaste = ref('')
const isApplyingCoords = ref(false)
const error = ref('')
const visibleCategories = ref<string[]>([])

const mapCoordSystem = computed(
  () => selectedLocation.value?.coordSystem ?? 'bd09',
)

onMounted(async () => {
  try {
    store.quotaStatus = await getQuotaStatus()
  } catch {
    /* optional */
  }

  const taskId = route.query.taskId as string
  if (taskId && !store.reportData) {
    try {
      const task = await getAgentTask(taskId)
      store.setTask(task)
      if (task.reportData) {
        setLocation({
          lat: task.reportData.center.lat,
          lng: task.reportData.center.lng,
          address: `坐标 ${task.reportData.center.lat}, ${task.reportData.center.lng}`,
          name: '分析位置',
          coordSystem: 'bd09',
        })
        inputMode.value = 'coords'
      }
    } catch {
      /* ignore */
    }
  }
})

function setLocation(loc: SelectedLocation) {
  selectedLocation.value = loc
  coordLat.value = loc.lat
  coordLng.value = loc.lng
}

async function onLocationSelect(loc: SelectedLocation) {
  const normalized = await normalizeLocation(loc)
  setLocation(normalized)
  store.reset()
}

function onMapMousemove(coords: LatLng) {
  mouseCoords.value = coords
}

function onMapMouseleave() {
  mouseCoords.value = null
}

function fillFromPaste() {
  const parsed = parseCoordText(coordPaste.value)
  if (!parsed) {
    error.value = '无法解析坐标，请使用「经度,纬度」或「纬度,经度」格式'
    return
  }
  error.value = ''
  coordLat.value = parsed.lat
  coordLng.value = parsed.lng
}

async function applyCoords() {
  if (Number.isNaN(coordLat.value) || Number.isNaN(coordLng.value)) {
    error.value = '请输入有效的经纬度'
    return
  }
  if (coordLat.value < -90 || coordLat.value > 90) {
    error.value = '纬度范围应为 -90 ~ 90'
    return
  }
  if (coordLng.value < -180 || coordLng.value > 180) {
    error.value = '经度范围应为 -180 ~ 180'
    return
  }

  isApplyingCoords.value = true
  error.value = ''
  try {
    const { lng, lat } = await convertToBd09(
      coordLng.value,
      coordLat.value,
      coordInputSystem.value,
    )
    setLocation({
      lat,
      lng,
      address: `BD-09 ${lat.toFixed(6)}, ${lng.toFixed(6)}`,
      name: `坐标点 (${lat.toFixed(4)}, ${lng.toFixed(4)})`,
      coordSystem: 'bd09',
    })
    coordLat.value = lat
    coordLng.value = lng
    coordInputSystem.value = 'bd09'
    store.reset()
  } catch (e) {
    error.value = e instanceof Error ? e.message : '坐标转换失败'
  } finally {
    isApplyingCoords.value = false
  }
}

watch(inputMode, () => {
  error.value = ''
})

watch(
  () => store.reportData,
  (data) => {
    if (data) {
      visibleCategories.value = data.categories.map((c) => c.category)
    } else {
      visibleCategories.value = []
    }
  },
)

async function startAnalysis() {
  if (!selectedLocation.value) {
    error.value =
      inputMode.value === 'coords'
        ? '请输入坐标并点击「应用坐标」'
        : '请输入地址并从列表中选择位置'
    return
  }

  error.value = ''
  store.isLoading = true
  store.reset()

  try {
    const loc = selectedLocation.value
    const taskId = await createAgentTask({
      lat: loc.lat,
      lng: loc.lng,
      address: loc.address,
    })

    const result = await pollAgentTask(taskId, (task) => {
      store.setTask(task)
    })

    store.setTask(result)
    store.isLoading = false

    document.getElementById('results-section')?.scrollIntoView({ behavior: 'smooth' })
  } catch (e) {
    error.value = e instanceof Error ? e.message : '分析失败，请稍后重试'
    store.isLoading = false
  }
}
</script>

<template>
  <div class="dashboard">
    <QuotaBanner :quotas="store.quotaStatus" />

    <header class="hero">
      <p class="eyebrow">15 分钟步行可达 · 真实路网</p>
      <h1>生活圈体检助手</h1>
      <p class="subtitle">
        输入地址或坐标，一键生成等时圈、设施覆盖统计与 AI 诊断报告
      </p>
    </header>

    <div class="workspace">
      <aside class="sidebar">
        <section class="module-card">
          <h2 class="module-title">
            <span class="module-num">1</span>
            选择分析位置
          </h2>

          <div class="mode-tabs" role="tablist">
            <button
              role="tab"
              :aria-selected="inputMode === 'address'"
              :class="{ active: inputMode === 'address' }"
              @click="inputMode = 'address'"
            >
              地址搜索
            </button>
            <button
              role="tab"
              :aria-selected="inputMode === 'coords'"
              :class="{ active: inputMode === 'coords' }"
              @click="inputMode = 'coords'"
            >
              坐标输入
            </button>
          </div>

          <AddressSelector
            v-if="inputMode === 'address'"
            @select="onLocationSelect"
          />

          <div v-else class="coords-form">
            <p class="coords-hint">
              选择坐标来源类型后输入数值，系统会转换为百度 BD-09 并在地图定位。
              可从
              <a href="https://api.map.baidu.com/lbsapi/getpoint/index.html" target="_blank" rel="noopener">百度坐标拾取器</a>
              复制粘贴。
            </p>
            <div class="field">
              <label for="coord-system">坐标来源</label>
              <select id="coord-system" v-model="coordInputSystem">
                <option value="bd09">百度 BD-09（百度地图 / 拾取器）</option>
                <option value="gcj02">国测局 GCJ-02（高德 / 腾讯地图）</option>
                <option value="wgs84">GPS WGS84（谷歌地球 / OpenStreetMap）</option>
              </select>
            </div>
            <div class="field">
              <label for="coord-paste">快速粘贴</label>
              <div class="paste-row">
                <input
                  id="coord-paste"
                  v-model="coordPaste"
                  type="text"
                  placeholder="粘贴 116.481,39.996 或 39.996,116.481"
                />
                <button type="button" class="paste-btn" @click="fillFromPaste">填入</button>
              </div>
            </div>
            <div class="coords-row">
              <div class="field">
                <label for="coord-lat">纬度 Lat</label>
                <input
                  id="coord-lat"
                  v-model.number="coordLat"
                  type="number"
                  step="0.000001"
                  placeholder="如 39.996000"
                />
              </div>
              <div class="field">
                <label for="coord-lng">经度 Lng</label>
                <input
                  id="coord-lng"
                  v-model.number="coordLng"
                  type="number"
                  step="0.000001"
                  placeholder="如 116.481000"
                />
              </div>
            </div>
            <button
              type="button"
              class="apply-coords-btn"
              :disabled="isApplyingCoords"
              @click="applyCoords"
            >
              {{ isApplyingCoords ? '转换定位中…' : '应用坐标' }}
            </button>
            <div v-if="selectedLocation && inputMode === 'coords'" class="selected-coords">
              地图定位（BD-09）：{{ selectedLocation.lat.toFixed(6) }}, {{ selectedLocation.lng.toFixed(6) }}
            </div>
          </div>
        </section>

        <section class="module-card">
          <h2 class="module-title">
            <span class="module-num">2</span>
            开始体检
          </h2>
          <p class="module-desc">
            确认分析位置后，系统将基于百度真实路网计算 15 分钟步行等时圈并生成报告。
          </p>
          <button
            class="analyze-btn"
            :disabled="store.isLoading || !selectedLocation"
            @click="startAnalysis"
          >
            {{ store.isLoading ? '体检中…' : '开始体检' }}
          </button>
          <p v-if="error" class="error" role="alert">{{ error }}</p>
          <AnalysisProgress
            :is-loading="store.isLoading"
            :progress="store.currentTask?.progress ?? null"
            :stage="store.currentTask?.progress?.stage"
          />
        </section>
      </aside>

      <section class="map-area" aria-label="地图">
        <h2 class="map-title">地图预览</h2>
        <MapPanel
          :center="selectedLocation"
          :report-data="store.reportData"
          :visible-categories="visibleCategories"
          :map-coord-system="mapCoordSystem"
          @mousemove="onMapMousemove"
          @mouseleave="onMapMouseleave"
        />
        <CoordDisplay
          :mouse-coords="mouseCoords"
          :center-coords="selectedLocation"
          :display-coord-system="mapCoordSystem"
        />
        <PoiLegend
          v-if="store.reportData"
          :categories="store.reportData.categories"
          :visible="visibleCategories"
          @update:visible="visibleCategories = $event"
        />
      </section>
    </div>

    <section
      id="results-section"
      class="results-section"
      :class="{ visible: store.reportData }"
      aria-label="体检结果"
    >
      <h2 class="section-title">
        <span class="module-num">3</span>
        体检结果
      </h2>

      <div v-if="!store.reportData && !store.isLoading" class="results-empty">
        完成体检后，此处将显示评分、图表与 AI 报告
      </div>

      <ReportPanel
        v-if="store.reportData"
        :report-data="store.reportData"
        :ai-report="store.aiReport"
      />
    </section>
  </div>
</template>

<style scoped>
.dashboard {
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
}

.hero {
  padding: 1.25rem 1.25rem 1rem;
  border-bottom: 1px solid rgba(184, 149, 107, 0.15);
}

.eyebrow {
  font-family: var(--font-mono);
  font-size: 0.6875rem;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: var(--jade-glow);
  margin-bottom: 0.375rem;
}

.hero h1 {
  font-family: var(--font-display);
  font-size: clamp(1.5rem, 4vw, 2.25rem);
  font-weight: 700;
  color: var(--white);
}

.subtitle {
  margin-top: 0.375rem;
  color: var(--mist);
  font-size: 0.875rem;
}

.workspace {
  display: grid;
  grid-template-columns: 1fr;
  gap: 0;
  flex: 1;
}

@media (min-width: 900px) {
  .workspace {
    grid-template-columns: 380px 1fr;
    min-height: 480px;
  }
}

.sidebar {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding: 1rem;
  background: var(--ink-soft);
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
}

@media (min-width: 900px) {
  .sidebar {
    border-bottom: none;
    border-right: 1px solid rgba(255, 255, 255, 0.06);
    overflow-y: auto;
    max-height: calc(100dvh - 140px);
  }
}

.module-card {
  background: var(--ink);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: var(--radius-md);
  padding: 1rem;
}

.module-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-family: var(--font-display);
  font-size: 1rem;
  color: var(--white);
  margin-bottom: 1rem;
}

.module-num {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.5rem;
  height: 1.5rem;
  background: var(--jade);
  color: var(--white);
  font-family: var(--font-mono);
  font-size: 0.75rem;
  border-radius: 50%;
}

.mode-tabs {
  display: flex;
  gap: 0.25rem;
  margin-bottom: 1rem;
}

.mode-tabs button {
  flex: 1;
  padding: 0.5rem;
  font-size: 0.8125rem;
  color: var(--mist);
  border-radius: var(--radius-sm);
  border: 1px solid transparent;
}

.mode-tabs button.active {
  color: var(--paper);
  background: rgba(45, 106, 90, 0.2);
  border-color: var(--jade);
}

.coords-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.coords-hint {
  font-size: 0.8125rem;
  color: var(--mist);
  line-height: 1.5;
}

.coords-hint a {
  color: var(--jade-glow);
  text-decoration: underline;
}

.coords-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.75rem;
}

.paste-row {
  display: flex;
  gap: 0.5rem;
}

.paste-row input {
  flex: 1;
}

.paste-btn {
  flex-shrink: 0;
  padding: 0.5rem 0.75rem;
  background: rgba(45, 106, 90, 0.25);
  border: 1px solid var(--jade);
  color: var(--paper);
  border-radius: var(--radius-sm);
  font-size: 0.8125rem;
}

.coords-form .field select {
  width: 100%;
  padding: 0.625rem 0.75rem;
  background: var(--ink-soft);
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: var(--radius-sm);
  color: var(--paper);
  font-size: 0.9375rem;
}

.coords-form .field input {
  width: 100%;
  padding: 0.625rem 0.75rem;
  background: var(--ink-soft);
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: var(--radius-sm);
  color: var(--paper);
  font-size: 0.9375rem;
  font-family: var(--font-mono);
}

.apply-coords-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.field label {
  display: block;
  font-size: 0.8125rem;
  color: var(--mist);
  margin-bottom: 0.375rem;
}

.field input {
  width: 100%;
  padding: 0.625rem 0.75rem;
  background: var(--ink-soft);
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: var(--radius-sm);
  color: var(--paper);
  font-size: 0.9375rem;
  font-family: var(--font-mono);
}

.apply-coords-btn {
  padding: 0.625rem;
  background: rgba(45, 106, 90, 0.25);
  border: 1px solid var(--jade);
  color: var(--paper);
  border-radius: var(--radius-sm);
  font-size: 0.875rem;
}

.apply-coords-btn:hover {
  background: rgba(45, 106, 90, 0.4);
}

.selected-coords {
  font-size: 0.8125rem;
  color: var(--jade-glow);
  font-family: var(--font-mono);
}

.module-desc {
  font-size: 0.8125rem;
  color: var(--mist);
  margin-bottom: 1rem;
}

.analyze-btn {
  width: 100%;
  padding: 0.875rem;
  background: var(--jade);
  color: var(--white);
  font-weight: 600;
  font-size: 0.9375rem;
  border-radius: var(--radius-sm);
  transition: background 0.2s;
}

.analyze-btn:hover:not(:disabled) {
  background: var(--jade-glow);
}

.analyze-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.error {
  margin-top: 0.75rem;
  color: var(--coral);
  font-size: 0.875rem;
}

.map-area {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 1rem;
  min-height: 400px;
}

.map-title {
  font-size: 0.8125rem;
  color: var(--mist);
}

.results-section {
  padding: 1.25rem;
  border-top: 1px solid rgba(184, 149, 107, 0.15);
  background: var(--ink-soft);
  min-height: 200px;
}

.results-section.visible {
  min-height: auto;
}

.section-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-family: var(--font-display);
  font-size: 1.125rem;
  color: var(--white);
  margin-bottom: 1.25rem;
}

.results-empty {
  padding: 2rem;
  text-align: center;
  color: var(--mist);
  font-size: 0.875rem;
  border: 1px dashed rgba(255, 255, 255, 0.1);
  border-radius: var(--radius-md);
}
</style>
