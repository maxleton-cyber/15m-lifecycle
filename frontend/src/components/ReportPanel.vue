<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import * as echarts from 'echarts'
import type { ReportData } from '../types'

const props = defineProps<{
  reportData: ReportData
  aiReport: string | null
}>()

const radarRef = ref<HTMLDivElement | null>(null)
const barRef = ref<HTMLDivElement | null>(null)
let radarChart: echarts.ECharts | null = null
let barChart: echarts.ECharts | null = null

const blindCount = computed(
  () => props.reportData.blindSpots.filter((b) => b.isBlindSpot).length,
)

function renderCharts() {
  const cats = props.reportData.categories

  if (radarRef.value) {
    radarChart?.dispose()
    radarChart = echarts.init(radarRef.value)
    radarChart.setOption({
      backgroundColor: 'transparent',
      radar: {
        indicator: cats.map((c) => ({ name: c.label, max: 100 })),
        axisName: { color: '#7a8b96', fontSize: 11 },
        splitLine: { lineStyle: { color: 'rgba(255,255,255,0.08)' } },
        axisLine: { lineStyle: { color: 'rgba(255,255,255,0.1)' } },
      },
      series: [
        {
          type: 'radar',
          data: [{ value: cats.map((c) => c.score), name: '覆盖度' }],
          areaStyle: { color: 'rgba(45, 106, 90, 0.35)' },
          lineStyle: { color: '#3d8a72' },
          itemStyle: { color: '#b8956b' },
        },
      ],
    })
  }

  if (barRef.value) {
    barChart?.dispose()
    barChart = echarts.init(barRef.value)
    barChart.setOption({
      backgroundColor: 'transparent',
      grid: { left: 48, right: 16, top: 16, bottom: 40 },
      xAxis: {
        type: 'category',
        data: cats.map((c) => c.label),
        axisLabel: { color: '#7a8b96', fontSize: 10, rotate: 25 },
        axisLine: { lineStyle: { color: 'rgba(255,255,255,0.1)' } },
      },
      yAxis: {
        type: 'value',
        axisLabel: { color: '#7a8b96' },
        splitLine: { lineStyle: { color: 'rgba(255,255,255,0.06)' } },
      },
      series: [
        {
          type: 'bar',
          data: cats.map((c) => ({
            value: c.count,
            itemStyle: { color: c.isBlindSpot ? '#c45c3e' : '#3d8a72' },
          })),
        },
      ],
    })
  }
}

function formatReport(text: string) {
  return text
    .replace(/^## (.+)$/gm, '<h4>$1</h4>')
    .replace(/^- (.+)$/gm, '<li>$1</li>')
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/\n\n/g, '</p><p>')
}

onMounted(() => {
  renderCharts()
  window.addEventListener('resize', renderCharts)
})

onUnmounted(() => {
  radarChart?.dispose()
  barChart?.dispose()
  window.removeEventListener('resize', renderCharts)
})

watch(() => props.reportData, renderCharts, { deep: true })
</script>

<template>
  <div class="report-panel">
    <div class="score-row">
      <div class="score-card">
        <span class="score-num">{{ reportData.overallScore.toFixed(0) }}</span>
        <span class="score-text">综合评分</span>
      </div>
      <div class="stat-cards">
        <div class="stat">
          <span class="stat-num">{{ reportData.categories.length }}</span>
          <span class="stat-label">设施类别</span>
        </div>
        <div class="stat" :class="{ warn: blindCount > 0 }">
          <span class="stat-num">{{ blindCount }}</span>
          <span class="stat-label">盲区类别</span>
        </div>
      </div>
    </div>

    <div class="charts-row">
      <div class="chart-box">
        <h4>覆盖度雷达</h4>
        <div ref="radarRef" class="chart" />
      </div>
      <div class="chart-box">
        <h4>设施数量</h4>
        <div ref="barRef" class="chart" />
      </div>
    </div>

    <div class="blind-list">
      <h4>盲区诊断</h4>
      <ul>
        <li
          v-for="spot in reportData.blindSpots"
          :key="spot.category"
          :class="{ blind: spot.isBlindSpot }"
        >
          <span class="spot-label">{{ spot.label }}</span>
          <span class="spot-status">
            {{ spot.isBlindSpot ? '盲区' : '达标' }}
          </span>
          <p v-if="spot.isBlindSpot" class="spot-suggestion">{{ spot.suggestion }}</p>
        </li>
      </ul>
    </div>

    <article v-if="aiReport" class="ai-report">
      <h4>AI 体检报告</h4>
      <div class="report-body" v-html="formatReport(aiReport)" />
    </article>
  </div>
</template>

<style scoped>
.report-panel {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.score-row {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  align-items: stretch;
}

.score-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-width: 120px;
  padding: 1rem 1.5rem;
  background: rgba(184, 149, 107, 0.1);
  border: 1px solid rgba(184, 149, 107, 0.3);
  border-radius: var(--radius-md);
}

.score-num {
  font-family: var(--font-display);
  font-size: 2.5rem;
  font-weight: 700;
  color: var(--gold);
  line-height: 1;
}

.score-text {
  font-size: 0.75rem;
  color: var(--mist);
  margin-top: 0.25rem;
}

.stat-cards {
  display: flex;
  gap: 0.75rem;
  flex: 1;
}

.stat {
  flex: 1;
  padding: 1rem;
  background: var(--ink);
  border-radius: var(--radius-md);
  border: 1px solid rgba(255, 255, 255, 0.06);
  text-align: center;
}

.stat.warn {
  border-color: rgba(196, 92, 62, 0.4);
}

.stat-num {
  display: block;
  font-family: var(--font-mono);
  font-size: 1.5rem;
  color: var(--paper);
}

.stat.warn .stat-num {
  color: var(--coral);
}

.stat-label {
  font-size: 0.75rem;
  color: var(--mist);
}

.charts-row {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1rem;
}

@media (min-width: 768px) {
  .charts-row {
    grid-template-columns: 1fr 1fr;
  }
}

.chart-box {
  background: var(--ink);
  border-radius: var(--radius-md);
  padding: 1rem;
  border: 1px solid rgba(255, 255, 255, 0.06);
}

.chart-box h4,
.blind-list h4,
.ai-report h4 {
  font-family: var(--font-display);
  font-size: 0.9375rem;
  color: var(--paper);
  margin-bottom: 0.75rem;
}

.chart {
  height: 240px;
}

.blind-list ul {
  list-style: none;
  display: grid;
  gap: 0.5rem;
}

.blind-list li {
  padding: 0.75rem;
  background: var(--ink);
  border-radius: var(--radius-sm);
  border: 1px solid rgba(255, 255, 255, 0.06);
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 0.25rem 1rem;
}

.blind-list li.blind {
  border-color: rgba(196, 92, 62, 0.35);
}

.spot-label {
  font-weight: 500;
}

.spot-status {
  font-size: 0.75rem;
  color: var(--jade-glow);
}

.blind-list li.blind .spot-status {
  color: var(--coral);
}

.spot-suggestion {
  grid-column: 1 / -1;
  font-size: 0.8125rem;
  color: var(--mist);
}

.ai-report {
  background: var(--ink);
  border-radius: var(--radius-md);
  padding: 1.25rem;
  border: 1px solid rgba(45, 106, 90, 0.25);
}

.report-body :deep(h4) {
  color: var(--jade-glow);
  margin: 1rem 0 0.5rem;
  font-size: 0.9375rem;
}

.report-body :deep(p),
.report-body :deep(li) {
  font-size: 0.875rem;
  line-height: 1.7;
  color: var(--paper);
  margin-bottom: 0.5rem;
}

.report-body :deep(strong) {
  color: var(--gold);
}
</style>
