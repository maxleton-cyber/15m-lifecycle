<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue'
import { createPoiIconHtml, getCategoryMeta } from '../constants/poiCategories'
import { hasBaiduMapAk, loadBaiduMap } from '../utils/loadBaiduMap'
import { toBd09, type CoordSystem } from '../utils/coord'
import type { CategoryCoverage, LatLng, ReportData, SelectedLocation } from '../types'

const props = defineProps<{
  center: SelectedLocation | null
  reportData: ReportData | null
  visibleCategories?: string[]
  mapCoordSystem?: CoordSystem
}>()

const emit = defineEmits<{
  mousemove: [coords: LatLng]
  mouseleave: []
}>()

const mapContainer = ref<HTMLDivElement | null>(null)
const mapError = ref('')
const mapReady = ref(false)

let map: BMapGL.Map | null = null
let centerOverlays: BMapGL.Overlay[] = []
let isoPolygon: BMapGL.Polygon | null = null
const poiOverlays = new Map<string, BMapGL.Overlay[]>()

function escapeHtml(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

function getCoordSystem(): CoordSystem {
  return props.center?.coordSystem ?? props.mapCoordSystem ?? 'bd09'
}

/** 统一转为百度 BD-09 坐标（输入应已规范为 bd09，保留转换以兼容地址搜索） */
function toBaiduPoint(lat: number, lng: number, system?: CoordSystem): BMapGL.Point {
  const coordSystem = system ?? getCoordSystem()
  const [bdLng, bdLat] = toBd09(lng, lat, coordSystem)
  return new BMapGL.Point(bdLng, bdLat)
}

function pointToLatLng(point: BMapGL.Point): LatLng {
  return { lat: point.lat, lng: point.lng }
}

function isCoordOnlyLocation(loc: SelectedLocation): boolean {
  return (
    loc.name.startsWith('坐标点') ||
    /^BD-09\s/i.test(loc.address) ||
    /^坐标\s*[\d.,\s-]+$/i.test(loc.address)
  )
}

function createCenterLabelHtml(loc: SelectedLocation): string | null {
  if (isCoordOnlyLocation(loc)) return null
  const name = escapeHtml(loc.name)
  const addr = escapeHtml(loc.address)
  return `<div class="location-card">
    <div class="location-card-title">${name}</div>
    <div class="location-card-addr">${addr}</div>
  </div>`
}

function clearCenterOverlays() {
  if (!map) return
  centerOverlays.forEach((o) => map!.removeOverlay(o))
  centerOverlays = []
}

function bindMapEvents() {
  if (!map) return

  map.addEventListener('mousemove', (e) => {
    if (e.latlng) emit('mousemove', pointToLatLng(e.latlng))
  })
  map.addEventListener('mouseout', () => emit('mouseleave'))
}

async function initMap() {
  if (!mapContainer.value || map) return

  if (!hasBaiduMapAk()) {
    mapError.value =
      '请在项目根目录 .env 中配置 VITE_BAIDU_MAP_AK（百度「浏览器端」应用 AK，与服务端 BAIDU_MAP_AK 不同）'
    return
  }

  try {
    await loadBaiduMap()
  } catch (err) {
    const msg = err instanceof Error ? err.message : '百度地图加载失败'
    mapError.value = `${msg}。请确认：① AK 为「浏览器端」类型；② Referer 白名单含 http://localhost:5173/*`
    return
  }

  const defaultPoint = props.center
    ? toBaiduPoint(props.center.lat, props.center.lng)
    : new BMapGL.Point(116.481, 39.996)

  map = new BMapGL.Map(mapContainer.value, {
    enableRotate: false,
    enableTilt: false,
  })
  map.centerAndZoom(defaultPoint, 16)
  map.enableScrollWheelZoom(true)

  map.setMapType(BMapGL.BMAP_SATELLITE_MAP)
  map.setDisplayOptions({
    poi: true,
    poiText: true,
    building: true,
    street: true,
  })

  bindMapEvents()
  mapReady.value = true
  updateCenter()
  updateIsochrone()
}

function updateCenter() {
  if (!map) return

  clearCenterOverlays()

  if (!props.center) return

  const loc = props.center
  const point = toBaiduPoint(loc.lat, loc.lng)

  const marker = new BMapGL.Marker(point)
  marker.addEventListener('click', () => {
    map?.closeInfoWindow()
    const info = new BMapGL.InfoWindow(
      `<div class="center-popup">
        <strong>${escapeHtml(loc.name)}</strong>
        ${isCoordOnlyLocation(loc) ? '' : `<p>${escapeHtml(loc.address)}</p>`}
      </div>`,
      { width: 240 },
    )
    map?.openInfoWindow(info, point)
  })
  map.addOverlay(marker)
  centerOverlays.push(marker)

  const labelHtml = createCenterLabelHtml(loc)
  if (labelHtml) {
    const label = new BMapGL.Label(labelHtml, {
      position: point,
      offset: new BMapGL.Size(-100, -72),
    })
    label.setStyle({
      border: 'none',
      background: 'transparent',
      padding: '0',
      pointerEvents: 'none',
    })
    map.addOverlay(label)
    centerOverlays.push(label)
  }

  map.centerAndZoom(point, Math.max(map.getZoom(), 16))
}

function clearPoiOverlays() {
  if (!map) return
  poiOverlays.forEach((overlays) => {
    overlays.forEach((o) => map!.removeOverlay(o))
  })
  poiOverlays.clear()
}

function showPoiInfo(
  point: BMapGL.Point,
  poiName: string,
  categoryLabel: string,
  emoji: string,
  address: string,
) {
  if (!map) return
  map.closeInfoWindow()
  const info = new BMapGL.InfoWindow(
    `<div class="poi-popup">
      <span class="poi-popup-icon">${emoji}</span>
      <div>
        <strong>${escapeHtml(poiName)}</strong><br/>
        <span>${escapeHtml(categoryLabel)}</span><br/>
        <small>${escapeHtml(address)}</small>
      </div>
    </div>`,
    { width: 260 },
  )
  map.openInfoWindow(info, point)
}

function updateIsochrone() {
  if (!map) return

  if (isoPolygon) {
    map.removeOverlay(isoPolygon)
    isoPolygon = null
  }
  clearPoiOverlays()

  const data = props.reportData
  if (!data) return

  const system = getCoordSystem()
  const points = data.isochrone.polygon.map((p) =>
    toBaiduPoint(p.lat, p.lng, system),
  )

  isoPolygon = new BMapGL.Polygon(points, {
    strokeColor: '#7ee8c7',
    strokeWeight: 2,
    strokeOpacity: 0.9,
    fillColor: '#2d6a5a',
    fillOpacity: 0.25,
    strokeStyle: 'dashed',
  })
  map.addOverlay(isoPolygon)

  data.categories.forEach((cat: CategoryCoverage) => {
    const meta = getCategoryMeta(cat.category)
    const overlays: BMapGL.Overlay[] = []

    cat.pois.forEach((poi) => {
      const point = toBaiduPoint(poi.lat, poi.lng, system)

      const marker = new BMapGL.Marker(point)
      marker.addEventListener('click', (e) => {
        e?.stopPropagation?.()
        showPoiInfo(point, poi.name, cat.label, meta.emoji, poi.address)
      })
      overlays.push(marker)

      const iconLabel = new BMapGL.Label(createPoiIconHtml(cat.category, 'map'), {
        position: point,
        offset: new BMapGL.Size(-16, -42),
      })
      iconLabel.setStyle({
        border: 'none',
        background: 'transparent',
        padding: '0',
        cursor: 'pointer',
        pointerEvents: 'auto',
      })
      iconLabel.addEventListener('click', (e) => {
        e?.stopPropagation?.()
        showPoiInfo(point, poi.name, cat.label, meta.emoji, poi.address)
      })
      overlays.push(iconLabel)
    })

    poiOverlays.set(cat.category, overlays)
  })

  updateVisibility()
  map.setViewport(points, { margins: [48, 48, 48, 48] })
}

function updateVisibility() {
  if (!map) return
  const visible = new Set(props.visibleCategories ?? [])

  poiOverlays.forEach((overlays, category) => {
    overlays.forEach((overlay) => {
      if (visible.has(category)) {
        map!.addOverlay(overlay)
      } else {
        map!.removeOverlay(overlay)
      }
    })
  })
}

onMounted(() => {
  initMap()
})

onUnmounted(() => {
  map = null
  centerOverlays = []
  isoPolygon = null
  poiOverlays.clear()
})

watch(() => props.center, updateCenter, { deep: true })
watch(() => props.reportData, updateIsochrone, { deep: true })
watch(() => props.visibleCategories, updateVisibility, { deep: true })
</script>

<template>
  <div class="map-panel">
    <div ref="mapContainer" class="map-container" />
    <div v-if="mapError" class="map-error" role="alert">
      {{ mapError }}
    </div>
    <div v-else-if="!mapReady && !mapError" class="map-loading">地图加载中…</div>
    <div v-else-if="!center && mapReady" class="map-hint">
      请从左侧选择或输入分析位置
    </div>
  </div>
</template>

<style scoped>
.map-panel {
  position: relative;
  width: 100%;
  height: 100%;
  min-height: 400px;
  border-radius: var(--radius-md);
  overflow: hidden;
  border: 1px solid rgba(255, 255, 255, 0.08);
}

.map-container {
  width: 100%;
  height: 100%;
  min-height: inherit;
  background: #1a2332;
}

.map-hint,
.map-loading,
.map-error {
  position: absolute;
  top: 0.75rem;
  left: 50%;
  transform: translateX(-50%);
  z-index: 500;
  padding: 0.5rem 0.875rem;
  background: rgba(12, 21, 32, 0.9);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: var(--radius-sm);
  color: var(--mist);
  font-size: 0.75rem;
  max-width: 90%;
  text-align: center;
}

.map-error {
  color: var(--coral);
  border-color: rgba(196, 92, 62, 0.4);
}
</style>

<style>
/* 分析中心标注 */
.location-card {
  min-width: 200px;
  max-width: 260px;
  padding: 0.5rem 0.625rem;
  background: rgba(12, 21, 32, 0.92);
  border: 2px solid #b8956b;
  border-radius: 8px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.45);
  pointer-events: none;
}

.location-card-title {
  font-size: 13px;
  font-weight: 600;
  color: #fff;
  line-height: 1.3;
}

.location-card-addr {
  margin-top: 0.25rem;
  font-size: 11px;
  color: #a8b8c4;
  line-height: 1.35;
}

.center-popup strong {
  display: block;
  margin-bottom: 0.25rem;
  color: #1a2332;
}

.center-popup p {
  margin: 0;
  font-size: 12px;
  color: #4a5568;
  line-height: 1.4;
}

/* POI 图标 */
.poi-marker.poi-icon-map {
  position: relative;
  width: 32px;
  height: 38px;
  filter: drop-shadow(0 3px 6px rgba(0, 0, 0, 0.55));
}

.poi-marker-body {
  position: relative;
  z-index: 2;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(160deg, var(--poi-color, #3d8a72) 0%, #1a2a35 100%);
  border: 2px solid rgba(255, 255, 255, 0.9);
  border-radius: 10px 10px 10px 4px;
  transform: rotate(-6deg);
}

.poi-marker-emoji {
  font-size: 16px;
  line-height: 1;
  transform: rotate(6deg);
}

.poi-marker-pin {
  position: absolute;
  bottom: 0;
  left: 50%;
  transform: translateX(-50%);
  width: 0;
  height: 0;
  border-left: 6px solid transparent;
  border-right: 6px solid transparent;
  border-top: 8px solid var(--poi-color, #3d8a72);
  z-index: 1;
}

.poi-popup {
  display: flex;
  gap: 0.5rem;
  align-items: flex-start;
  color: #1a2332;
}

.poi-popup-icon {
  font-size: 1.25rem;
}
</style>
