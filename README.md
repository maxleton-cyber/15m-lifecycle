# 15 分钟生活圈智能体检助手

基于百度地图开放能力与 LLM 的 Web 应用：选定社区中心点后，计算**真实路网 15 分钟步行等时圈**，清洗并统计 7 类民生设施覆盖，在地图上标注覆盖盲区，并生成 AI 体检报告。

[![CI](https://github.com/maxleton-cyber/15m-lifecycle/actions/workflows/ci.yml/badge.svg)](https://github.com/maxleton-cyber/15m-lifecycle/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)

仓库地址：https://github.com/maxleton-cyber/15m-lifecycle

---

## 功能概览

| 能力 | 说明 |
|------|------|
| 选点 | 地址搜索联想 / 坐标粘贴（BD-09、GCJ-02、WGS84） |
| 等时圈 | 百度真实路网步行时长，多方向采样 + 批量算路二分 |
| 设施覆盖 | 7 类民生 POI：召回 → 名称/tag 清洗 → 圈内统计 |
| 盲区标注 | 等时圈内 100m 网格；类别在 1km（直线）内不可达则记缺失；灰色等级 = 缺失类数 |
| 地图交互 | 普通底图、设施图例与盲区灰度右侧可折叠面板、点击格子/设施弹窗 |
| AI 报告 | OpenAI 兼容 LLM；未配置时模板降级 |
| 配额与限流 | 地点检索 / 算路等日配额可配置；批量算路独立 QPS 与分批间隔 |

默认地图中心示例：上海陆家嘴。

---

## 技术栈

| 层级 | 技术 |
|------|------|
| 前端 | Vue 3 + TypeScript + Vite + Pinia + ECharts + 百度地图 JS API GL |
| 后端 | ASP.NET Core 8 Web API |
| 缓存 | Redis（可选；不可用时内存降级） |
| 部署 | Docker Compose + GitHub Actions CI |

本地默认端口：前端 `5173`，后端 `5280`（Compose 中后端多为 `5000`，以 `docker-compose.yml` 为准）。

---

## 设施类别（7 类）

| 类别 | 纳入（摘要） | 清洗要点 |
|------|----------------|----------|
| 基础医疗 | 医院、社区卫生、诊所等（含口腔/中医/妇幼等专科） | 药店单独成类；排除宠物/医美等 |
| 药店 | 药店/药房及连锁品牌（如同仁堂，店名可不含「药店」） | 医院主体不算药店 |
| 学校 | 幼儿园、小学、初中、实验学校等 | 排除高中/大学与培训机构 |
| 购物 | 菜市场、超市、便利店 | 不含商场专卖等 |
| 养老服务 | 养老院、日间照料、养老驿站等 | 排除殡葬/售楼等 |
| 公园绿地 | 公园、口袋公园、公共广场绿地等 | 以 tag/名称为准，不用小区「花园」地址误判 |
| 公共交通 | 公交站、地铁站 | 商户名带「地铁站」的一般排除 |

**重要：分类只使用 POI「名称 + 百度 tag」，不使用详细地址**，避免「XX 幼儿园西南侧」把商户误判成学校。

实现位置：

- 规则：`backend/Services/PoiClassifier.cs`
- 召回词：`backend/Services/BaiduMapClient.cs` → `IsochroneRecallBatches`
- 流水线：`backend/Services/PoiCoverageService.cs`

---

## 等时圈与盲区规则（当前实现）

### 等时圈

- 默认 15 分钟步行，约 24 方向 × 6 轮二分
- 调用百度 `routematrix/v2/walking` 批量算路
- Redis 缓存（键版本会随算法升级变化）

### 类别盲区（报告 / 图例「盲区」角标）

- 相对**体检中心点**
- 该类最近设施直线距离 **> 1000m**（或缺类）→ 该类为盲区

### 地图灰色格子

- 仅在等时圈内铺约 **100m** 网格
- 对每个格子中心检查 7 类：直线距离 **≤ 各类 R（默认 1000m）** 视为覆盖
- `level = 缺失类别数`（1～7）；灰度加深表示缺得更多，透明度统一
- 点击格子显示缺失类别清单；设施图标在盲区层之上

> 规格文档要求路网步行距离；为控制算路量，**当前覆盖判定使用直线距离**。详见 [盲区与类别清洗规格](./docs/15min-life-circle-blind-area-spec.md)。

---

## 项目结构

```
15m-lifecycle/
├── backend/                      # ASP.NET Core API
│   ├── Controllers/              # HTTP 接口
│   ├── Services/                 # 等时圈、POI、清洗、盲区、限流等
│   │   ├── PoiClassifier.cs      # 类别清洗规则
│   │   ├── BlindAreaService.cs   # 网格盲区
│   │   ├── BaiduMapClient.cs     # 百度 Web API
│   │   └── QuotaGuardService.cs  # 日配额
│   ├── Workers/                  # Agent 后台任务
│   └── Configuration/            # 根目录 .env 加载
├── frontend/                     # Vue 3 SPA
│   └── src/
│       ├── components/           # 地图、图例、进度弹窗、报告等
│       ├── constants/            # POI 图例样式 / 盲区色阶
│       └── views/                # 主页面
├── docs/                         # 需求、规格、Agent 说明
├── .github/workflows/            # CI
├── docker-compose.yml
└── .env.example                  # 环境变量模板（勿提交真实 .env）
```

---

## 快速开始

### 1. 克隆与配置

```bash
git clone https://github.com/maxleton-cyber/15m-lifecycle.git
cd 15m-lifecycle
cp .env.example .env
```

编辑 `.env` 填入密钥。**切勿将 `.env` 提交到 Git**。

### 2. 环境变量

| 变量 | 必填 | 说明 |
|------|------|------|
| `BAIDU_MAP_AK` | **是** | 服务端 AK（地理编码 / 地点检索 / 步行批量算路） |
| `VITE_BAIDU_MAP_AK` | **是** | 浏览器端 AK（JS API GL，与服务端不同） |
| `BAIDU_MAP_MAX_QPS` | 否 | 通用 API QPS，默认 `3` |
| `BAIDU_ROUTE_MATRIX_MAX_QPS` | 否 | 批量算路峰值 QPS，默认 `30` |
| `BAIDU_ROUTE_MATRIX_BATCH_INTERVAL_MS` | 否 | 算路批次间隔 ms，默认 `300` |
| `BAIDU_POI_SEARCH_DAILY_LIMIT` | 否 | 地点检索日配额，默认 `5000` |
| `BAIDU_GEOCODE_DAILY_LIMIT` | 否 | 地理编码日配额，默认 `5000` |
| `BAIDU_ROUTE_MATRIX_DAILY_LIMIT` | 否 | 批量算路日配额，默认 `5000` |
| `LLM_API_BASE_URL` | 否 | OpenAI 兼容根地址（需含 `/v1`） |
| `LLM_API_KEY` | 否 | LLM Key |
| `LLM_MODEL` | 否 | 模型名 |
| `REDIS_CONNECTION_STRING` | 否 | 默认 `localhost:6379` |
| `AGENT_MAX_CONCURRENCY` | 否 | Agent 并发，默认见配置 |

示例（请换成你自己的 Key）：

```env
BAIDU_MAP_AK=your_server_ak
VITE_BAIDU_MAP_AK=your_browser_ak
BAIDU_MAP_MAX_QPS=3
BAIDU_ROUTE_MATRIX_MAX_QPS=30
BAIDU_POI_SEARCH_DAILY_LIMIT=5000
LLM_API_BASE_URL=https://api.openai.com/v1
LLM_API_KEY=sk-your-key
LLM_MODEL=gpt-4o
REDIS_CONNECTION_STRING=localhost:6379
AGENT_MAX_CONCURRENCY=1
```

也可在 `backend/appsettings.json` 的 `BaiduMap` / `BlindArea` / `Agent` 节点调整默认值；`.env` 会在启动时注入（见 `EnvFileLoader`）。

#### 百度 AK 申请要点

1. 打开 [百度地图开放平台](https://lbsyun.baidu.com/apiconsole/key)
2. 创建两个应用：
   - **服务端**：Web 服务 API（地理编码、地点检索、步行批量算路等），IP 白名单
   - **浏览器端**：JavaScript API GL，Referer 如 `http://localhost:5173/*`
3. 按控制台实际配额调整 `BAIDU_*_DAILY_LIMIT` 与 QPS；项目内对通用接口与批量算路分别限流

未配置 LLM 时报告走模板；**未配置 `BAIDU_MAP_AK` 时核心分析不可用**。

### 3. 本地开发

**Redis（可选）**

```bash
docker run -d -p 6379:6379 redis:7-alpine
```

**后端** → http://localhost:5280

```bash
cd backend
dotnet run --urls http://localhost:5280
```

**前端** → http://localhost:5173

```bash
cd frontend
npm install
npm run dev -- --host --port 5173
```

### 4. Docker 部署

```bash
cp .env.example .env
# 编辑 .env
docker-compose up -d --build
```

| 服务 | 地址（以 compose 为准） |
|------|-------------------------|
| 前端 | http://localhost:5173 |
| 后端 | http://localhost:5000 |

---

## API 接口

| 接口 | 方法 | 说明 |
|------|------|------|
| `/api/geocode?address=` | GET | 地址转坐标 |
| `/api/geocode/suggest?query=` | GET | 地址联想 |
| `/api/isochrone?lat=&lng=&minutes=` | GET | 等时圈 |
| `/api/poi/coverage?lat=&lng=` | GET | POI 覆盖 + 盲区分析 |
| `/api/agent/tasks` | POST | 提交体检任务（body: lat/lng/address） |
| `/api/agent/tasks/{id}` | GET | 查询进度与结果 |
| `/api/quota/status` | GET | 当日配额使用情况 |

体检主流程：创建任务 → Worker 排队 → 解析位置 → 等时圈（批量算路）→ 地点检索与清洗 → 盲区网格 → AI 报告。前端有分步进度弹窗。

---

## 主要配置项（appsettings）

**BaiduMap**

| 键 | 默认 | 含义 |
|----|------|------|
| `MaxQps` | 3 | 通用 Web API 限流 |
| `RouteMatrixMaxQps` | 30 | 批量算路峰值 |
| `RouteMatrixBatchIntervalMs` | 300 | 算路分批间隔 |
| `PoiSearchDailyLimit` | 5000 | 地点检索日配额 |
| `GeocodeDailyLimit` | 5000 | 地理编码日配额 |
| `RouteMatrixDailyLimit` | 5000 | 批量算路日配额 |

**BlindArea**

| 键 | 默认 | 含义 |
|----|------|------|
| `GridSizeMeters` | 100 | 盲区网格边长 |
| `MinInsideRatio` | 0.5 | 格子与等时圈最小相交比 |
| `DefaultReachThresholdMeters` | 1000 | 默认可达阈值 R |

**Agent**：`IsochroneSampleDirections`（24）、`IsochroneBinarySearchIterations`（6）、`IsochroneMinutes`（15）等。

---

## CI/CD

GitHub Actions（`.github/workflows/ci.yml`）在 push / PR 时执行：

- **backend**：`dotnet build` + `dotnet format --verify-no-changes`
- **frontend**：`npm ci` + `npm run build`（含 `vue-tsc`）

---

## 开发规范

- C#：4 空格，遵循 `.editorconfig`
- 前端：TypeScript + Vue 3 Composition API
- 密钥只放 `.env`，不要写入仓库或截图到文档
- 提交前确认 `git status` 不含 `.env`

---

## 文档

| 文档 | 说明 |
|------|------|
| [docs/README.md](./docs/README.md) | 文档索引 |
| [需求与架构](./docs/requirements-and-architecture.md) | 功能与架构说明 |
| [盲区与类别清洗规格](./docs/15min-life-circle-blind-area-spec.md) | 7 类定义、盲区网格与样式 |
| [Agent 开发说明](./docs/AGENT.md) | AI 助手开发约束 |

---

## 许可证

[MIT](./LICENSE)
