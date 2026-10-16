<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue'
import { blindLevelStyle, createPoiIconHtml, getCategoryMeta } from '../constants/poiCategories'
import { hasBaiduMapAk, loadBaiduMap } from '../utils/loadBaiduMap'
import { toBd09, type CoordSystem } from '../utils/coord'
import type { BlindAreaRegion, CategoryCoverage, LatLng, ReportData, SelectedLocation } from '../types'

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
let blindOverlays: BMapGL.Overlay[] = []
const poiOverlays = new Map<string, BMapGL.Overlay[]>()
/** 点击命中检测用的盲区多边形（图标层在上，格子点击改由地图事件处理） */
let blindHitRegions: BlindAreaRegion[] = []
/** POI 点击后短暂屏蔽盲区弹窗，避免事件穿透 */
let suppressBlindClickUntil = 0
let mapBlindClickBound = false

const Z_ISO = 10
const Z_BLIND = 20
const Z_POI = 500

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
    : new BMapGL.Point(121.5051, 31.2448)

  map = new BMapGL.Map(mapContainer.value, {
    enableRotate: false,
    enableTilt: false,
  })
  map.centerAndZoom(defaultPoint, 16)
  map.enableScrollWheelZoom(true)

  map.setMapType(BMapGL.BMAP_NORMAL_MAP)
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

function clearBlindOverlays() {
  if (!map) return
  blindOverlays.forEach((o) => map!.removeOverlay(o))
  blindOverlays = []
  blindHitRegions = []
}

function setOverlayZ(overlay: BMapGL.Overlay, z: number) {
  overlay.setZIndex?.(z)
}

function pointInBlindRegion(lat: number, lng: number, region: BlindAreaRegion): boolean {
  const polygon = region.polygon
  let inside = false
  for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
    const yi = polygon[i].lat
    const xi = polygon[i].lng
    const yj = polygon[j].lat
    const xj = polygon[j].lng
    const intersect =
      yi > lat !== yj > lat && lng < ((xj - xi) * (lat - yi)) / (yj - yi + 1e-12) + xi
    if (intersect) inside = !inside
  }
  return inside
}

function findBlindRegionAt(lat: number, lng: number): BlindAreaRegion | null {
  // 后添加的格子优先（同点重叠时取最后一个）
  for (let i = blindHitRegions.length - 1; i >= 0; i--) {
    if (pointInBlindRegion(lat, lng, blindHitRegions[i])) return blindHitRegions[i]
  }
  return null
}

function onMapBlindClick(e: BMapGL.MapEvent) {
  if (Date.now() < suppressBlindClickUntil) return
  const pt = e.latlng ?? e.point
  if (!pt) return
  const region = findBlindRegionAt(pt.lat, pt.lng)
  if (!region) return
  const system = getCoordSystem()
  showBlindInfo(region, regionCenterPoint(region, system))
}

function bindMapBlindClick() {
  if (!map || mapBlindClickBound) return
  map.addEventListener('click', onMapBlindClick)
  mapBlindClickBound = true
}

function markPoiClick() {
  suppressBlindClickUntil = Date.now() + 400
}

function regionToPoints(region: BlindAreaRegion, system: CoordSystem): BMapGL.Point[] {
  return region.polygon.map((p) => toBaiduPoint(p.lat, p.lng, system))
}

/** 格子多边形中心（用于信息窗锚点，避免锚在左下角） */
function regionCenterPoint(region: BlindAreaRegion, system: CoordSystem): BMapGL.Point {
  const pts = region.polygon
  if (pts.length === 0) return toBaiduPoint(0, 0, system)
  const lat = pts.reduce((s, p) => s + p.lat, 0) / pts.length
  const lng = pts.reduce((s, p) => s + p.lng, 0) / pts.length
  return toBaiduPoint(lat, lng, system)
}

function showBlindInfo(region: BlindAreaRegion, point: BMapGL.Point) {
  if (!map) return
  map.closeInfoWindow()
  const missing =
    region.missingCategories.length > 0
      ? region.missingCategories.join('、')
      : '—'
  const html = `<div class="poi-popup">
    <div>
      <strong>盲区 · 缺 ${region.level} 类</strong><br/>
      <span>缺失：${escapeHtml(missing)}</span><br/>
      <small>面积约 ${Math.round(region.areaM2)} m²${
        region.degraded ? ' · 含估算距离' : ''
      }</small>
    </div>
  </div>`
  map.openInfoWindow(new BMapGL.InfoWindow(html, { width: 260 }), point)
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
  clearBlindOverlays()
  clearPoiOverlays()

  const data = props.reportData
  if (!data) return

  const system = getCoordSystem()
  const points = data.isochrone.polygon.map((p) =>
    toBaiduPoint(p.lat, p.lng, system),
  )

  // 等时圈：描边 + 极淡填充，避免与盲区灰色混脏
  isoPolygon = new BMapGL.Polygon(points, {
    strokeColor: '#7ee8c7',
    strokeWeight: 2,
    strokeOpacity: 0.9,
    fillColor: '#2d6a5a',
    fillOpacity: 0.06,
    strokeStyle: 'dashed',
  })
  map.addOverlay(isoPolygon)
  setOverlayZ(isoPolygon, Z_ISO)

  // 盲区灰色层：只绘制，不绑定点击（避免盖住设施图标点击）
  blindHitRegions = []
  for (const region of data.blindArea?.blindRegions ?? []) {
    if (region.level < 1) continue
    const polyPoints = regionToPoints(region, system)
    if (polyPoints.length < 3) continue
    const style = blindLevelStyle(region.level)
    const poly = new BMapGL.Polygon(polyPoints, {
      strokeColor: style.strokeColor,
      strokeWeight: style.strokeWeight,
      strokeOpacity: style.strokeOpacity,
      fillColor: style.fillColor,
      fillOpacity: style.fillOpacity,
    })
    const polyAny = poly as BMapGL.Polygon & {
      setFillColor?: (c: string) => void
      setFillOpacity?: (o: number) => void
      setStrokeWeight?: (w: number) => void
    }
    polyAny.setFillColor?.(style.fillColor)
    polyAny.setFillOpacity?.(style.fillOpacity)
    polyAny.setStrokeWeight?.(style.strokeWeight)

    map.addOverlay(poly)
    setOverlayZ(poly, Z_BLIND)
    blindOverlays.push(poly)
    blindHitRegions.push(region)
  }

  bindMapBlindClick()

  // 设施点位（最上层，优先响应点击）
  data.categories.forEach((cat: CategoryCoverage) => {
    const meta = getCategoryMeta(cat.category)
    const overlays: BMapGL.Overlay[] = []

    cat.pois.forEach((poi) => {
      const point = toBaiduPoint(poi.lat, poi.lng, system)

      const marker = new BMapGL.Marker(point)
      marker.addEventListener('click', (e) => {
        markPoiClick()
        e?.domEvent?.stopPropagation?.()
        e?.stopPropagation?.()
        showPoiInfo(point, poi.name, cat.label, meta.emoji, poi.address)
      })
      setOverlayZ(marker, Z_POI)
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
        zIndex: String(Z_POI),
      })
      iconLabel.addEventListener('click', (e) => {
        markPoiClick()
        e?.domEvent?.stopPropagation?.()
        e?.stopPropagation?.()
        showPoiInfo(point, poi.name, cat.label, meta.emoji, poi.address)
      })
      setOverlayZ(iconLabel, Z_POI + 1)
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
        setOverlayZ(overlay, Z_POI)
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
  blindOverlays = []
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
  min-height: 480px;
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
