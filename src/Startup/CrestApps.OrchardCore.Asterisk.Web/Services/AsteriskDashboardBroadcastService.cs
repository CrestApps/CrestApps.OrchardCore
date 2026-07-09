using System.Threading.Channels;
using CrestApps.OrchardCore.Asterisk.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.Asterisk.Web.Services;

/// <summary>
/// Pushes live dashboard snapshots to connected browsers and performs slower reconciliation refreshes.
/// </summary>
public sealed class AsteriskDashboardBroadcastService : BackgroundService
{
    private static readonly TimeSpan EventCoalescingDelay = TimeSpan.FromMilliseconds(250);

    private readonly AsteriskDiagnosticsService _asteriskDiagnosticsService;
    private readonly IHubContext<AsteriskDashboardHub> _hubContext;
    private readonly Channel<bool> _refreshSignals = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropOldest,
    });

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskDashboardBroadcastService"/> class.
    /// </summary>
    /// <param name="asteriskDiagnosticsService">The diagnostics snapshot provider.</param>
    /// <param name="hubContext">The SignalR hub context.</param>
    public AsteriskDashboardBroadcastService(
        AsteriskDiagnosticsService asteriskDiagnosticsService,
        IHubContext<AsteriskDashboardHub> hubContext)
    {
        _asteriskDiagnosticsService = asteriskDiagnosticsService;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Requests an immediate dashboard refresh and broadcast.
    /// </summary>
    public void RequestRefresh()
    {
        _refreshSignals.Writer.TryWrite(true);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reconciliationInterval = TimeSpan.FromSeconds(_asteriskDiagnosticsService.RefreshSeconds);
        var refreshSignalTask = _refreshSignals.Reader.ReadAsync(stoppingToken).AsTask();

        await BroadcastSnapshotAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var timerTask = Task.Delay(reconciliationInterval, stoppingToken);
            var completedTask = await Task.WhenAny(refreshSignalTask, timerTask);

            if (completedTask == refreshSignalTask)
            {
                await refreshSignalTask;
                refreshSignalTask = _refreshSignals.Reader.ReadAsync(stoppingToken).AsTask();
                await Task.Delay(EventCoalescingDelay, stoppingToken);

                while (_refreshSignals.Reader.TryRead(out _))
                {
                }
            }

            await BroadcastSnapshotAsync(stoppingToken);
        }
    }

    private async Task BroadcastSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _asteriskDiagnosticsService.RefreshAsync(cancellationToken);

        await _hubContext.Clients.All.SendAsync("dashboardSnapshot", snapshot, cancellationToken);
    }
}
