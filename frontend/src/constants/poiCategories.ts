export interface PoiCategoryMeta {
  label: string
  emoji: string
  color: string
}

/** 7 类民生设施（见 docs/15min-life-circle-blind-area-spec.md） */
export const POI_CATEGORIES: Record<string, PoiCategoryMeta> = {
  medical: { label: '基础医疗', emoji: '🏥', color: '#3d8a72' },
  pharmacy: { label: '药店', emoji: '💊', color: '#5ba88a' },
  education: { label: '学校', emoji: '🏫', color: '#b8956b' },
  market: { label: '购物', emoji: '🛒', color: '#c45c3e' },
  elderly: { label: '养老服务', emoji: '👴', color: '#7a6b8a' },
  park: { label: '公园绿地', emoji: '🌳', color: '#4a8f6a' },
  transit: { label: '公共交通', emoji: '🚇', color: '#8a7a5b' },
}

/** 盲区统一透明度（百度 GL 对逐级透明度支持差，梯度只靠灰度） */
const BLIND_FILL_OPACITY = 0.5
const BLIND_STROKE_OPACITY = 0.65

/**
 * 盲区样式：透明度固定，缺 n 类仅加深灰色（级差拉大）。
 */
export function blindLevelStyle(level: number): {
  fillColor: string
  fillOpacity: number
  strokeColor: string
  strokeOpacity: number
  strokeWeight: number
  /** 图例用 CSS 背景 */
  cssBackground: string
} {
  const n = Math.max(1, Math.min(7, level))
  // 浅灰 → 近黑，级差再拉开
  const grays = [245, 195, 145, 100, 60, 30, 8]
  const gray = grays[n - 1]
  const channel = gray.toString(16).padStart(2, '0')
  const hex = `#${channel}${channel}${channel}`

  return {
    fillColor: hex,
    fillOpacity: BLIND_FILL_OPACITY,
    strokeColor: hex,
    strokeOpacity: BLIND_STROKE_OPACITY,
    strokeWeight: 1,
    cssBackground: `rgba(${gray},${gray},${gray},${BLIND_FILL_OPACITY})`,
  }
}

export function getCategoryMeta(category: string): PoiCategoryMeta {
  return POI_CATEGORIES[category] ?? { label: category, emoji: '📍', color: '#7a8b96' }
}

export function createPoiIconHtml(category: string, size = 'map'): string {
  const meta = getCategoryMeta(category)
  const cls = size === 'legend' ? 'poi-icon-legend' : 'poi-icon-map'
  return `<div class="poi-marker ${cls}" style="--poi-color:${meta.color}">
    <div class="poi-marker-pin"></div>
    <div class="poi-marker-body"><span class="poi-marker-emoji">${meta.emoji}</span></div>
  </div>`
}
