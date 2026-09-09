import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { AgentTaskResult, QuotaStatus, ReportData } from '../types'

export const useAnalysisStore = defineStore('analysis', () => {
  const currentTask = ref<AgentTaskResult | null>(null)
  const reportData = ref<ReportData | null>(null)
  const aiReport = ref<string | null>(null)
  const isLoading = ref(false)
  const progressMessage = ref('')
  const quotaStatus = ref<QuotaStatus[]>([])

  function setTask(task: AgentTaskResult) {
    currentTask.value = task
    progressMessage.value = task.progress?.message ?? ''
    if (task.reportData) reportData.value = task.reportData
    if (task.aiReport) aiReport.value = task.aiReport
  }

  function reset() {
    currentTask.value = null
    reportData.value = null
    aiReport.value = null
    isLoading.value = false
    progressMessage.value = ''
  }

  return {
    currentTask,
    reportData,
    aiReport,
    isLoading,
    progressMessage,
    quotaStatus,
    setTask,
    reset,
  }
})
