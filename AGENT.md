# Agent.md

本文件供AI编程助手（Claude Code等）在本仓库工作时阅读，用于快速了解项目背景、架构约束与开发规范。完整需求与架构说明见 `15分钟生活圈体检助手-需求与架构文档.md`，本文件只保留**AI助手必须遵守的硬性约束**和**日常开发速查信息**，避免每次都重新读一遍长文档。

---

## 项目一句话说明

基于百度地图API的"15分钟步行生活圈"体检工具：输入社区中心点坐标，计算真实步行可达范围（等时圈），统计范围内医疗/教育/购物/养老等设施覆盖情况，识别设施盲区，并由LLM生成自然语言体检报告。参赛项目：开源AI工具赛道。

---

## 技术栈速查

| 层 | 技术 |
|---|---|
| 前端 | Vue3 + TypeScript + 百度地图JS API + ECharts |
| 后端 | ASP.NET Core Web API (.NET 8) |
| AI编排 | 后端内置 `AgentService`，直连LLM厂商Chat Completions接口（OpenAI兼容协议，Function Calling） |
| 异步任务 | `Channel<T>` + `BackgroundService`（进程内队列，非外部消息队列） |
| 缓存 | Redis（服务器端共享缓存，非SQLite，非浏览器本地存储） |
| 部署 | Docker + docker-compose |

---

## ⚠️ 硬性架构约束（不要在没有明确讨论的情况下改动）

以下决策是经过权衡后确定的，AI助手在写代码/建议方案时**不要**擅自引入被否决的方案：

1. **不使用Dify或任何独立Agent平台**
   原因：自建Dify社区版在工作流并发上有明显瓶颈（自身worker进程数、状态持久化开销是额外调优负担），且与本项目"AI处理耗时长、需要精细并发控制"的诉求冲突。Agent逻辑必须写在ASP.NET Core后端内，用Function Calling直连LLM API。

2. **不使用LangChain**
   原因：LangChain是Python优先的框架，.NET移植版是社区二等公民，功能滞后。如果未来需要引入编排框架，优先考虑Semantic Kernel / Microsoft Agent Framework（原生.NET一等公民支持），而不是LangChain。当前阶段（工具集固定、流程线性）优先手写`AgentService`，不引入框架。

3. **缓存必须是Redis共享缓存，不是SQLite，不是浏览器本地存储**
   所有用户请求都经后端统一转发，缓存键为`(中心点坐标, 分类)`，对所有用户共享。不要写成"每个用户各自缓存"的逻辑，也不要用localStorage/sessionStorage做跨会话的业务数据持久化。

4. **AI任务必须走异步队列，不能同步阻塞HTTP请求**
   `AgentController`收到请求后应立即返回任务ID，任务进`AgentTaskQueue`（`Channel<T>`），由`AgentTaskWorker`（`BackgroundService`）用`SemaphoreSlim`控制并发（建议3~5并发）消费。前端通过轮询或SSE（`/api/agent/tasks/{id}` / `/api/agent/tasks/{id}/stream`）获取结果。**不要**写成"controller里直接await整个Agent链路跑完再返回"的同步实现。

5. **暂不实现对话式追问（多轮对话）功能**
   AI能力目前只做"一次性生成体检报告"，`AgentService`处理单轮任务即可，不需要维护对话历史/多轮上下文。如果后续需求变更要加这个功能，需要先确认，不要自行扩展。

6. **前端要响应式，且要为小程序web-view兼容性预留**
   - 断点：手机`<768px` / 平板`768~1024px` / 桌面`>1024px`，移动优先设计
   - 核心参数（中心点坐标、任务ID等）优先走**URL query**传递，不要依赖localStorage做跨会话状态（小程序webview里localStorage行为不稳定）
   - 不要写只有hover才能触发的关键交互，需有触屏可用的替代方案
   - 本期不建小程序原生壳工程，只做好H5本身的兼容性

7. **AIGC报告生成必须基于结构化数据，禁止让LLM编造数据**
   Prompt需要明确约束"只能基于传入的结构化数据做总结/建议，不得添加未检索到的设施信息"。涉及数量/距离的表述要和原始工具返回数据一致。建议加一层输出校验（报告里提到的数字与原始数据核对，不一致则重新生成或降级为模板文案）。

---

## API配额约束（开发/测试时务必注意）

百度地图开放平台个人开发者账号配额：

| 接口 | 配额(次/日) | 备注 |
|---|---|---|
| 步行路线规划(轻量) | 5,000 | 一次等时圈分析约消耗20次（20方向采样），优先用批量算路替代逐点调用 |
| 批量算路 | 5,000 | 优先使用，减少请求轮次 |
| 地理编码 | 5,000 | 富余 |
| **地点检索（POI）** | **100（全链路瓶颈）** | 一次分析约消耗8~20次，**开发调试阶段务必先查Redis缓存，同一坐标不要重复真实调用** |

调试建议：本地开发时对同一测试坐标反复跑，务必确认走的是Redis缓存分支，避免几小时内刷光当日配额导致其他调试卡住。

---

## 目录结构约定

```
/backend                  # ASP.NET Core Web API
 ├─ Controllers/
 ├─ Services/
 │   ├─ AgentService.cs         # LLM Function Calling编排
 │   ├─ AgentToolRegistry.cs    # 工具Schema注册
 │   ├─ AgentTaskQueue.cs       # Channel<T>队列
 │   ├─ RedisCacheService.cs
 │   ├─ QuotaGuardService.cs
 │   ├─ IsochroneService.cs
 │   └─ PoiCoverageService.cs
 ├─ Workers/
 │   └─ AgentTaskWorker.cs      # BackgroundService
 └─ Models/

/frontend                 # Vue3 + TypeScript
 ├─ src/views/
 ├─ src/components/
 ├─ src/api/
 └─ src/store/

/docs                      # 需求架构文档、本文件
docker-compose.yml          # 前端 + 后端 + Redis 一键起
```

---

## 环境变量（示例，需脱敏，实际值放.env不入库）

**百度地图需申请两个独立应用、两个 AK：**

| 变量 | 用途 | 应用类型 | 白名单 |
|---|---|---|---|
| `BAIDU_MAP_AK` | 后端 Web 服务 API（地理编码/POI/路径规划） | 服务端 | 服务器 IP |
| `VITE_BAIDU_MAP_AK` | 前端 JavaScript API GL（地图展示） | 浏览器端 | Referer |

以上变量均写在**项目根目录** `.env`（前端 Vite 已通过 `envDir` 指向根目录，docker-compose 亦从此读取）。

```
# 项目根目录 .env
BAIDU_MAP_AK=your_server_ak
VITE_BAIDU_MAP_AK=your_browser_ak

LLM_API_BASE_URL=https://your-llm-provider/v1
LLM_API_KEY=your_llm_api_key
LLM_MODEL=your_model_name
REDIS_CONNECTION_STRING=redis:6379
AGENT_MAX_CONCURRENCY=4
```

---

## 常用命令

```bash
# 本地一键起（前端+后端+Redis）
docker-compose up -d

# 仅后端开发调试
cd backend && dotnet run

# 仅前端开发调试
cd frontend && npm run dev

# 后端格式化检查（对应开源工程规范评分项）
cd backend && dotnet format --verify-no-changes
```

---

## 给AI编程助手的额外提醒

- 改动涉及第2节里列出的硬性约束时，先在对话里跟用户确认，不要直接改代码
- 新增依赖前，优先确认是否已有同类能力（比如不要在已经有`RedisCacheService`的情况下又引入内存缓存库）
- 涉及百度地图API/LLM API调用的代码，务必经过`QuotaGuardService`或等效的限流封装，不要绕过直接裸调
- 涉及PII或AK/API Key的内容，一律走环境变量，不要硬编码进代码或提交进Git历史
