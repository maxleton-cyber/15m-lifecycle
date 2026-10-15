export type CoordSystem = 'bd09' | 'gcj02' | 'wgs84'

export interface LatLng {
  lat: number
  lng: number
}

export interface GeocodeResult {
  lat: number
  lng: number
  formattedAddress: string
  coordSystem?: CoordSystem
}

export interface PlaceSuggestion {
  name: string
  address: string
  lat: number
  lng: number
  district?: string
  tag?: string
  coordSystem?: CoordSystem
}

export interface RegionNode {
  code: string
  name: string
  children?: RegionNode[]
}

export interface SelectedLocation {
  lat: number
  lng: number
  address: string
  name: string
  coordSystem?: CoordSystem
}

export interface IsochroneResult {
  polygonId: string
  center: LatLng
  minutes: number
  polygon: LatLng[]
  radiusMeters: number
}

export interface PoiItem {
  id: string
  name: string
  category: string
  lat: number
  lng: number
  address: string
  distanceMeters: number
}

export interface CategoryCoverage {
  category: string
  label: string
  count: number
  score: number
  isBlindSpot: boolean
  nearestDistanceMeters: number | null
  pois: PoiItem[]
}

export interface BlindSpotInfo {
  category: string
  label: string
  isBlindSpot: boolean
  nearestDistanceMeters: number | null
  suggestion: string
}

export interface BlindAreaRegion {
  id: string
  level: number
  missingCategories: string[]
  polygon: LatLng[]
  areaM2: number
  degraded: boolean
}

export interface BlindAreaAnalysis {
  blindRegions: BlindAreaRegion[]
  blindAreaRatio: number
  averageMissingCount: number
}

export interface ReportData {
  polygonId: string
  center: LatLng
  isochrone: IsochroneResult
  categories: CategoryCoverage[]
  blindSpots: BlindSpotInfo[]
  overallScore: number
  blindArea?: BlindAreaAnalysis | null
}

export type AgentTaskStatus = 'Queued' | 'Processing' | 'Completed' | 'Failed'

export interface AgentTaskProgress {
  stage: string
  message: string
}

export interface AgentTaskResult {
  taskId: string
  status: AgentTaskStatus
  progress: AgentTaskProgress | null
  reportData: ReportData | null
  aiReport: string | null
  error: string | null
  createdAt: string
  completedAt: string | null
}

export interface AgentTaskRequest {
  lat?: number
  lng?: number
  address?: string
}
