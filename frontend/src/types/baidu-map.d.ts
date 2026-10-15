/** 百度地图 JavaScript API GL 最小类型声明 */
declare namespace BMapGL {
  class Point {
    constructor(lng: number, lat: number)
    lng: number
    lat: number
  }

  class Size {
    constructor(width: number, height: number)
  }

  class Icon {
    constructor(url: string, size: Size, opts?: { anchor?: Size; imageSize?: Size })
  }

  class Map {
    constructor(container: HTMLElement | string, opts?: object)
    centerAndZoom(point: Point, zoom: number): void
    setCenter(point: Point): void
    getZoom(): number
    setZoom(zoom: number): void
    enableScrollWheelZoom(enable?: boolean): void
    setMapType(mapTypeId: number): void
    setDisplayOptions(options: Record<string, boolean>): void
    addOverlay(overlay: Overlay): void
    removeOverlay(overlay: Overlay): void
    addEventListener(event: string, handler: (e: MapEvent) => void): void
    setViewport(points: Point[], opts?: { margins?: number[] }): void
    getContainer(): HTMLElement
    openInfoWindow(info: InfoWindow, point: Point): void
    closeInfoWindow(): void
  }

  class Marker extends Overlay {
    constructor(point: Point, opts?: { icon?: Icon })
  }

  class Label extends Overlay {
    constructor(content: string, opts?: { position?: Point; offset?: Size })
    setStyle(styles: Record<string, string>): void
    addEventListener(event: string, handler: (e?: Event) => void): void
  }

  class InfoWindow extends Overlay {
    constructor(content: string, opts?: { width?: number; height?: number; title?: string })
  }

  class Convertor {
    translate(
      points: Point[],
      from: number,
      to: number,
      callback: (data: ConvertResult) => void,
    ): void
  }

  interface ConvertResult {
    status: number
    points: Point[]
  }

  class Polygon extends Overlay {
    constructor(points: Point[], opts?: PolygonOptions)
  }

  interface PolygonOptions {
    strokeColor?: string
    strokeWeight?: number
    strokeOpacity?: number
    fillColor?: string
    fillOpacity?: number
    strokeStyle?: string
  }

  interface MapEvent {
    latlng: Point
    point: Point
    domEvent?: Event
    stopPropagation?: () => void
  }

  abstract class Overlay {
    addEventListener(event: string, handler: (e?: Event) => void): void
    setZIndex?(zIndex: number): void
  }

  const BMAP_NORMAL_MAP: number
  const BMAP_SATELLITE_MAP: number
  const BMAP_EARTH_MAP: number
}

interface Window {
  BMapGL: typeof BMapGL
  /** 百度地图 GL 动态加载 callback（见 loadBaiduMap.ts） */
  __baiduMapGlInit__?: () => void
}
