using System.Collections.Concurrent;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using MarkDownViewer.Contracts;
using Credentials = LibGit2Sharp.UsernamePasswordCredentials;

namespace MarkDownViewer.Services;

public sealed class GitSyncService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sourceLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<GitSyncService> _logger;
    private readonly IHostEnvironment _hostEnvironment;

    public GitSyncService(ILogger<GitSyncService> logger, IHostEnvironment hostEnvironment)
    {
        _logger = logger;
        _hostEnvironment = hostEnvironment;
    }

    public async Task EnsureInitializedAsync(DocumentSourceDto source, CancellationToken cancellationToken)
    {
        var repositoryPath = GetRepositoryPath(source);
        if (source.Kind != DocumentSourceKind.Git || Directory.Exists(Path.Combine(repositoryPath, ".git")))
        {
            return;
        }

        await SyncAsync(source, cancellationToken);
    }

    public async Task<GitSyncResponse> SyncAsync(DocumentSourceDto source, CancellationToken cancellationToken)
    {
        if (source.Kind != DocumentSourceKind.Git)
        {
            throw new ArgumentException("只有 Git 文档源支持同步。");
        }

        var sourceLock = _sourceLocks.GetOrAdd(source.Id, _ => new SemaphoreSlim(1, 1));
        await sourceLock.WaitAsync(cancellationToken);

        try
        {
            return await Task.Run(() => SyncCore(source), cancellationToken);
        }
        finally
        {
            sourceLock.Release();
        }
    }

    public static string GetSafeStorageName(string sourceName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(sourceName
            .Trim()
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "unnamed-source" : sanitized;
    }

    private GitSyncResponse SyncCore(DocumentSourceDto source)
    {
        var repositoryPath = GetRepositoryPath(source);
        Directory.CreateDirectory(Path.GetDirectoryName(repositoryPath)!);

        try
        {
            if (!Directory.Exists(repositoryPath))
            {
                CloneRepository(source, repositoryPath);
            }
            else if (!Directory.Exists(Path.Combine(repositoryPath, ".git")))
            {
                throw new ArgumentException($"Git 源目录“{repositoryPath}”已存在，但不是一个有效的仓库。");
            }
            else
            {
                PullRepository(source, repositoryPath);
            }

            if (!string.IsNullOrWhiteSpace(source.SubDirectory))
            {
                var subDirectory = Path.Combine(repositoryPath, source.SubDirectory.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(subDirectory))
                {
                    throw new ArgumentException($"仓库中不存在子目录“{source.SubDirectory}”。");
                }
            }

            var message = string.IsNullOrWhiteSpace(source.SubDirectory)
                ? $"Git 文档源“{source.Name}”同步完成。"
                : $"Git 文档源“{source.Name}”同步完成，当前浏览子目录“{source.SubDirectory}”。";

            return new GitSyncResponse(true, message, DateTimeOffset.Now);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "同步 Git 文档源 {SourceName} 失败。", source.Name);
            throw new ArgumentException($"Git 同步失败：{exception.Message}");
        }
    }

    private void CloneRepository(DocumentSourceDto source, string repositoryPath)
    {
        var options = new CloneOptions(new FetchOptions
        {
            CredentialsProvider = BuildCredentialsProvider(source)
        })
        {
            Checkout = true
        };

        Repository.Clone(source.GitRepositoryUrl!, repositoryPath, options);
    }

    private void PullRepository(DocumentSourceDto source, string repositoryPath)
    {
        using var repository = new Repository(repositoryPath);
        if (repository.Network.Remotes["origin"] is { } remote &&
            !string.Equals(remote.Url, source.GitRepositoryUrl, StringComparison.OrdinalIgnoreCase))
        {
            repository.Network.Remotes.Update("origin", updater => updater.Url = source.GitRepositoryUrl);
        }

        var signature = new Signature("MD Viewer", "robot@mdviewer.local", DateTimeOffset.Now);
        var pullOptions = new PullOptions
        {
            FetchOptions = new FetchOptions
            {
                CredentialsProvider = BuildCredentialsProvider(source)
            }
        };

        Commands.Pull(repository, signature, pullOptions);
    }

    private CredentialsHandler? BuildCredentialsProvider(DocumentSourceDto source)
    {
        return source.GitAuthMode switch
        {
            GitAuthMode.None => null,
            GitAuthMode.UsernamePassword => (_, _, _) => new Credentials
            {
                Username = source.GitUserName,
                Password = source.GitPassword
            },
            GitAuthMode.Token => (_, _, _) => new Credentials
            {
                Username = "oauth2",
                Password = source.GitToken
            },
            _ => null
        };
    }

    private string GetRepositoryPath(DocumentSourceDto source) =>
        Path.Combine(_hostEnvironment.ContentRootPath, "data", GetSafeStorageName(source.Name));
}
