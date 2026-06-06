# MEMORY

Last updated: 2026-06-06 22:52 +08:00

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
- 修复长时间生成导致浏览器等待并最终 Nginx 504 的问题：`POST /api/jobs` 现在只入队并快速返回 `202 Accepted`。
- API 新增任务列表/轮询能力，任务响应包含 `queueAhead`、`progressPercent`、`startedAt`、`completedAt`。
- 前端生成结果区支持多次连续提交、排队位次、前方人数、粗进度条、失败/成功状态和完成后下载。
- 公网连续提交两个 800x300 裁切任务验证通过：0.18s/0.09s 返回 202，第二个显示第 2 位且前面 1 个任务，轮询后两个均成功。
- 按用户要求，生成列表改为只显示 `pending` / `running` 实时任务；成功/失败任务不再作为历史列表从 `/api/jobs` 返回。
- 非投稿生成源图在任务结束后立即删除；输出文件和预览图只保留约 30 分钟作为临时下载窗口，并由后台清理器删除。
- 部署前已等待线上运行任务 `6162ffb6...` 完成，未中断用户任务。
- 公网 quick 任务验证通过：成功后 `/api/jobs` active count 为 0，直接 job 查询和 TDLD 下载可用，上传源图已从服务器删除。
- API 增加浏览器本地 `clientId` 任务隐私：自己的任务显示文件名、缩略图、下载和分享；其他人的任务在队列中只显示匿名序号。
- 直接查询非本人 jobUuid 返回 404；下载/预览也要求同一 `clientId`。
- 基础分享广场已从前端示例升级为服务端 JSON 持久化：成功任务可命名分享到广场，名称允许重复，广场显示缩略图、适用单片机型号和下载格式。
- 首页结构调整为广场优先、创作第二屏；广场支持按全部/RP2040/RP2350/ESP32-S3 分类，支持搜索、点赞最多/最新/随机排序。
- 公网验证通过：另一个 client 只能看到匿名任务；RP2040 任务生成后以重复名称分享到广场，广场过滤和缩略图 PNG 校验通过。

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
- 下一阶段完善分享广场：管理员审核、点赞 API、PostgreSQL/对象存储、更完整搜索排序和投稿治理。
- Docker Compose 构建需在 .NET SDK 镜像可拉取后再验证，或改为 host publish + runtime image 流程。
- 当前上线使用 systemd + Nginx 路径方式：API 监听 `127.0.0.1:5080`，前端挂载 `/tomodachi/`。
- 将私密服务器计划保留在仓库外；仓库内只记录不含密码的部署边界。
- 当前队列仍是单进程内存队列；服务器重启会丢失未完成任务状态。后续真实生产需要 Redis/Hangfire 或 PostgreSQL 持久化队列。
- 未投稿文件当前为临时下载，不进入历史列表；真正投稿/审核通过后才需要持久化图片和各单片机文件。
- 当前基础广场使用服务器 JSON 文件持久化，适合 MVP；并发投稿、审核和长期运营仍应迁移到数据库/对象存储。

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
- 2026-06-06 21:05 | GPT-5 Codex | 将同步生成改为异步队列和轮询进度，修复长任务 504，并完成公网连续提交/排队/下载校验。
- 2026-06-06 21:48 | GPT-5 Codex | 生成列表改为仅显示实时/排队任务，非投稿文件临时化并验证源图删除和 active-only 列表。
- 2026-06-06 22:52 | GPT-5 Codex | 实现任务隐私、我的缩略图、命名分享到持久化广场、广场首屏和按单片机型号分类。
