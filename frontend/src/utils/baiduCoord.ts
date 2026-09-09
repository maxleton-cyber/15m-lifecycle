import { loadBaiduMap } from './loadBaiduMap'
import { toBd09, type CoordSystem } from './coord'
import type { SelectedLocation } from '../types'

/** 百度坐标转换常量（BMapGL.Convertor） */
const COORD_WGS84 = 1
const COORD_GCJ02 = 3
const COORD_BD09 = 5

const FROM_TYPE: Record<Exclude<CoordSystem, 'bd09'>, number> = {
  wgs84: COORD_WGS84,
  gcj02: COORD_GCJ02,
}

/** 将任意国内常用坐标系转为百度 BD-09（优先调用百度官方 Convertor） */
export async function convertToBd09(
  lng: number,
  lat: number,
  from: CoordSystem,
): Promise<{ lng: number; lat: number }> {
  if (from === 'bd09') return { lng, lat }

  try {
    await loadBaiduMap()
    if (window.BMapGL?.Convertor) {
      const result = await translateWithBaidu(lng, lat, FROM_TYPE[from])
      if (result) return result
    }
  } catch {
    /* fallback to local algorithm */
  }

  const [bdLng, bdLat] = toBd09(lng, lat, from)
  return { lng: bdLng, lat: bdLat }
}

function translateWithBaidu(
  lng: number,
  lat: number,
  fromType: number,
): Promise<{ lng: number; lat: number } | null> {
  return new Promise((resolve) => {
    const convertor = new BMapGL.Convertor()
    convertor.translate(
      [new BMapGL.Point(lng, lat)],
      fromType,
      COORD_BD09,
      (data) => {
        if (data.status === 0 && data.points.length > 0) {
          resolve({ lng: data.points[0].lng, lat: data.points[0].lat })
        } else {
          resolve(null)
        }
      },
    )
  })
}

/** 统一规范为 BD-09，供百度地图与后端 Web API 使用 */
export async function normalizeLocation(loc: SelectedLocation): Promise<SelectedLocation> {
  const system = loc.coordSystem ?? 'bd09'
  if (system === 'bd09') return loc

  const { lng, lat } = await convertToBd09(loc.lng, loc.lat, system)
  return {
    ...loc,
    lat,
    lng,
    coordSystem: 'bd09',
    address: loc.address || `BD-09 ${lat.toFixed(6)}, ${lng.toFixed(6)}`,
  }
}

/** 解析粘贴的坐标字符串，支持 "经度,纬度" 或 "纬度,经度"（按数值范围自动判断） */
export function parseCoordText(text: string): { lat: number; lng: number } | null {
  const cleaned = text.trim().replace(/[，、\s]+/g, ',')
  const parts = cleaned.split(',').map((s) => parseFloat(s.trim()))
  if (parts.length < 2 || parts.some((n) => Number.isNaN(n))) return null

  const [a, b] = parts
  // 中国范围：经度 73~135，纬度 3~54
  const aIsLng = a >= 70 && a <= 140
  const bIsLat = b >= 2 && b <= 56
  const aIsLat = a >= 2 && a <= 56
  const bIsLng = b >= 70 && b <= 140

  if (aIsLng && bIsLat) return { lng: a, lat: b }
  if (aIsLat && bIsLng) return { lng: b, lat: a }
  // 默认按 纬度,经度（表单字段顺序）
  return { lat: a, lng: b }
}
