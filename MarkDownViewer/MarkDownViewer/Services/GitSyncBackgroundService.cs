using MarkDownViewer.Contracts;

namespace MarkDownViewer.Services;

public sealed class GitSyncBackgroundService : BackgroundService
{
    private readonly Dictionary<string, DateTimeOffset> _lastSyncTimes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<GitSyncBackgroundService> _logger;
    private readonly AppConfigService _configService;
    private readonly GitSyncService _gitSyncService;

    public GitSyncBackgroundService(
        ILogger<GitSyncBackgroundService> logger,
        AppConfigService configService,
        GitSyncService gitSyncService)
    {
        _logger = logger;
        _configService = configService;
        _gitSyncService = gitSyncService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOneRoundAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOneRoundAsync(stoppingToken);
        }
    }

    private async Task RunOneRoundAsync(CancellationToken cancellationToken)
    {
        try
        {
            var config = await _configService.GetAsync(cancellationToken);
            foreach (var source in config.Sources.Where(item => item.Kind == DocumentSourceKind.Git))
            {
                var interval = TimeSpan.FromMinutes(Math.Max(1, source.PullIntervalMinutes));
                if (_lastSyncTimes.TryGetValue(source.Id, out var lastSyncTime) &&
                    DateTimeOffset.UtcNow - lastSyncTime < interval)
                {
                    continue;
                }

                _lastSyncTimes[source.Id] = DateTimeOffset.UtcNow;
                try
                {
                    await _gitSyncService.SyncAsync(source, cancellationToken);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "后台同步文档源 {SourceName} 失败。", source.Name);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "执行 Git 后台同步轮询时发生异常。");
        }
    }
}
