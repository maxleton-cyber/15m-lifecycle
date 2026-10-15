import axios from 'axios'
import type {
  AgentTaskRequest,
  AgentTaskResult,
  GeocodeResult,
  PlaceSuggestion,
  RegionNode,
} from '../types'

const api = axios.create({
  baseURL: '/api',
  timeout: 120000,
})

export async function geocode(address: string): Promise<GeocodeResult> {
  const { data } = await api.get<GeocodeResult>('/geocode', { params: { address } })
  return data
}

export async function suggestPlaces(
  query: string,
  region?: string,
): Promise<PlaceSuggestion[]> {
  const { data } = await api.get<PlaceSuggestion[]>('/geocode/suggest', {
    params: { query, region: region || undefined },
  })
  return data
}

export async function getRegions(parent?: string): Promise<RegionNode[]> {
  const { data } = await api.get<RegionNode[]>('/geocode/regions', {
    params: parent ? { parent } : undefined,
  })
  return data
}

export async function createAgentTask(request: AgentTaskRequest): Promise<string> {
  const { data } = await api.post<{ taskId: string }>('/agent/tasks', request)
  return data.taskId
}

export async function getAgentTask(taskId: string): Promise<AgentTaskResult> {
  const { data } = await api.get<AgentTaskResult>(`/agent/tasks/${taskId}`)
  return data
}

export async function pollAgentTask(
  taskId: string,
  onProgress?: (task: AgentTaskResult) => void,
  intervalMs = 1500,
): Promise<AgentTaskResult> {
  return new Promise((resolve, reject) => {
    const poll = async () => {
      try {
        const task = await getAgentTask(taskId)
        onProgress?.(task)
        if (task.status === 'Completed') {
          resolve(task)
          return
        }
        if (task.status === 'Failed') {
          reject(new Error(task.error ?? '分析失败'))
          return
        }
        setTimeout(poll, intervalMs)
      } catch (err) {
        reject(err)
      }
    }
    poll()
  })
}
