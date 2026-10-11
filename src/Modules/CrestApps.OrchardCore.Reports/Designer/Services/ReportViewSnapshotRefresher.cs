using System.Diagnostics;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Refreshes the stored result of scheduled views. A view is run exactly as a report reads it, through
/// <see cref="ReportViewRunner"/>, with its owner's current access and the configured size limits, and keeps at most
/// <see cref="ReportQueryLimits.MaxRowsPerDataSet"/> rows. A failed refresh keeps the previous rows and records the
/// error. A distributed lock keeps two nodes from refreshing the same view at once.
/// </summary>
public sealed class ReportViewSnapshotRefresher
{
    /// <summary>
    /// How early a view counts as due, so a refresh that started a little after the scheduled run is not pushed to the
    /// next run.
    /// </summary>
    public static readonly TimeSpan DueTolerance = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(30);

    private readonly ICatalog<ReportView> _views;
    private readonly ReportViewSnapshotStore _store;
    private readonly ReportViewRunner _runner;
    private readonly ReportOwnerPrincipalResolver _owners;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDistributedLock _distributedLock;
    private readonly ReportQueryLimits _limits;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportViewSnapshotRefresher"/> class.
    /// </summary>
    /// <param name="views">The view catalog.</param>
    /// <param name="store">The snapshot store.</param>
    /// <param name="runner">The view runner.</param>
    /// <param name="owners">The resolver of a view owner's principal.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="distributedLock">The lock that keeps one view from being refreshed twice at once.</param>
    /// <param name="limits">The configured size limits.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportViewSnapshotRefresher(
        ICatalog<ReportView> views,
        ReportViewSnapshotStore store,
        ReportViewRunner runner,
        ReportOwnerPrincipalResolver owners,
        IAuthorizationService authorizationService,
        IDistributedLock distributedLock,
        IOptions<ReportQueryLimits> limits,
        IClock clock,
        ILogger<ReportViewSnapshotRefresher> logger,
        IStringLocalizer<ReportViewSnapshotRefresher> stringLocalizer)
    {
        _views = views;
        _store = store;
        _runner = runner;
        _owners = owners;
        _authorizationService = authorizationService;
        _distributedLock = distributedLock;
        _limits = limits.Value;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Determines whether a scheduled view is due for a refresh: it has no stored result yet, or its last refresh
    /// attempt is older than its schedule.
    /// </summary>
    /// <param name="view">The view.</param>
    /// <param name="status">The view's refresh status, or <see langword="null"/> when it has no stored result.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <returns><see langword="true"/> when the view should be refreshed.</returns>
    public static bool IsDue(ReportView view, ReportViewSnapshotStatus status, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.RefreshIntervalMinutes <= ReportViewRefreshIntervals.Live)
        {
            return false;
        }

        // A failed refresh waits for the next scheduled time too, so a broken view is not run on every pass.
        return status is null ||
            status.AttemptedUtc + TimeSpan.FromMinutes(ReportViewRefreshIntervals.Normalize(view.RefreshIntervalMinutes)) - DueTolerance <= utcNow;
    }

    /// <summary>
    /// Lists the scheduled views that are due for a refresh.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identifiers of the due views.</returns>
    public async Task<IReadOnlyList<string>> ListDueAsync(CancellationToken cancellationToken = default)
    {
        var views = (await _views.GetAllAsync(cancellationToken))
            .Where(view => view.RefreshIntervalMinutes > ReportViewRefreshIntervals.Live)
            .ToList();

        if (views.Count == 0)
        {
            return [];
        }

        var statuses = await _store.ListStatusesAsync();
        var utcNow = _clock.UtcNow;

        return views
            .Where(view => IsDue(view, statuses.GetValueOrDefault(view.ItemId), utcNow))
            .Select(view => view.ItemId)
            .ToArray();
    }

    /// <summary>
    /// Refreshes a view's stored result.
    /// </summary>
    /// <param name="viewId">The view identifier.</param>
    /// <param name="onlyWhenDue">Whether to skip a view that is not due, as the scheduled refresh does.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    public async Task<ReportViewRefreshResult> RefreshAsync(string viewId, bool onlyWhenDue, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(viewId);

        var view = await _views.FindByIdAsync(viewId, cancellationToken);

        if (view is null)
        {
            return new ReportViewRefreshResult { Status = ReportViewRefreshStatus.NotFound };
        }

        return await RefreshAsync(view, onlyWhenDue, cancellationToken);
    }

    /// <summary>
    /// Refreshes a view's stored result. A view that fails keeps its previous rows, and the error is recorded.
    /// </summary>
    /// <param name="view">The view.</param>
    /// <param name="onlyWhenDue">Whether to skip a view that is not due, as the scheduled refresh does.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The outcome.</returns>
    public async Task<ReportViewRefreshResult> RefreshAsync(ReportView view, bool onlyWhenDue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.RefreshIntervalMinutes <= ReportViewRefreshIntervals.Live)
        {
            return new ReportViewRefreshResult { Status = ReportViewRefreshStatus.NotScheduled };
        }

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(LockKey(view.ItemId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return new ReportViewRefreshResult { Status = ReportViewRefreshStatus.Busy };
        }

        await using (locker)
        {
            var snapshot = await _store.FindAsync(view.ItemId);
            var utcNow = _clock.UtcNow;

            // Another node may have refreshed the view while this one waited for it.
            if (onlyWhenDue && !IsDue(view, ReportViewSnapshotStatus.From(snapshot), utcNow))
            {
                return new ReportViewRefreshResult
                {
                    Status = ReportViewRefreshStatus.NotDue,
                    Snapshot = ReportViewSnapshotStatus.From(snapshot),
                };
            }

            snapshot ??= new ReportViewSnapshot { ViewId = view.ItemId };
            snapshot.AttemptedUtc = utcNow;

            var started = Stopwatch.GetTimestamp();
            var status = ReportViewRefreshStatus.Refreshed;

            try
            {
                var maxRows = Math.Max(1, _limits.MaxRowsPerDataSet);
                var result = await RunAsOwnerAsync(view, maxRows, cancellationToken);

                ReportViewSnapshotData.Fill(snapshot, result.Table, maxRows);
                snapshot.Warnings = result.Warnings;
                snapshot.RefreshedUtc = utcNow;
                snapshot.DurationMilliseconds = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                snapshot.LastError = null;
                snapshot.LastErrorUtc = null;

                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Refreshed the report view '{ViewId}': {RowCount} rows in {DurationMilliseconds} ms.", view.ItemId, snapshot.RowCount, snapshot.DurationMilliseconds);
                }
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                // The previous rows stay, so the reports that read the view keep working with older data.
                status = ReportViewRefreshStatus.Failed;
                snapshot.LastError = exception is ReportQueryException
                    ? exception.Message
                    : S["The view could not be refreshed. The error was logged."].Value;
                snapshot.LastErrorUtc = utcNow;

                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(exception, "The report view '{ViewId}' could not be refreshed.", view.ItemId);
                }
            }

            await _store.SaveAsync(snapshot, cancellationToken);

            return new ReportViewRefreshResult
            {
                Status = status,
                Snapshot = ReportViewSnapshotStatus.From(snapshot),
            };
        }
    }

    private async Task<ReportViewRunResult> RunAsOwnerAsync(ReportView view, int maxRows, CancellationToken cancellationToken)
    {
        // Nobody reads the view during a scheduled refresh, so it reads its data with its owner's current access.
        var owner = await _owners.ResolveAsync(view.OwnerId) ??
            throw new ReportQueryException(S["The owner of the view '{0}' no longer exists or is disabled.", view.DisplayText]);

        if (!await _authorizationService.AuthorizeAsync(owner, ReportDesignerPermissions.ViewAllReportDesigns, view))
        {
            throw new ReportQueryException(S["The owner of the view '{0}' can no longer read it.", view.DisplayText]);
        }

        return await _runner.RunAsync(view, new ReportDataSourceContext { User = owner }, maxRows, cancellationToken);
    }

    private static string LockKey(string viewId)
    {
        return "CrestApps.Reports.ViewSnapshot:" + viewId;
    }
}

/// <summary>
/// The outcome of refreshing a view's stored result.
/// </summary>
public sealed class ReportViewRefreshResult
{
    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public ReportViewRefreshStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the view's refresh status afterwards, or <see langword="null"/> when it has no stored result.
    /// </summary>
    public ReportViewSnapshotStatus Snapshot { get; set; }
}

/// <summary>
/// What happened when a view's stored result was refreshed.
/// </summary>
public enum ReportViewRefreshStatus
{
    /// <summary>
    /// The view ran and its result was stored.
    /// </summary>
    Refreshed,

    /// <summary>
    /// The view failed; its previous rows were kept and the error recorded.
    /// </summary>
    Failed,

    /// <summary>
    /// The view was not due.
    /// </summary>
    NotDue,

    /// <summary>
    /// The view is being refreshed elsewhere.
    /// </summary>
    Busy,

    /// <summary>
    /// The view runs live and stores no result.
    /// </summary>
    NotScheduled,

    /// <summary>
    /// The view does not exist.
    /// </summary>
    NotFound,
}
