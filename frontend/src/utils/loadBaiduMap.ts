const BAIDU_AK = import.meta.env.VITE_BAIDU_MAP_AK as string | undefined
const CALLBACK_NAME = '__baiduMapGlInit__'

let loading: Promise<void> | null = null

export function hasBaiduMapAk(): boolean {
  return Boolean(BAIDU_AK?.trim())
}

function ensureBmapCss() {
  if (document.querySelector('link[data-bmapgl-ui="gl"]')) return
  const link = document.createElement('link')
  link.rel = 'stylesheet'
  link.type = 'text/css'
  link.href = 'https://api.map.baidu.com/res/webgl/10/bmap.css'
  link.setAttribute('data-bmapgl-ui', 'gl')
  document.head.appendChild(link)
}

/** 动态加载百度地图 JS API GL（须使用 callback，不能仅靠 script.onload） */
export function loadBaiduMap(): Promise<void> {
  if (typeof window !== 'undefined' && window.BMapGL) {
    return Promise.resolve()
  }

  if (!hasBaiduMapAk()) {
    return Promise.reject(new Error('未配置 VITE_BAIDU_MAP_AK'))
  }

  if (loading) return loading

  loading = new Promise((resolve, reject) => {
    const win = window as Window & { [key: string]: unknown }

    const cleanup = () => {
      clearTimeout(timer)
      delete win[CALLBACK_NAME]
    }

    const fail = (message: string) => {
      cleanup()
      loading = null
      reject(new Error(message))
    }

    const timer = setTimeout(() => {
      fail('百度地图 API 加载超时，请检查 AK 是否有效、Referer 白名单是否包含当前域名')
    }, 20000)

    win[CALLBACK_NAME] = () => {
      cleanup()
      if (window.BMapGL) {
        ensureBmapCss()
        resolve()
      } else {
        loading = null
        reject(new Error('百度地图 API 初始化失败'))
      }
    }

    const script = document.createElement('script')
    script.type = 'text/javascript'
    script.src =
      `https://api.map.baidu.com/api?v=1.0&type=webgl&ak=${encodeURIComponent(BAIDU_AK!)}&callback=${CALLBACK_NAME}`
    script.onerror = () => {
      fail('百度地图 API 脚本请求失败，请检查网络或 AK 是否有效')
    }
    document.head.appendChild(script)
  })

  return loading
}
