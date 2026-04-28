# OnlineMarkDownViewer

基于 `.NET 10 + Blazor WebAssembly` 的在线 Markdown 文档查看器，提供静态登录页、文档源管理、Markdown 渲染、代码高亮和 Git 文档同步能力。

## AI 说明

本项目主要使用 AI 辅助编写与迭代实现，包括页面结构、样式、前后端逻辑和示例文档内容。

在继续扩展或用于正式环境前，仍建议结合人工测试与代码审查，重点关注安全性、稳定性和业务细节。

## 功能概览

- 静态登录页，登录成功后跳转到 WASM 查看器
- 文档源配置：本地路径 / Git 仓库 二选一
- 左侧目录浏览、面包屑导航、文件名与内容搜索
- Markdown 渲染，支持表格、任务列表、脚注等 GFM 能力
- 代码高亮、语言标签、一键复制
- 默认示例文档源，首次启动可直接体验界面与交互

## 本地运行

```powershell
dotnet run --project MarkDownViewer\MarkDownViewer\MarkDownViewer.csproj
```

默认会打开登录页：

```text
http://localhost:5114/login.html
```

## 默认账号

- 用户名：`admin`
- 密码：`admin123`

## 默认文档源

系统首次启动并读取配置时，会自动完成两件事：

1. 在 `MarkDownViewer/MarkDownViewer/data/default-docs/` 下生成示例 Markdown 文档
2. 在 `MarkDownViewer/MarkDownViewer/data/config.json` 中写入一个默认本地文档源

默认文档源名称为：

```text
默认文档源
```

如果你后续手工删除全部文档源，系统不会反复强制恢复默认源。

## 示例文档内容

默认示例文档源中包含：

- 欢迎首页
- 使用说明
- 文档源配置说明
- Markdown 语法演示
- 表格与任务列表示例
- Git 文档源同步说明

这些文档主要用于验证：

- 目录层级
- 面包屑跳转
- 搜索体验
- 章节导航
- 代码高亮
- 表格与引用样式

## 关键目录

```text
MarkDownViewer/
├─ MarkDownViewer/                 # ASP.NET Core 宿主与 API
│  ├─ data/                        # 配置与默认示例文档
│  └─ wwwroot/login.html           # 静态登录页
└─ MarkDownViewer.Client/          # Blazor WASM 前端
```

## 当前说明

- 登录页预热逻辑已适配当前 `.NET 10` 的 `_framework` 输出，不依赖 `blazor.boot.json`
- Git 文档源已支持 clone / pull / 定时同步
- 更完整的稀疏检出能力仍可继续增强
