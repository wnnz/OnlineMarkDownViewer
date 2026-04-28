using System.Text.Json;
using System.Text.Json.Serialization;
using MarkDownViewer.Contracts;

namespace MarkDownViewer.Services;

public sealed class AppConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _configPath;

    public AppConfigService(IHostEnvironment hostEnvironment)
    {
        var dataDirectory = Path.Combine(hostEnvironment.ContentRootPath, "data");
        Directory.CreateDirectory(dataDirectory);
        _configPath = Path.Combine(dataDirectory, "config.json");
    }

    public async Task<AppConfigDto> GetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_configPath))
            {
                var empty = new AppConfigDto();
                await WriteCoreAsync(empty, cancellationToken);
                return empty;
            }

            await using var stream = File.OpenRead(_configPath);
            var config = await JsonSerializer.DeserializeAsync<AppConfigDto>(stream, JsonOptions, cancellationToken) ?? new AppConfigDto();
            return Normalize(config);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AppConfigDto> SaveAsync(AppConfigDto config, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(config);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await WriteCoreAsync(normalized, cancellationToken);
            return normalized;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WriteCoreAsync(AppConfigDto config, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_configPath);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken);
    }

    private static AppConfigDto Normalize(AppConfigDto config)
    {
        var normalized = new AppConfigDto();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in config.Sources)
        {
            var item = NormalizeSource(source);
            if (!usedNames.Add(item.Name))
            {
                throw new ArgumentException($"文档源“{item.Name}”重复，请保持名称唯一。");
            }

            normalized.Sources.Add(item);
        }

        return normalized;
    }

    private static DocumentSourceDto NormalizeSource(DocumentSourceDto source)
    {
        var normalized = new DocumentSourceDto
        {
            Id = string.IsNullOrWhiteSpace(source.Id) ? Guid.NewGuid().ToString("N") : source.Id.Trim(),
            Name = source.Name.Trim(),
            Kind = source.Kind,
            LocalPath = NormalizeOptional(source.LocalPath),
            GitRepositoryUrl = NormalizeOptional(source.GitRepositoryUrl),
            PullIntervalMinutes = Math.Clamp(source.PullIntervalMinutes <= 0 ? 30 : source.PullIntervalMinutes, 1, 1440),
            SubDirectory = NormalizeRelative(source.SubDirectory),
            GitAuthMode = source.GitAuthMode,
            GitUserName = NormalizeOptional(source.GitUserName),
            GitPassword = NormalizeOptional(source.GitPassword),
            GitToken = NormalizeOptional(source.GitToken)
        };

        if (string.IsNullOrWhiteSpace(normalized.Name))
        {
            throw new ArgumentException("文档源名称不能为空。");
        }

        if (normalized.Kind == DocumentSourceKind.Local)
        {
            if (string.IsNullOrWhiteSpace(normalized.LocalPath) || !Path.IsPathRooted(normalized.LocalPath))
            {
                throw new ArgumentException($"本地文档源“{normalized.Name}”必须填写服务器上的绝对路径。");
            }

            normalized.GitRepositoryUrl = null;
            normalized.SubDirectory = null;
            normalized.GitAuthMode = GitAuthMode.None;
            normalized.GitUserName = null;
            normalized.GitPassword = null;
            normalized.GitToken = null;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(normalized.GitRepositoryUrl))
            {
                throw new ArgumentException($"Git 文档源“{normalized.Name}”必须填写仓库地址。");
            }

            normalized.LocalPath = null;

            switch (normalized.GitAuthMode)
            {
                case GitAuthMode.None:
                    normalized.GitUserName = null;
                    normalized.GitPassword = null;
                    normalized.GitToken = null;
                    break;
                case GitAuthMode.UsernamePassword:
                    if (string.IsNullOrWhiteSpace(normalized.GitUserName) || string.IsNullOrWhiteSpace(normalized.GitPassword))
                    {
                        throw new ArgumentException($"Git 文档源“{normalized.Name}”需要填写用户名和密码。");
                    }

                    normalized.GitToken = null;
                    break;
                case GitAuthMode.Token:
                    if (string.IsNullOrWhiteSpace(normalized.GitToken))
                    {
                        throw new ArgumentException($"Git 文档源“{normalized.Name}”需要填写访问令牌。");
                    }

                    normalized.GitUserName = null;
                    normalized.GitPassword = null;
                    break;
            }
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeRelative(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Replace('\\', '/').Trim('/');
}
