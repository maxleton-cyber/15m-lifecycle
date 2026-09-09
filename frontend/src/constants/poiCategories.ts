export interface PoiCategoryMeta {
  label: string
  emoji: string
  color: string
}

export const POI_CATEGORIES: Record<string, PoiCategoryMeta> = {
  medical: { label: '基础医疗', emoji: '🏥', color: '#3d8a72' },
  pharmacy: { label: '药店', emoji: '💊', color: '#5ba88a' },
  education: { label: '学校', emoji: '🏫', color: '#b8956b' },
  market: { label: '购物', emoji: '🛍️', color: '#c45c3e' },
  elderly: { label: '养老服务', emoji: '👴', color: '#7a6b8a' },
  convenience: { label: '商超', emoji: '🛒', color: '#6b8faa' },
  park: { label: '公园绿地', emoji: '🌳', color: '#4a8f6a' },
  transit: { label: '公共交通', emoji: '🚇', color: '#8a7a5b' },
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
