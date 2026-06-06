# TomodachiDrawer-CN 初始化说明

本仓库基于 [Lucas7yoshi/TomodachiDrawer](https://github.com/Lucas7yoshi/TomodachiDrawer) 初始化，用于推进 TomodachiDrawer-CN 的本地开发、Web MVP 和后续服务器部署验证。

## 目标

- 保留并复用上游 `TomodachiDrawer.Core` 的图片处理、调色、路径规划和输出能力。
- 增加面向国内用户的 Web 工具链：图片上传、参数选择、排队生成、结果下载、作品保存、分享申请和管理员审核。
- 严格遵守 GPL-3.0，保留原作者信息、许可证、修改记录和衍生源码公开路径。

## 当前状态

- 上游桌面端和固件代码已保留。
- AI 协作文档体系已建立。
- CN Web/API/Worker 仍处于待实现阶段。

## 本地验证

上游项目需要 .NET 10 SDK：

```powershell
dotnet restore
dotnet build --configuration Debug
dotnet test --configuration Debug
```

当前工作机若没有 `dotnet`，请在安装 .NET 10 SDK 后再执行，或转到服务器验证。

## 合规说明

请阅读：

- [LICENSE](LICENSE)
- [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md)
- [NOTICE.md](NOTICE.md)
- [CHANGES.md](CHANGES.md)
- [Docs/gpl-compliance.md](Docs/gpl-compliance.md)

本项目与 Nintendo 无官方关联。
