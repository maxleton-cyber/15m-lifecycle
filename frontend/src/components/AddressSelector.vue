<script setup lang="ts">
import { ref } from 'vue'
import { suggestPlaces } from '../api'
import type { PlaceSuggestion, SelectedLocation } from '../types'

const emit = defineEmits<{
  select: [location: SelectedLocation]
}>()

const addressQuery = ref('')
const suggestions = ref<PlaceSuggestion[]>([])
const selected = ref<PlaceSuggestion | null>(null)
const isSearching = ref(false)
const searchError = ref<string | null>(null)
const showSuggestions = ref(false)
let searchTimer: ReturnType<typeof setTimeout> | null = null

function onAddressInput() {
  if (searchTimer) clearTimeout(searchTimer)
  const q = addressQuery.value.trim()
  if (!q) {
    suggestions.value = []
    searchError.value = null
    return
  }
  searchTimer = setTimeout(searchAddress, 300)
}

async function searchAddress() {
  const q = addressQuery.value.trim()
  if (!q) return

  isSearching.value = true
  showSuggestions.value = true
  searchError.value = null
  try {
    suggestions.value = await suggestPlaces(q)
  } catch (err: unknown) {
    suggestions.value = []
    if (typeof err === 'object' && err !== null && 'response' in err) {
      const resp = (err as { response?: { data?: unknown; status?: number } }).response
      const data = resp?.data
      searchError.value =
        typeof data === 'string' && data.length > 0
          ? data
          : typeof data === 'object' && data !== null && 'title' in data
            ? String((data as { title?: string }).title)
            : `搜索失败（HTTP ${resp?.status ?? '未知'}），请确认后端已启动且已配置 BAIDU_MAP_AK`
    } else if (err instanceof Error) {
      searchError.value = err.message.includes('Network Error')
        ? '无法连接后端，请确认服务已在 http://localhost:5280 启动'
        : err.message
    } else {
      searchError.value = '搜索失败，请稍后重试'
    }
  } finally {
    isSearching.value = false
  }
}

function pickSuggestion(item: PlaceSuggestion) {
  selected.value = item
  addressQuery.value = item.name
  showSuggestions.value = false
  emit('select', {
    lat: item.lat,
    lng: item.lng,
    address: item.address,
    name: item.name,
    coordSystem: item.coordSystem ?? 'bd09',
  })
}

function onBlur() {
  setTimeout(() => {
    showSuggestions.value = false
  }, 200)
}
</script>

<template>
  <div class="address-selector">
    <div class="field address-field">
      <label for="address">地址</label>
      <input
        id="address"
        v-model="addressQuery"
        type="search"
        placeholder="输入地址、小区或地标名称，从列表中选择"
        autocomplete="off"
        @input="onAddressInput"
        @focus="showSuggestions = suggestions.length > 0 || isSearching"
        @blur="onBlur"
      />
      <p class="address-hint">支持全国范围搜索，如「北京望京SOHO」「上海陆家嘴」</p>

      <ul
        v-if="showSuggestions && (suggestions.length > 0 || isSearching || addressQuery.trim())"
        class="suggest-list"
        role="listbox"
      >
        <li v-if="isSearching" class="suggest-item loading">搜索中…</li>
        <li
          v-for="(item, i) in suggestions"
          :key="`${item.name}-${i}`"
          class="suggest-item"
          role="option"
          :aria-selected="selected?.name === item.name"
          @mousedown.prevent="pickSuggestion(item)"
        >
          <span class="suggest-name">{{ item.name }}</span>
          <span class="suggest-addr">{{ item.address }}</span>
          <span v-if="item.tag" class="suggest-tag">{{ item.tag }}</span>
        </li>
        <li v-if="!isSearching && searchError" class="suggest-item error">
          {{ searchError }}
        </li>
        <li
          v-else-if="!isSearching && suggestions.length === 0 && addressQuery.trim()"
          class="suggest-item empty"
        >
          未找到匹配地点，请更换关键词
        </li>
      </ul>
    </div>

    <div v-if="selected" class="selected-card">
      <span class="selected-label">已选位置</span>
      <strong>{{ selected.name }}</strong>
      <span class="selected-addr">{{ selected.address }}</span>
    </div>
  </div>
</template>

<style scoped>
.address-selector {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.field label {
  display: block;
  font-size: 0.8125rem;
  color: var(--mist);
  margin-bottom: 0.375rem;
}

.field input {
  width: 100%;
  padding: 0.625rem 0.75rem;
  background: var(--ink);
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: var(--radius-sm);
  color: var(--paper);
  font-size: 0.9375rem;
}

.address-field {
  position: relative;
}

.address-hint {
  margin-top: 0.375rem;
  font-size: 0.75rem;
  color: var(--mist);
}

.suggest-list {
  position: absolute;
  z-index: 100;
  top: 100%;
  left: 0;
  right: 0;
  margin-top: 0.25rem;
  max-height: 280px;
  overflow-y: auto;
  background: var(--ink-soft);
  border: 1px solid rgba(45, 106, 90, 0.4);
  border-radius: var(--radius-sm);
  box-shadow: var(--shadow);
  list-style: none;
}

.suggest-item {
  padding: 0.625rem 0.75rem;
  cursor: pointer;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

.suggest-item:hover,
.suggest-item[aria-selected='true'] {
  background: rgba(45, 106, 90, 0.2);
}

.suggest-item.loading,
.suggest-item.empty {
  color: var(--mist);
  cursor: default;
}

.suggest-item.error {
  color: #f0a080;
  cursor: default;
  line-height: 1.4;
}

.suggest-name {
  font-weight: 500;
  color: var(--white);
}

.suggest-addr {
  font-size: 0.75rem;
  color: var(--mist);
}

.suggest-tag {
  font-size: 0.6875rem;
  color: var(--gold);
}

.selected-card {
  padding: 0.75rem;
  background: rgba(45, 106, 90, 0.12);
  border: 1px solid var(--jade);
  border-radius: var(--radius-sm);
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.selected-label {
  font-size: 0.6875rem;
  font-family: var(--font-mono);
  color: var(--jade-glow);
  text-transform: uppercase;
  letter-spacing: 0.08em;
}

.selected-addr {
  font-size: 0.8125rem;
  color: var(--mist);
}
</style>
