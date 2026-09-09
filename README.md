# 15 分钟生活圈智能体检助手

基于百度地图开放能力与 LLM 的 Web 应用：输入社区中心点，计算**真实路网 15 分钟步行等时圈**，统计民生设施覆盖、识别盲区，并生成 AI 体检报告。

[![CI](https://github.com/maxleton-cyber/15m-lifecycle/actions/workflows/ci.yml/badge.svg)](https://github.com/maxleton-cyber/15m-lifecycle/actions/workflows/ci.yml)

## 功能概览

- 地址搜索 / 坐标选点（支持 BD-09、GCJ-02、WGS84）
- 百度真实路网步行等时圈（多方向采样 + 批量算路）
- 8 类民生 POI 覆盖分析：基础医疗、药店、学校、购物、养老服务、商超、公园绿地、公共交通
- AI 体检报告（OpenAI 兼容 LLM，可配置）
- Redis 缓存与 API 配额管理

## 技术栈

| 层级 | 技术 |
|------|------|
| 前端 | Vue 3 + TypeScript + ECharts + 百度地图 JS API GL |
| 后端 | ASP.NET Core 8 Web API |
| 缓存 | Redis（可选，无 Redis 时降级内存缓存） |
| 部署 | Docker Compose |

## 项目结构

```
15m-lifecycle/
├── backend/                 # ASP.NET Core API
│   ├── Controllers/         # HTTP 接口
│   ├── Services/            # 等时圈、POI、地图、Agent 等业务逻辑
│   ├── Workers/             # 后台任务队列
│   └── Configuration/       # 环境变量加载等
├── frontend/                # Vue 3 单页应用
│   └── src/
│       ├── components/      # 地图、报告、地址选择等
│       ├── views/           # 主页面
│       └── constants/       # POI 分类元数据
├── .github/workflows/       # GitHub Actions CI
├── docker-compose.yml
├── .env.example             # 环境变量模板（提交到仓库）
└── docs/                    # 需求与架构文档
```

## 快速开始

### 1. 克隆与配置

```bash
git clone https://github.com/maxleton-cyber/15m-lifecycle.git
cd 15m-lifecycle
cp .env.example .env
```

编辑 `.env` 填入密钥。**切勿将 `.env` 提交到 Git**（已在 `.gitignore` 中排除）。

### 2. 环境变量

| 变量 | 必填 | 说明 |
|------|------|------|
| `BAIDU_MAP_AK` | **是** | 百度地图**服务端** AK（地理编码 / POI / 步行算路） |
| `VITE_BAIDU_MAP_AK` | **是** | 百度地图**浏览器端** AK（页面地图 JS API，与服务端 AK 不同） |
| `LLM_API_BASE_URL` | 否 | LLM API 根地址（OpenAI 兼容，需含 `/v1`） |
| `LLM_API_KEY` | 否 | LLM API Key |
| `LLM_MODEL` | 否 | 模型名，如 `gpt-4o`、`deepseek-chat` |
| `REDIS_CONNECTION_STRING` | 否 | 默认 `localhost:6379` |
| `AGENT_MAX_CONCURRENCY` | 否 | Agent 并发数，默认 `1` |

`.env.example` 示例（**请替换为你的 Key，勿使用示例值**）：

```env
BAIDU_MAP_AK=your_server_ak_here
VITE_BAIDU_MAP_AK=your_browser_ak_here
LLM_API_BASE_URL=https://api.openai.com/v1
LLM_API_KEY=sk-your-key-here
LLM_MODEL=gpt-4o
REDIS_CONNECTION_STRING=localhost:6379
AGENT_MAX_CONCURRENCY=1
```

#### 百度 AK 申请要点

1. 登录 [百度地图开放平台](https://lbsyun.baidu.com/apiconsole/key)
2. 创建两个应用：
   - **服务端**：勾选 Web 服务 API（地理编码、地点检索、步行批量算路等），白名单填服务器 IP
   - **浏览器端**：勾选 JavaScript API GL，Referer 填 `http://localhost:5173/*`
3. 个人开发者 Web 服务 **并发约 3 QPS**，项目已内置全局限流

未配置 LLM 时，报告模块使用模板降级；**未配置 `BAIDU_MAP_AK` 时核心功能不可用**。

### 3. 本地开发

**Redis（可选）**

```bash
docker run -d -p 6379:6379 redis:7-alpine
```

**后端**（默认 `http://localhost:5280`）

```bash
cd backend
dotnet run
```

后端启动时会自动加载项目根目录 `.env`。

**前端**（默认 `http://localhost:5173`）

```bash
cd frontend
npm install
npm run dev
```

### 4. Docker 部署

```bash
cp .env.example .env
# 编辑 .env 后执行
docker-compose up -d --build
```

| 服务 | 地址 |
|------|------|
| 前端 | http://localhost:5173 |
| 后端 API | http://localhost:5000 |

## API 接口

| 接口 | 方法 | 说明 |
|------|------|------|
| `/api/geocode?address=` | GET | 地址转坐标 |
| `/api/geocode/suggest?query=` | GET | 地址联想 |
| `/api/poi/coverage?lat=&lng=` | GET | POI 覆盖统计 |
| `/api/agent/tasks` | POST | 提交分析任务 |
| `/api/agent/tasks/{id}` | GET | 查询任务状态与结果 |
| `/api/quota/status` | GET | 当日配额状态 |

## CI/CD

GitHub Actions（`.github/workflows/ci.yml`）在 push / PR 时自动执行：

- **backend**：`dotnet build` + `dotnet format --verify-no-changes`
- **frontend**：`npm ci` + `npm run build`（含 TypeScript 检查）

## 开发规范

- C#：4 空格缩进，遵循 `.editorconfig`
- 前端：TypeScript 严格模式，Vue 3 Composition API
- 敏感信息仅通过 `.env` 注入，禁止写入代码或 `appsettings.json`
- 提交前确认：`git status` 中不应出现 `.env`

## 文档

- [需求与架构文档](./15分钟生活圈体检助手-需求与架构文档.md)
- [Agent 开发说明](./AGENT.md)

## 许可证

[MIT](./LICENSE)
