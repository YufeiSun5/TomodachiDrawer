# MEMORY

Last updated: 2026-06-06 20:54 +08:00

## 当前阶段

全新初始化阶段。上游 `Lucas7yoshi/TomodachiDrawer` 已作为当前工作树基础，正在为 TomodachiDrawer-CN 建立 AI 协作文档体系和后续 Web MVP 实施入口。

## 已完成事项

- 拉取上游 TomodachiDrawer 代码到当前目录。
- 确认上游主技术栈为 .NET 10 / Avalonia / RP2040-RP2350 / ESP32-S3。
- 确认当前本机具备 Git、Node/npm 和 GitHub CLI 登录态。
- 确认当前本机缺少 `dotnet` 和 Docker，后续本地 .NET/Docker 验证受限。
- 创建 TomodachiDrawer-CN 最小 Web/API/Worker 骨架。
- 前端 `npm install`、`npm run build` 通过。
- Vite smoke 已在 `http://localhost:5178` 截图验证桌面和移动布局非空可用。
- 服务器 `/opt/tomodachi-drawer-cn` 已通过归档部署，不改动既有 20k 项目目录。
- 服务器 .NET 10 SDK 已安装并完成 `dotnet restore` / `dotnet build`。
- 服务器 API health、图片上传、任务创建、`.tdld` 下载 smoke 通过。
- 服务器 Worker 短时启动 smoke 通过。
- 前端已改为原创“朋友收集梦想生活 / 小屋工作台”风格。
- 已上线公网 IP 路径：`http://49.232.169.142/tomodachi/`。
- 公网 API smoke 通过：`/tomodachi/api/health`、上传任务、`.tdld` 下载。
- 后端已接入 `TomodachiDrawer.Core` 真实生成链路，移除占位输出。
- 公网真实 TDLD 校验通过：`TDLD` magic、version `3`、终止 opcode `0x00`。
- 公网真实 RP2040 UF2 校验通过：UF2 magic、target `0x10100000`、family `E48BFF56`。
- 公网真实 RP2350 UF2 校验通过：family `E48BFF57`。
- API 发布包含 SkiaSharp Linux native assets，解决服务器 `libSkiaSharp.so` 缺失问题。
- API 支持任意尺寸图片上传后按裁切参数重采样为 256x256，再进入 `TomodachiDrawer.Core`。
- API 任务响应新增 `previewUrl`，服务端保存一份处理后的 PNG 预览图。
- 前端新增 1:1 裁切画布、缩放/水平/垂直取景控制，PC 三栏和手机单列布局已截图验证。
- 前端新增“分享广场”示例区，具备分类、搜索、点赞最多/最新/随机排序的前端交互；真实持久化广场和点赞 API 尚未完成。
- 公网上传 800x300 PNG 裁切生成验证通过：TDLD magic/version/end byte、预览 PNG magic、RP2350 UF2 magic/family/target/end 均合法。

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
| Web MVP skeleton | online | `frontend/`、`backend/`、`docker-compose.yml` 已创建；公网 IP 路径已上线并接入真实生成。 |

## 后续建议

- 创建 GitHub fork/备份远程后推送初始化提交。
- 安装或使用服务器上的 .NET 10 SDK 进行上游构建验证。
- 下一阶段做预览图保存、审核稿件持久化和真实图库/后台。
- 下一阶段实现真实分享广场：用户投稿保存、管理员审核、作品预览图和各单片机文件持久化、搜索排序和点赞 API。
- Docker Compose 构建需在 .NET SDK 镜像可拉取后再验证，或改为 host publish + runtime image 流程。
- 当前上线使用 systemd + Nginx 路径方式：API 监听 `127.0.0.1:5080`，前端挂载 `/tomodachi/`。
- 将私密服务器计划保留在仓库外；仓库内只记录不含密码的部署边界。

## 待确认

- CN 项目公开仓库地址和最终源码公开 URL。 <!-- 待确认 -->
- 第一版是否只支持匿名任务加管理员账号。 <!-- 待确认 -->
- 服务器验证使用的域名、Nginx server block 和 HTTPS 证书策略。 <!-- 待确认 -->
- 是否在本机安装 .NET 10 SDK 和 Docker，或只在服务器验证。 <!-- 待确认 -->

## 改动记录

- 2026-06-06 12:59 | GPT-5 Codex | 初始化上游工作树与 AI 协作文档体系。
- 2026-06-06 13:13 | GPT-5 Codex | 新增最小 Web/API/Worker 骨架并完成前端 smoke。
- 2026-06-06 13:57 | GPT-5 Codex | 在服务器完成 .NET 构建和 API/Worker smoke 验证。
- 2026-06-06 14:38 | GPT-5 Codex | 优化前端风格并上线到公网 `/tomodachi/`。
- 2026-06-06 19:20 | GPT-5 Codex | 接入真实 Core 生成并通过公网 TDLD/UF2 校验。
- 2026-06-06 20:54 | GPT-5 Codex | 支持任意尺寸裁切转换，新增服务端预览图和响应式分享广场示例，并完成公网大图 TDLD/UF2 校验。
