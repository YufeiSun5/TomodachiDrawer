# MEMORY

Last updated: 2026-06-06 12:59 +08:00

## 当前阶段

全新初始化阶段。上游 `Lucas7yoshi/TomodachiDrawer` 已作为当前工作树基础，正在为 TomodachiDrawer-CN 建立 AI 协作文档体系和后续 Web MVP 实施入口。

## 已完成事项

- 拉取上游 TomodachiDrawer 代码到当前目录。
- 确认上游主技术栈为 .NET 10 / Avalonia / RP2040-RP2350 / ESP32-S3。
- 确认当前本机具备 Git、Node/npm 和 GitHub CLI 登录态。
- 确认当前本机缺少 `dotnet` 和 Docker，后续本地 .NET/Docker 验证受限。

## AI 工程化状态清单

| Item | Status | Notes |
| --- | --- | --- |
| `AGENTS.md` | initialized | 根级 AI 路引入口。 |
| `MEMORY.md` | initialized | 当前项目记忆。 |
| `AI_BOARD.md` | initialized | 唯一活跃协作看板。 |
| `.ai/` mother docs | initialized | instructions/docs/skills/agents/prompts 基础结构。 |
| Copilot adapter | initialized | 薄入口，指向母本文档。 |
| Cursor adapter | initialized | 薄入口，指向母本文档。 |
| Trae adapter | pending | 未发现 Trae 原生规则目录，暂不创建。 |

## 后续建议

- 创建 GitHub fork/备份远程后推送初始化提交。
- 安装或使用服务器上的 .NET 10 SDK 进行上游构建验证。
- 下一阶段优先实现最小 CN Web 测试链路：上传页、API health、任务状态 mock/骨架、Worker 边界。
- 将私密服务器计划保留在仓库外；仓库内只记录不含密码的部署边界。

## 待确认

- CN 项目公开仓库地址和最终源码公开 URL。 <!-- 待确认 -->
- 第一版是否只支持匿名任务加管理员账号。 <!-- 待确认 -->
- 服务器验证使用的域名、Nginx server block 和 HTTPS 证书策略。 <!-- 待确认 -->
- 是否在本机安装 .NET 10 SDK 和 Docker，或只在服务器验证。 <!-- 待确认 -->

## 改动记录

- 2026-06-06 12:59 | GPT-5 Codex | 初始化上游工作树与 AI 协作文档体系。
