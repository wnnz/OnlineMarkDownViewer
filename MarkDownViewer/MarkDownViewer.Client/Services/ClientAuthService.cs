using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarkDownViewer.Client.Contracts;
using Microsoft.JSInterop;

namespace MarkDownViewer.Client.Services;

public sealed class ClientAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private string? _token;

    public ClientAuthService(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    public string CurrentUserName { get; private set; } = string.Empty;

    public bool IsInitialized { get; private set; }

    public async Task<bool> InitializeAsync()
    {
        if (IsInitialized)
        {
            return !string.IsNullOrWhiteSpace(CurrentUserName);
        }

        _token = await _jsRuntime.InvokeAsync<string?>("mdViewerAuth.getToken");
        if (string.IsNullOrWhiteSpace(_token))
        {
            IsInitialized = true;
            return false;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            await ClearAsync();
            return false;
        }

        var currentUser = await response.Content.ReadFromJsonAsync<CurrentUserResponse>(JsonOptions);
        CurrentUserName = currentUser?.UserName ?? string.Empty;
        IsInitialized = true;
        return !string.IsNullOrWhiteSpace(CurrentUserName);
    }

    public async Task<string?> GetTokenAsync()
    {
        if (!IsInitialized)
        {
            await InitializeAsync();
        }

        return _token;
    }

    public async Task ClearAsync()
    {
        _token = null;
        CurrentUserName = string.Empty;
        IsInitialized = true;
        await _jsRuntime.InvokeVoidAsync("mdViewerAuth.clearToken");
    }

    public async Task LogoutAndRedirectAsync()
    {
        await ClearAsync();
        await _jsRuntime.InvokeVoidAsync("mdViewerAuth.redirectToLogin");
    }
}
