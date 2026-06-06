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
| TD-CN-001 | `backend-ai` | setup | open | 建立可推送的 CN 备份远程，避免推送到上游作者仓库。 | 创建/确认 fork 或备份 remote 后推送初始化提交。 |
| TD-CN-002 | `test-ai` | blocker | blocked | 本机缺少 `dotnet` 和 Docker，无法本地验证 .NET 构建或容器运行。 | 在本机安装工具链，或转到服务器执行验证。 |
| TD-CN-003 | `backend-ai` | implementation | open | 搭建最小 CN Web/API/Worker 测试链路。 | 创建前端/API/Worker 骨架和 health smoke。 |

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
