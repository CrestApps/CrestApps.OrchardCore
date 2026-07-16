using System.Threading.Channels;
using CrestApps.OrchardCore.Asterisk.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Asterisk.Web.Services;

/// <summary>
/// Pushes live dashboard snapshots to connected browsers and performs slower reconciliation refreshes.
/// </summary>
public sealed class AsteriskDashboardBroadcastService : BackgroundService
{
    private static readonly TimeSpan EventCoalescingDelay = TimeSpan.FromMilliseconds(50);

    private readonly AsteriskDiagnosticsService _asteriskDiagnosticsService;
    private readonly IHubContext<AsteriskDashboardHub> _hubContext;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Channel<string> _refreshSignals = Channel.CreateBounded<string>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropOldest,
    });

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskDashboardBroadcastService"/> class.
    /// </summary>
    /// <param name="asteriskDiagnosticsService">The diagnostics snapshot provider.</param>
    /// <param name="hubContext">The SignalR hub context.</param>
    /// <param name="timeProvider">The time provider used to measure refresh latency.</param>
    /// <param name="logger">The logger.</param>
    public AsteriskDashboardBroadcastService(
        AsteriskDiagnosticsService asteriskDiagnosticsService,
        IHubContext<AsteriskDashboardHub> hubContext,
        TimeProvider timeProvider,
        ILogger<AsteriskDashboardBroadcastService> logger)
    {
        _asteriskDiagnosticsService = asteriskDiagnosticsService;
        _hubContext = hubContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Requests an immediate dashboard refresh and broadcast.
    /// </summary>
    /// <param name="source">The event or action that requested the refresh.</param>
    public void RequestRefresh(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        if (!_refreshSignals.Writer.TryWrite(source))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Coalesced Asterisk dashboard refresh request from '{RefreshSource}' because another refresh is already pending.",
                    source);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reconciliationInterval = TimeSpan.FromSeconds(_asteriskDiagnosticsService.RefreshSeconds);
        var refreshSignalTask = _refreshSignals.Reader.ReadAsync(stoppingToken).AsTask();

        await BroadcastSnapshotAsync("startup", stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var timerTask = Task.Delay(reconciliationInterval, stoppingToken);
            var completedTask = await Task.WhenAny(refreshSignalTask, timerTask);

            var refreshSource = "periodic reconciliation";

            if (completedTask == refreshSignalTask)
            {
                refreshSource = await refreshSignalTask;
                refreshSignalTask = _refreshSignals.Reader.ReadAsync(stoppingToken).AsTask();
                await Task.Delay(EventCoalescingDelay, stoppingToken);

                while (_refreshSignals.Reader.TryRead(out _))
                {
                }
            }

            await BroadcastSnapshotAsync(refreshSource, stoppingToken);
        }
    }

    private async Task BroadcastSnapshotAsync(string source, CancellationToken cancellationToken)
    {
        var startedTimestamp = _timeProvider.GetTimestamp();
        var snapshot = await _asteriskDiagnosticsService.RefreshAsync(cancellationToken);
        var snapshotElapsed = _timeProvider.GetElapsedTime(startedTimestamp);

        await _hubContext.Clients.All.SendAsync("dashboardSnapshot", snapshot, cancellationToken);
        var broadcastElapsed = _timeProvider.GetElapsedTime(startedTimestamp);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Broadcast Asterisk dashboard snapshot requested by '{RefreshSource}' in {BroadcastMilliseconds} ms; snapshot acquisition took {SnapshotMilliseconds} ms. Channels: {ChannelCount}. Bridges: {BridgeCount}. Logical calls: {CallCount}.",
                source,
                broadcastElapsed.TotalMilliseconds,
                snapshotElapsed.TotalMilliseconds,
                snapshot.ChannelCount,
                snapshot.BridgeCount,
                snapshot.ActiveCallCount);
        }
    }
}
