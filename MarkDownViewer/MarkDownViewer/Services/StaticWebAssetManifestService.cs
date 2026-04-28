using System.Text.Json;

namespace MarkDownViewer.Services;

public sealed class StaticWebAssetManifestService
{
    private readonly Lazy<IReadOnlyList<string>> _frameworkAssets;

    public StaticWebAssetManifestService()
    {
        _frameworkAssets = new Lazy<IReadOnlyList<string>>(LoadFrameworkAssets);
    }

    public IReadOnlyList<string> GetFrameworkPreloadAssets() => _frameworkAssets.Value;

    private static IReadOnlyList<string> LoadFrameworkAssets()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "MarkDownViewer.staticwebassets.endpoints.json");
        if (!File.Exists(manifestPath))
        {
            return
            [
                "/_framework/blazor.webassembly.js",
                "/_framework/dotnet.js"
            ];
        }

        using var stream = File.OpenRead(manifestPath);
        using var document = JsonDocument.Parse(stream);

        var assets = document.RootElement
            .GetProperty("Endpoints")
            .EnumerateArray()
            .Select(element => element.GetProperty("Route").GetString())
            .Where(route => !string.IsNullOrWhiteSpace(route))
            .Select(route => route!)
            .Where(route => route.StartsWith("_framework/", StringComparison.Ordinal))
            .Where(route => !route.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            .Where(route => !route.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
            .Where(route => !route.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            .Where(route => !route.Contains(".lib.module.", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(route => GetPriority(route))
            .ThenBy(route => route, StringComparer.OrdinalIgnoreCase)
            .Select(route => $"/{route}")
            .ToList();

        if (assets.Count == 0)
        {
            assets.Add("/_framework/blazor.webassembly.js");
            assets.Add("/_framework/dotnet.js");
        }

        return assets;
    }

    private static int GetPriority(string route)
    {
        return route switch
        {
            "_framework/blazor.webassembly.js" => 0,
            "_framework/dotnet.js" => 1,
            _ when route.Contains(".runtime.", StringComparison.OrdinalIgnoreCase) => 2,
            _ when route.Contains(".native.", StringComparison.OrdinalIgnoreCase) => 3,
            _ when route.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase) => 4,
            _ when route.EndsWith(".dat", StringComparison.OrdinalIgnoreCase) => 5,
            _ => 6
        };
    }
}
