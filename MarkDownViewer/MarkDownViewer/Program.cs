using System.Security.Claims;
using System.Text.Json.Serialization;
using MarkDownViewer.Contracts;
using MarkDownViewer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddDataProtection();
builder.Services.AddAuthentication(TokenService.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>(TokenService.AuthenticationScheme, _ => { });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<AppConfigService>();
builder.Services.AddSingleton<GitSyncService>();
builder.Services.AddSingleton<DocumentLibraryService>();
builder.Services.AddSingleton<StaticWebAssetManifestService>();
builder.Services.AddHostedService<GitSyncBackgroundService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
// 显式启用终结点路由，避免静态资源分支下的回退路由遗漏 EndpointMiddleware。
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

var authApi = app.MapGroup("/api/auth");

authApi.MapPost("/login", ([FromBody] LoginRequest request, TokenService tokenService) =>
{
    if (!string.Equals(request.UserName, "admin", StringComparison.Ordinal) ||
        !string.Equals(request.Password, "admin123", StringComparison.Ordinal))
    {
        return Results.Unauthorized();
    }

    var response = tokenService.CreateLoginResponse("admin");
    return Results.Ok(response);
});

authApi.MapGet("/me", (ClaimsPrincipal user) =>
{
    var userName = user.Identity?.Name ?? "admin";
    return Results.Ok(new CurrentUserResponse(userName));
}).RequireAuthorization();

app.MapGet("/api/runtime/preload-assets", (StaticWebAssetManifestService manifestService) =>
{
    var assets = manifestService.GetFrameworkPreloadAssets();
    return Results.Ok(assets);
});

var securedApi = app.MapGroup("/api").RequireAuthorization();

securedApi.MapGet("/config", async (AppConfigService configService, CancellationToken cancellationToken) =>
{
    var config = await configService.GetAsync(cancellationToken);
    return Results.Ok(config);
});

securedApi.MapPut("/config", async ([FromBody] AppConfigDto config, AppConfigService configService, CancellationToken cancellationToken) =>
{
    try
    {
        var savedConfig = await configService.SaveAsync(config, cancellationToken);
        return Results.Ok(savedConfig);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ApiErrorResponse(exception.Message));
    }
});

securedApi.MapGet("/documents/browse", async (
    [FromQuery] string sourceId,
    [FromQuery] string? path,
    DocumentLibraryService documentLibrary,
    CancellationToken cancellationToken) =>
{
    try
    {
        var directory = await documentLibrary.BrowseAsync(sourceId, path, cancellationToken);
        return Results.Ok(directory);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ApiErrorResponse(exception.Message));
    }
    catch (DirectoryNotFoundException exception)
    {
        return Results.NotFound(new ApiErrorResponse(exception.Message));
    }
});

securedApi.MapGet("/documents/search", async (
    [FromQuery] string sourceId,
    [FromQuery] string query,
    DocumentLibraryService documentLibrary,
    CancellationToken cancellationToken) =>
{
    try
    {
        var results = await documentLibrary.SearchAsync(sourceId, query, cancellationToken);
        return Results.Ok(results);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ApiErrorResponse(exception.Message));
    }
    catch (DirectoryNotFoundException exception)
    {
        return Results.NotFound(new ApiErrorResponse(exception.Message));
    }
});

securedApi.MapGet("/documents/content", async (
    [FromQuery] string sourceId,
    [FromQuery] string path,
    DocumentLibraryService documentLibrary,
    CancellationToken cancellationToken) =>
{
    try
    {
        var document = await documentLibrary.ReadFileAsync(sourceId, path, cancellationToken);
        return Results.Ok(document);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ApiErrorResponse(exception.Message));
    }
    catch (FileNotFoundException exception)
    {
        return Results.NotFound(new ApiErrorResponse(exception.Message));
    }
});

securedApi.MapPost("/documents/sync/{sourceId}", async (
    string sourceId,
    AppConfigService configService,
    GitSyncService gitSyncService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var config = await configService.GetAsync(cancellationToken);
        var source = config.Sources.FirstOrDefault(item => string.Equals(item.Id, sourceId, StringComparison.OrdinalIgnoreCase));
        if (source is null)
        {
            return Results.NotFound(new ApiErrorResponse("未找到指定的文档源。"));
        }

        if (source.Kind != DocumentSourceKind.Git)
        {
            return Results.BadRequest(new ApiErrorResponse("只有 Git 文档源支持立即同步。"));
        }

        var result = await gitSyncService.SyncAsync(source, cancellationToken);
        return Results.Ok(result);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ApiErrorResponse(exception.Message));
    }
});

app.MapFallbackToFile("index.html");
app.UseEndpoints(_ => { });

app.Run();
