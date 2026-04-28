using MarkDownViewer.Contracts;

namespace MarkDownViewer.Services;

public sealed class DocumentLibraryService
{
    private readonly AppConfigService _configService;
    private readonly GitSyncService _gitSyncService;
    private readonly IHostEnvironment _hostEnvironment;

    public DocumentLibraryService(
        AppConfigService configService,
        GitSyncService gitSyncService,
        IHostEnvironment hostEnvironment)
    {
        _configService = configService;
        _gitSyncService = gitSyncService;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<DirectoryBrowseResponse> BrowseAsync(string sourceId, string? relativePath, CancellationToken cancellationToken)
    {
        var source = await GetSourceAsync(sourceId, cancellationToken);
        var rootPath = await ResolveBrowseRootAsync(source, cancellationToken);
        var currentDirectory = ResolveWithinRoot(rootPath, relativePath, mustBeDirectory: true);

        var directories = Directory.EnumerateDirectories(currentDirectory)
            .Select(path =>
            {
                var info = new DirectoryInfo(path);
                return new DirectoryEntryDto
                {
                    Name = info.Name,
                    RelativePath = ToRelativePath(rootPath, info.FullName),
                    IsDirectory = true,
                    ModifiedAt = info.LastWriteTimeUtc
                };
            })
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);

        var files = Directory.EnumerateFiles(currentDirectory)
            .Where(IsMarkdownFile)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new DirectoryEntryDto
                {
                    Name = info.Name,
                    RelativePath = ToRelativePath(rootPath, info.FullName),
                    IsDirectory = false,
                    Size = info.Length,
                    ModifiedAt = info.LastWriteTimeUtc
                };
            })
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);

        return new DirectoryBrowseResponse
        {
            SourceId = source.Id,
            SourceName = source.Name,
            CurrentPath = NormalizeRelativePath(relativePath),
            Breadcrumbs = BuildBreadcrumbs(NormalizeRelativePath(relativePath)),
            Entries = directories.Concat(files).ToList()
        };
    }

    public async Task<List<DirectoryEntryDto>> SearchAsync(string sourceId, string query, CancellationToken cancellationToken)
    {
        var keyword = query.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return [];
        }

        var source = await GetSourceAsync(sourceId, cancellationToken);
        var rootPath = await ResolveBrowseRootAsync(source, cancellationToken);

        var results = new List<DirectoryEntryDto>();
        foreach (var path in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories).Where(IsMarkdownFile))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(path);
            var nameMatch = fileName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
            var contentMatch = false;

            if (!nameMatch)
            {
                var content = await File.ReadAllTextAsync(path, cancellationToken);
                contentMatch = content.Contains(keyword, StringComparison.OrdinalIgnoreCase);
            }

            if (!nameMatch && !contentMatch)
            {
                continue;
            }

            var info = new FileInfo(path);
            results.Add(new DirectoryEntryDto
            {
                Name = info.Name,
                RelativePath = ToRelativePath(rootPath, info.FullName),
                DisplayPath = ToRelativePath(rootPath, info.DirectoryName ?? rootPath),
                IsDirectory = false,
                IsContentMatch = contentMatch && !nameMatch,
                Size = info.Length,
                ModifiedAt = info.LastWriteTimeUtc
            });
        }

        return results
            .OrderByDescending(item => !item.IsContentMatch)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<MarkdownDocumentDto> ReadFileAsync(string sourceId, string relativePath, CancellationToken cancellationToken)
    {
        var source = await GetSourceAsync(sourceId, cancellationToken);
        var rootPath = await ResolveBrowseRootAsync(source, cancellationToken);
        var fullPath = ResolveWithinRoot(rootPath, relativePath, mustBeFile: true);

        if (!IsMarkdownFile(fullPath))
        {
            throw new ArgumentException("当前仅支持打开 Markdown 文件。");
        }

        var info = new FileInfo(fullPath);
        return new MarkdownDocumentDto
        {
            SourceId = source.Id,
            SourceName = source.Name,
            Name = info.Name,
            RelativePath = ToRelativePath(rootPath, info.FullName),
            Size = info.Length,
            ModifiedAt = info.LastWriteTimeUtc,
            Content = await File.ReadAllTextAsync(fullPath, cancellationToken)
        };
    }

    private async Task<DocumentSourceDto> GetSourceAsync(string sourceId, CancellationToken cancellationToken)
    {
        var config = await _configService.GetAsync(cancellationToken);
        var source = config.Sources.FirstOrDefault(item => string.Equals(item.Id, sourceId, StringComparison.OrdinalIgnoreCase));
        return source ?? throw new ArgumentException("未找到指定的文档源。");
    }

    private async Task<string> ResolveBrowseRootAsync(DocumentSourceDto source, CancellationToken cancellationToken)
    {
        if (source.Kind == DocumentSourceKind.Git)
        {
            await _gitSyncService.EnsureInitializedAsync(source, cancellationToken);
        }

        var storageRoot = source.Kind == DocumentSourceKind.Local
            ? source.LocalPath!
            : Path.Combine(_hostEnvironment.ContentRootPath, "data", GitSyncService.GetSafeStorageName(source.Name));

        if (!Directory.Exists(storageRoot))
        {
            throw new DirectoryNotFoundException($"文档源目录不存在：{storageRoot}");
        }

        if (source.Kind == DocumentSourceKind.Git && !string.IsNullOrWhiteSpace(source.SubDirectory))
        {
            storageRoot = ResolveWithinRoot(storageRoot, source.SubDirectory, mustBeDirectory: true);
        }

        return storageRoot;
    }

    private static string ResolveWithinRoot(
        string rootPath,
        string? relativePath,
        bool mustBeDirectory = false,
        bool mustBeFile = false)
    {
        var normalizedRoot = Path.GetFullPath(rootPath);
        var cleanRelativePath = NormalizeRelativePath(relativePath)
            .Replace('/', Path.DirectorySeparatorChar);
        var combinedPath = string.IsNullOrWhiteSpace(cleanRelativePath)
            ? normalizedRoot
            : Path.GetFullPath(Path.Combine(normalizedRoot, cleanRelativePath));

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!combinedPath.StartsWith(normalizedRoot, comparison))
        {
            throw new ArgumentException("检测到非法路径访问。");
        }

        if (mustBeDirectory && !Directory.Exists(combinedPath))
        {
            throw new DirectoryNotFoundException($"目录不存在：{relativePath ?? "/"}");
        }

        if (mustBeFile && !File.Exists(combinedPath))
        {
            throw new FileNotFoundException("文件不存在。", combinedPath);
        }

        return combinedPath;
    }

    private static string NormalizeRelativePath(string? relativePath) =>
        string.IsNullOrWhiteSpace(relativePath)
            ? string.Empty
            : relativePath.Trim().Replace('\\', '/').Trim('/');

    private static List<BreadcrumbItemDto> BuildBreadcrumbs(string relativePath)
    {
        var result = new List<BreadcrumbItemDto>
        {
            new("根目录", string.Empty)
        };

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return result;
        }

        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var currentPath = string.Empty;
        foreach (var segment in segments)
        {
            currentPath = string.IsNullOrWhiteSpace(currentPath) ? segment : $"{currentPath}/{segment}";
            result.Add(new BreadcrumbItemDto(segment, currentPath));
        }

        return result;
    }

    private static string ToRelativePath(string rootPath, string targetPath)
    {
        var relative = Path.GetRelativePath(rootPath, targetPath).Replace('\\', '/');
        return relative == "." ? string.Empty : relative;
    }

    private static bool IsMarkdownFile(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase);
    }
}
