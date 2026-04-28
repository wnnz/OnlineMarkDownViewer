using System.Text.Json.Serialization;

namespace MarkDownViewer.Client.Contracts;

public sealed class LoginRequest
{
    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed record LoginResponse(string Token, string UserName, DateTimeOffset ExpiresAt);

public sealed record CurrentUserResponse(string UserName);

public sealed record ApiErrorResponse(string Message);

public sealed class AppConfigDto
{
    public List<DocumentSourceDto> Sources { get; set; } = [];
}

public sealed class DocumentSourceDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public DocumentSourceKind Kind { get; set; } = DocumentSourceKind.Local;

    public string? LocalPath { get; set; }

    public string? GitRepositoryUrl { get; set; }

    public int PullIntervalMinutes { get; set; } = 30;

    public string? SubDirectory { get; set; }

    public GitAuthMode GitAuthMode { get; set; } = GitAuthMode.None;

    public string? GitUserName { get; set; }

    public string? GitPassword { get; set; }

    public string? GitToken { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DocumentSourceKind
{
    Local,
    Git
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GitAuthMode
{
    None,
    UsernamePassword,
    Token
}

public sealed record BreadcrumbItemDto(string Name, string RelativePath);

public sealed class DirectoryBrowseResponse
{
    public string SourceId { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public string CurrentPath { get; set; } = string.Empty;

    public List<BreadcrumbItemDto> Breadcrumbs { get; set; } = [];

    public List<DirectoryEntryDto> Entries { get; set; } = [];
}

public sealed class DirectoryEntryDto
{
    public string Name { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public bool IsDirectory { get; set; }

    public bool IsContentMatch { get; set; }

    public string? DisplayPath { get; set; }

    public long Size { get; set; }

    public DateTimeOffset ModifiedAt { get; set; }
}

public sealed class MarkdownDocumentDto
{
    public string SourceId { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public long Size { get; set; }

    public DateTimeOffset ModifiedAt { get; set; }

    public string Content { get; set; } = string.Empty;
}

public sealed record GitSyncResponse(bool Success, string Message, DateTimeOffset CompletedAt);
