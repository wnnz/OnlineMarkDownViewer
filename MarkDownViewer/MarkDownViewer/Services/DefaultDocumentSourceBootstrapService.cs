using MarkDownViewer.Contracts;

namespace MarkDownViewer.Services;

public sealed class DefaultDocumentSourceBootstrapService
{
    private const string DefaultSourceId = "default-local-docs";
    private const string DefaultSourceName = "默认文档源";

    private readonly string _dataDirectory;
    private readonly string _defaultDocsPath;
    private readonly string _bootstrapMarkerPath;

    public DefaultDocumentSourceBootstrapService(IHostEnvironment hostEnvironment)
    {
        _dataDirectory = Path.Combine(hostEnvironment.ContentRootPath, "data");
        _defaultDocsPath = Path.Combine(_dataDirectory, "default-docs");
        _bootstrapMarkerPath = Path.Combine(_dataDirectory, ".default-source-initialized");
    }

    public async Task<BootstrapConfigResult> EnsureInitializedAsync(
        AppConfigDto config,
        bool configFileExists,
        CancellationToken cancellationToken)
    {
        await EnsureSampleDocumentsAsync(cancellationToken);

        if (TryRepairDefaultSource(config))
        {
            return new BootstrapConfigResult(config, true);
        }

        if (config.Sources.Count > 0)
        {
            return new BootstrapConfigResult(config, false);
        }

        if (!configFileExists || !File.Exists(_bootstrapMarkerPath))
        {
            await File.WriteAllTextAsync(_bootstrapMarkerPath, "initialized", cancellationToken);
            return new BootstrapConfigResult(BuildDefaultConfig(), true);
        }

        return new BootstrapConfigResult(config, false);
    }

    private AppConfigDto BuildDefaultConfig()
    {
        return new AppConfigDto
        {
            Sources =
            [
                new DocumentSourceDto
                {
                    Id = DefaultSourceId,
                    Name = DefaultSourceName,
                    Kind = DocumentSourceKind.Local,
                    LocalPath = _defaultDocsPath
                }
            ]
        };
    }

    private bool TryRepairDefaultSource(AppConfigDto config)
    {
        var source = config.Sources.FirstOrDefault(item =>
            string.Equals(item.Id, DefaultSourceId, StringComparison.OrdinalIgnoreCase));

        if (source is null)
        {
            return false;
        }

        var changed = false;
        if (source.Kind != DocumentSourceKind.Local)
        {
            source.Kind = DocumentSourceKind.Local;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(source.LocalPath) || !Directory.Exists(source.LocalPath))
        {
            source.LocalPath = _defaultDocsPath;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(source.Name))
        {
            source.Name = DefaultSourceName;
            changed = true;
        }

        return changed;
    }

    private async Task EnsureSampleDocumentsAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_defaultDocsPath);
        Directory.CreateDirectory(Path.Combine(_defaultDocsPath, "01-quick-start"));
        Directory.CreateDirectory(Path.Combine(_defaultDocsPath, "02-configuration"));
        Directory.CreateDirectory(Path.Combine(_defaultDocsPath, "03-markdown-examples"));
        Directory.CreateDirectory(Path.Combine(_defaultDocsPath, "04-git-source"));

        await WriteIfMissingAsync(
            Path.Combine(_defaultDocsPath, "00-home.md"),
            """
            # 欢迎使用 MD Viewer

            这是系统自动生成的默认文档源，用来帮助你快速确认整个查看器是否工作正常。

            ## 你现在可以做什么

            - 打开左侧不同目录，体验面包屑导航与文件列表滚动
            - 搜索“Markdown”或“Git”，测试文件名与内容搜索
            - 打开示例文档，查看目录导航、代码高亮和表格样式
            - 点击右上角配置按钮，添加你自己的本地目录或 Git 文档源

            ## 默认账号

            - 用户名：`admin`
            - 密码：`admin123`

            ## 示例目录说明

            - `01-quick-start`：快速上手和基本使用说明
            - `02-configuration`：文档源配置说明
            - `03-markdown-examples`：Markdown 渲染效果示例
            - `04-git-source`：Git 文档源同步说明

            ## 下一步建议

            先打开“使用说明”和“Markdown 语法演示”，确认整体体验符合预期，再把自己的真实文档目录接进来。
            """,
            cancellationToken);

        await WriteIfMissingAsync(
            Path.Combine(_defaultDocsPath, "01-quick-start", "usage-guide.md"),
            """
            # 使用说明

            ## 登录

            使用默认账号 `admin / admin123` 登录系统。登录成功后会进入文档查看页面。

            ## 切换文档源

            顶部右侧点击配置按钮，添加或编辑文档源。配置保存后，左侧下拉框会自动刷新。

            ## 浏览文档

            ### 进入目录

            点击左侧列表中的目录项即可进入下一层目录。

            ### 打开 Markdown 文件

            点击 `.md` 或 `.markdown` 文件后，右侧会显示：

            - 文件名称
            - 文件大小
            - 最后修改时间
            - 自动解析的章节导航
            - 渲染后的 Markdown 正文

            ## 搜索

            搜索框支持两种匹配方式：

            - 文件名匹配
            - 文件内容匹配

            如果是内容命中，列表项会显示“内容匹配”标签。

            ## 退出登录

            点击右上角“退出”按钮会清除当前 Token，并返回登录页。
            """,
            cancellationToken);

        await WriteIfMissingAsync(
            Path.Combine(_defaultDocsPath, "02-configuration", "source-config-guide.md"),
            """
            # 文档源配置说明

            ## 本地路径模式

            适合直接读取服务器上的现有文档目录。

            ### 需要填写

            - 源名称
            - 服务器绝对路径

            ### 示例

            ```text
            C:\Docs\TeamWiki
            /home/docs/wiki
            ```

            ## Git 仓库模式

            适合把远程仓库中的 Markdown 文档同步到系统中统一查看。

            ### 需要填写

            - 源名称
            - Git 仓库地址
            - 拉取间隔（分钟）
            - 可选的子目录路径
            - 可选的认证信息

            ## 认证方式

            支持三种方式：

            - 不认证
            - 用户名密码
            - Token

            ## 保存位置

            系统配置会保存在：

            ```text
            data/config.json
            ```

            Git 同步下来的内容会保存在：

            ```text
            data/{源名称}/
            ```
            """,
            cancellationToken);

        await WriteIfMissingAsync(
            Path.Combine(_defaultDocsPath, "03-markdown-examples", "markdown-demo.md"),
            """
            # Markdown 语法演示

            ## 标题层级

            ### 三级标题示例

            #### 四级标题示例

            ## 列表

            - 无序列表项 A
            - 无序列表项 B
            - 无序列表项 C

            1. 有序列表 1
            2. 有序列表 2
            3. 有序列表 3

            ## 任务列表

            - [x] 完成基础布局
            - [x] 接入 Markdown 渲染
            - [ ] 完善 Git 稀疏检出

            ## 引用

            > 这是一个引用块，用来确认主题样式和左侧强调边框是否正常。

            ## 表格

            | 功能 | 状态 | 说明 |
            | --- | --- | --- |
            | 登录页 | 已完成 | 静态 HTML 登录页 |
            | Markdown 渲染 | 已完成 | 使用 Markdig |
            | 代码高亮 | 已完成 | 使用 highlight.js |

            ## 代码块

            ```csharp
            var sources = await apiClient.GetConfigAsync();
            Console.WriteLine($"当前共有 {sources.Sources.Count} 个文档源");
            ```

            ```json
            {
              "name": "默认文档源",
              "kind": "Local",
              "localPath": "C:/Docs/Default"
            }
            ```

            ```bash
            dotnet run --project MarkDownViewer/MarkDownViewer/MarkDownViewer.csproj
            ```

            ## 脚注

            这里是一段带脚注的文本。[^1]

            [^1]: 脚注由 Markdig 的扩展能力提供支持。
            """,
            cancellationToken);

        await WriteIfMissingAsync(
            Path.Combine(_defaultDocsPath, "03-markdown-examples", "tables-and-task-lists.md"),
            """
            # 表格与任务列表示例

            ## 任务进度

            - [x] 项目骨架初始化
            - [x] 登录与鉴权
            - [x] 文档浏览与渲染
            - [ ] 更完整的 Git 稀疏检出

            ## 对比表

            | 模式 | 优点 | 注意事项 |
            | --- | --- | --- |
            | 本地路径 | 配置简单、读取直接 | 依赖服务器已有目录 |
            | Git 仓库 | 易于集中管理和同步 | 需要处理认证与拉取策略 |

            ## 小提示

            如果你想测试搜索，可以在左侧搜索框里输入：

            - `任务`
            - `Git`
            - `Markdown`
            """,
            cancellationToken);

        await WriteIfMissingAsync(
            Path.Combine(_defaultDocsPath, "04-git-source", "git-sync-guide.md"),
            """
            # Git 文档源同步说明

            ## 工作方式

            系统会根据配置的拉取间隔，自动对 Git 文档源执行：

            1. 首次 clone
            2. 后续 pull
            3. 同步完成后更新可浏览内容

            ## 手动同步

            如果当前选中的文档源是 Git 类型，顶部会显示“立即同步”按钮。

            ## 推荐场景

            - 团队知识库
            - 项目设计文档
            - 运维手册
            - API 文档仓库

            ## 建议

            如果文档仓库比较大，优先把 Markdown 文档整理到固定子目录，再在配置里填写 `SubDirectory`，便于后续优化同步范围。
            """,
            cancellationToken);
    }

    private static async Task WriteIfMissingAsync(string path, string content, CancellationToken cancellationToken)
    {
        if (File.Exists(path))
        {
            return;
        }

        await File.WriteAllTextAsync(path, content, cancellationToken);
    }

    public sealed record BootstrapConfigResult(AppConfigDto Config, bool Changed);
}
