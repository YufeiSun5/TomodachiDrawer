# AI Collaboration Board

## 文件模型

`AI_BOARD.md` 是唯一活跃 AI 协作看板，必须与 `MEMORY.md` 同级。`.ai/docs/` 只保存稳定文档、闭合记录、阶段总结和归档材料，不承载 open/blocked 工作项。

## AI Identities

| Identity | Owner | Scope | Boundary |
| --- | --- | --- | --- |
| Frontend AI | `frontend-ai` | 前端 UI、上传/任务/图库/审核页面、API 调用封装、三语文案、视觉和交互。 | 不实现生成核心，不直接访问 Redis/PostgreSQL/文件系统。 |
| Backend AI | `backend-ai` | 后端、Worker、队列、生成核心封装、配置、DTO、安装器、部署。 | 前端可见 API/DTO/错误语义变化必须写入看板。 |
| Test AI | `test-ai` | 单元测试、smoke、E2E、lab、压测、发布门禁证据。 | 默认不改产品行为；修测试夹具必须说明范围。 |
| Review AI | `review-ai` | 架构审阅、上线评估、GPL/安全风险清单、跨模块微调。 | 先列风险和缺口；跨身份修改必须说明原因和影响。 |

## Active Board

| ID | Owner | Type | Status | Item | Next Action |
| --- | --- | --- | --- | --- | --- |
| TD-CN-001 | `backend-ai` | setup | closed | 建立可推送的 CN 备份远程，避免推送到上游作者仓库。 | 已创建 `YufeiSun5/TomodachiDrawer` fork 并推送初始化提交。 |
| TD-CN-002 | `test-ai` | blocker | closed | 本机缺少 `dotnet` 和 Docker，无法本地验证 .NET 构建或容器运行。 | 已转到服务器安装 .NET 10 SDK 并完成 build/smoke。 |
| TD-CN-003 | `backend-ai` | implementation | closed | 搭建最小 CN Web/API/Worker 测试链路。 | 已创建前端、API、Worker、Docker Compose 骨架。 |
| TD-CN-004 | `test-ai` | verification | closed | 在服务器验证最小链路。 | 服务器 .NET build、API health、上传/下载、Worker smoke 均通过。 |
| TD-CN-005 | `backend-ai` | follow-up | open | Docker Compose 构建未完成。 | `mcr.microsoft.com/dotnet/sdk:10.0` 镜像拉取多次超时；后续可重试或改 host publish/runtime image。 |
| TD-CN-006 | `frontend-ai` | design | closed | 前端不是朋友收集梦想生活风格。 | 已改为原创小屋、岛屿、作品墙和软糖色工作台风格。 |
| TD-CN-007 | `backend-ai` | deploy | closed | 上线最小可测试版。 | 已通过 systemd + Nginx 上线到 `http://49.232.169.142/tomodachi/`。 |
| TD-CN-008 | `backend-ai` | implementation | closed | 用开源仓库核心代码生成真实单片机文件。 | 已用 `TomodachiDrawer.Core` 生成 TDLD，并用上游 UF2 算法生成 RP2040/RP2350 UF2。 |
| TD-CN-009 | `test-ai` | verification | closed | 校验服务器真实生成文件合法性。 | TDLD magic/version/end opcode、RP2040/RP2350 UF2 magic/family/target 均通过。 |
| TD-CN-010 | `backend-ai` | implementation | closed | 支持任意尺寸图片按用户裁切框转换。 | API 已接收裁切参数，服务端重采样为 256x256 后进入 Core，并保存预览 PNG。 |
| TD-CN-011 | `frontend-ai` | design | closed | 参考像素广场信息结构重做前端，不照搬目标站样式。 | 已增加裁切预览、分享广场示例、搜索、按点赞排序，并完成 PC/手机截图验证。 |
| TD-CN-012 | `test-ai` | verification | closed | 上线后验证大图裁切、TDLD/UF2 合法性和公开预览路径。 | 公网上传 800x300 PNG 裁切生成通过，PNG/TDLD/UF2 头部校验通过。 |
| TD-CN-013 | `backend-ai/frontend-ai` | follow-up | open | 真实持久化分享广场、点赞、审核后台和作品下载库。 | 当前广场为前端示例数据；后续需要 PostgreSQL/存储、管理员审核、访客点赞和搜索排序 API。 |
| TD-CN-014 | `backend-ai/frontend-ai` | bug | open | 长时间生成请求同步等待导致 Nginx 504，前端一直转圈。 | 改为后端异步队列、任务状态轮询、排队位次和粗进度展示。 |

## Board Rules

1. 开工前先声明身份，再读 Active Board。
2. 新问题必须加到 Active Board，分配合法 `Owner`。
3. 解决后改为 `closed`，并追加 Activity Log。
4. 无法推进时改为 `blocked`，写清缺什么。
5. API、DTO、错误语义、页面范围变化，先更新 Active Board，再同步稳定契约文档。
6. `test-ai` 必须记录验证命令、通过/失败结果、未跑原因。
7. `review-ai` 必须优先列风险、缺口和阻塞。
8. 最终回复必须说明身份、处理的 Board 项、仍 open/blocked 的项。
9. 闭合历史过长时，由 `review-ai` 归档到 `.ai/docs/archive/` 或阶段总结。

## Activity Log Format

```text
- YYYY-MM-DD HH:mm | <frontend-ai/backend-ai/test-ai/review-ai> | <question/decision/answer/blocker/review/test> | <影响范围> | <open/closed/blocked>
```

## Activity Log

- 2026-06-06 12:59 | `backend-ai` | decision | project setup | open | 使用上游 TomodachiDrawer 作为当前工作树基础，新增 CN 协作体系。
- 2026-06-06 12:59 | `test-ai` | blocker | local verification | blocked | 本机缺少 `dotnet` 与 Docker，需服务器或安装工具链验证。
- 2026-06-06 13:03 | `backend-ai` | answer | backup remote | closed | 创建并推送到 `YufeiSun5/TomodachiDrawer` fork。
- 2026-06-06 13:13 | `frontend-ai` | test | frontend build | closed | `npm install`、`npm run build` 通过，桌面/移动截图非空。
- 2026-06-06 13:13 | `backend-ai` | decision | mvp skeleton | closed | API 当前生成占位输出，真实 Core 集成待 .NET 验证后接入。
- 2026-06-06 13:57 | `test-ai` | test | server smoke | closed | 服务器 `dotnet build`、API health、上传/下载和 Worker 启动通过。
- 2026-06-06 13:57 | `backend-ai` | blocker | docker compose | open | Docker SDK 镜像拉取超时，Compose 构建未完成。
- 2026-06-06 14:38 | `frontend-ai` | answer | frontend style | closed | 前端改为原创朋友收集梦想生活风格，桌面/移动构建验证通过。
- 2026-06-06 14:38 | `backend-ai` | answer | public deploy | closed | 公网 `http://49.232.169.142/tomodachi/` 和 `/tomodachi/api/health` 验证通过。
- 2026-06-06 19:20 | `backend-ai` | answer | core generation | closed | 后端改用 `CanvasDrawer`、`TimingSink`、`FileControllerSink` 生成真实 TDLD。
- 2026-06-06 19:20 | `test-ai` | test | public generation | closed | 公网测试 TDLD=509 bytes；RP2040/RP2350 UF2=1024 bytes 且 family id 正确。
- 2026-06-06 20:05 | `frontend-ai/backend-ai/test-ai` | decision | crop responsive gallery | open | 本轮处理任意尺寸裁切、分享广场筛选排序、PC/手机响应式和上线复验。
- 2026-06-06 20:54 | `test-ai` | test | public crop generation | closed | 公网上传 800x300 PNG，裁切 300x300 生成 TDLD=3133 bytes、RP2350 UF2=6656 bytes；预览 PNG magic、TDLD magic/version/end、UF2 magic/family/target/end 均通过。
- 2026-06-06 20:54 | `review-ai` | decision | gallery scope | open | 当前分享广场、搜索和点赞排序为前端示例能力，真实用户作品持久化、审核后台和点赞 API 进入 TD-CN-013。
- 2026-06-06 20:22 | `test-ai` | blocker | long generation | open | 用户实测返回 Nginx 504；线上日志显示 `POST /api/jobs` 长时间同步执行 Core 生成，需要异步队列。
