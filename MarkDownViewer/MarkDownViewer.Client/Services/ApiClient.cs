using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarkDownViewer.Client.Contracts;

namespace MarkDownViewer.Client.Services;

public sealed class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly HttpClient _httpClient;
    private readonly ClientAuthService _authService;

    public ApiClient(HttpClient httpClient, ClientAuthService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    public Task<AppConfigDto> GetConfigAsync(CancellationToken cancellationToken = default) =>
        SendAsync<AppConfigDto>(HttpMethod.Get, "/api/config", null, cancellationToken);

    public Task<AppConfigDto> SaveConfigAsync(AppConfigDto config, CancellationToken cancellationToken = default) =>
        SendAsync<AppConfigDto>(HttpMethod.Put, "/api/config", config, cancellationToken);

    public Task<DirectoryBrowseResponse> BrowseAsync(string sourceId, string? relativePath, CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(relativePath) ? string.Empty : Uri.EscapeDataString(relativePath);
        return SendAsync<DirectoryBrowseResponse>(HttpMethod.Get, $"/api/documents/browse?sourceId={Uri.EscapeDataString(sourceId)}&path={path}", null, cancellationToken);
    }

    public Task<List<DirectoryEntryDto>> SearchAsync(string sourceId, string query, CancellationToken cancellationToken = default) =>
        SendAsync<List<DirectoryEntryDto>>(HttpMethod.Get, $"/api/documents/search?sourceId={Uri.EscapeDataString(sourceId)}&query={Uri.EscapeDataString(query)}", null, cancellationToken);

    public Task<MarkdownDocumentDto> GetDocumentAsync(string sourceId, string relativePath, CancellationToken cancellationToken = default) =>
        SendAsync<MarkdownDocumentDto>(HttpMethod.Get, $"/api/documents/content?sourceId={Uri.EscapeDataString(sourceId)}&path={Uri.EscapeDataString(relativePath)}", null, cancellationToken);

    public Task<GitSyncResponse> SyncAsync(string sourceId, CancellationToken cancellationToken = default) =>
        SendAsync<GitSyncResponse>(HttpMethod.Post, $"/api/documents/sync/{Uri.EscapeDataString(sourceId)}", null, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string uri, object? payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        var token = await _authService.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload, options: JsonOptions);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _authService.ClearAsync();
            throw new UnauthorizedAccessException("当前登录状态已失效。");
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions, cancellationToken);
            throw new InvalidOperationException(error?.Message ?? "接口调用失败。");
        }

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("接口返回了空数据。");
    }
}
