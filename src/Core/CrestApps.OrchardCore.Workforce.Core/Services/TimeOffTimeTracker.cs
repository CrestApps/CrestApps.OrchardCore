using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Approved time off in Time Tracker: each day off of a worker with a login is kept in the stage of its type, so the
/// time report counts it with worked time.
/// </summary>
public static class TimeOffTimeTracker
{
    /// <summary>
    /// The id time-off entries are kept under, in <see cref="TimeEntry.ManagedBy"/>.
    /// </summary>
    public const string ClockId = "Workforce.TimeOff";
}

/// <summary>
/// Where time off is corrected, for Time Tracker's link.
/// </summary>
public sealed class TimeOffTimeTrackerOptions
{
    /// <summary>
    /// Gets or sets the time-off requests page.
    /// </summary>
    public string ManageUrl { get; set; }
}

/// <summary>
/// Rebuilds a worker's time-off entries over some days from their approved requests.
/// </summary>
internal sealed class TimeOffTimeTrackerSync
{
    // A full day off starts at this time in the request's zone; a part day keeps its own hours.
    private static readonly TimeOnly _dayStart = new(9, 0);

    private readonly WorkerStore _workers;
    private readonly TimeOffRequestStore _requests;
    private readonly TimeOffTypeStore _types;
    private readonly ITimeTrackerService _tracker;

    public TimeOffTimeTrackerSync(WorkerStore workers, TimeOffRequestStore requests, TimeOffTypeStore types, ITimeTrackerService tracker)
    {
        _workers = workers;
        _requests = requests;
        _types = types;
        _tracker = tracker;
    }

    /// <summary>
    /// Rebuilds a worker's time-off entries on some days.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="from">The first day.</param>
    /// <param name="to">The last day.</param>
    /// <param name="changed">A request whose new status isn't saved yet, which counts as it is now.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The result, or <see langword="null"/> when the worker has no login.</returns>
    public async Task<TimeTrackerResult> SyncAsync(string workerId, DateOnly from, DateOnly to, TimeOffRequest changed = null, CancellationToken cancellationToken = default)
    {
        var worker = await _workers.FindByIdAsync(workerId, cancellationToken);

        if (worker is null || string.IsNullOrEmpty(worker.UserId))
        {
            return null;
        }

        var zone = changed?.TimeZoneId;

        if (string.IsNullOrEmpty(zone))
        {
            zone = (await _requests.ListLiveForWorkerAsync(workerId, from, to, cancellationToken)).Select(request => request.TimeZoneId).FirstOrDefault(id => !string.IsNullOrEmpty(id)) ?? "UTC";
        }

        return await _tracker.SyncAsync(new TimeEntrySync
        {
            UserId = worker.UserId,
            UserName = worker.DisplayName,
            ManagedBy = TimeOffTimeTracker.ClockId,
            Source = TimeTrackerConstants.Sources.TimeOff,
            FromUtc = CalendarTime.ToUtc(from.ToDateTime(TimeOnly.MinValue), zone),
            ToUtc = CalendarTime.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone),
            BuildAsync = async token =>
            {
                var approved = (await _requests.ListLiveForWorkerAsync(workerId, from, to, token))
                    .Where(request => request.Status == TimeOffRequestStatus.Approved && request.ItemId != changed?.ItemId)
                    .ToList();

                if (changed?.Status == TimeOffRequestStatus.Approved)
                {
                    approved.Add(changed);
                }

                var entries = new List<TimeEntry>();

                foreach (var request in approved)
                {
                    if (await _types.FindByIdAsync(request.TypeId, token) is not { StageId: { Length: > 0 } stageId } type)
                    {
                        continue;
                    }

                    entries.AddRange(Build(request, type, stageId));
                }

                return entries;
            },
        }, cancellationToken);
    }

    // A day off from 9:00 for its minutes, or a part day ending at its end.
    internal static IEnumerable<TimeEntry> Build(TimeOffRequest request, TimeOffType type, string stageId)
    {
        var zone = string.IsNullOrEmpty(request.TimeZoneId) ? "UTC" : request.TimeZoneId;

        foreach (var day in request.Days)
        {
            var minutes = day.Minutes > 0 ? day.Minutes : (int)Math.Round(day.Amount * (type.Unit == TimeOffUnit.Days ? type.HoursPerDay : 1) * 60);

            if (minutes <= 0)
            {
                continue;
            }

            // A part day ends at its end: the minutes counted are its overlap with the shift, which ends there too.
            var start = request.IsPartialDay
                ? CalendarTime.ToUtc(day.Date.ToDateTime(request.EndTime.Value), zone).AddMinutes(-minutes)
                : CalendarTime.ToUtc(day.Date.ToDateTime(_dayStart), zone);

            if (request.IsPartialDay && CalendarTime.ToUtc(day.Date.ToDateTime(request.StartTime.Value), zone) is var requested && requested > start)
            {
                start = requested;
            }

            yield return new TimeEntry
            {
                StageId = stageId,
                StartUtc = start,
                EndUtc = start.AddMinutes(minutes),
                Source = TimeTrackerConstants.Sources.TimeOff,
                SourceRef = request.ItemId,
                Note = type.Name,
            };
        }
    }
}

/// <summary>
/// Keeps Time Tracker in step after time off is approved, canceled or otherwise changed and saved. It runs after the
/// request is saved because the sync saves its own work.
/// </summary>
internal sealed class TimeOffTimeTrackerObserver : ITimeOffRequestObserver
{
    private readonly TimeOffTimeTrackerSync _sync;

    public TimeOffTimeTrackerObserver(TimeOffTimeTrackerSync sync)
    {
        _sync = sync;
    }

    public Task ChangedAsync(TimeOffRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _sync.SyncAsync(request.WorkerId, request.StartDate, request.EndDate, request, cancellationToken);
    }
}

/// <summary>
/// Brings the days of approved time off into Time Tracker as they come: entries aren't kept ahead of their time.
/// </summary>
internal sealed class TimeOffTimeTrackerRefresher
{
    private readonly TimeOffRequestStore _requests;
    private readonly TimeOffTimeTrackerSync _sync;

    public TimeOffTimeTrackerRefresher(TimeOffRequestStore requests, TimeOffTimeTrackerSync sync)
    {
        _requests = requests;
        _sync = sync;
    }

    /// <summary>
    /// Syncs the workers with approved time off yesterday or today.
    /// </summary>
    /// <param name="today">The site's today.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many workers were synced.</returns>
    public async Task<int> RunAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var from = today.AddDays(-1);
        var workers = (await _requests.ListApprovedAsync(from, today, cancellationToken)).Select(request => request.WorkerId).Distinct(StringComparer.Ordinal).ToList();

        foreach (var workerId in workers)
        {
            await _sync.SyncAsync(workerId, from, today, cancellationToken: cancellationToken);
        }

        return workers.Count;
    }
}

/// <summary>
/// Gives a time-off type a Time Tracker stage: one with its name, or a new one counting toward <c>Time off</c>, and
/// <c>Paid</c> when the type is paid.
/// </summary>
internal sealed class TimeOffTypeStageAttacher
{
    private readonly ITimeStageManager _stages;
    private readonly ITimeCategoryManager _categories;

    public TimeOffTypeStageAttacher(ITimeStageManager stages, ITimeCategoryManager categories)
    {
        _stages = stages;
        _categories = categories;
    }

    public async Task AttachAsync(TimeOffType type, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(type);

        var stages = (await _stages.GetAllAsync(cancellationToken)).ToList();
        var stage = stages.FirstOrDefault(stage => string.Equals(stage.Name, type.Name, StringComparison.OrdinalIgnoreCase));

        if (stage is null)
        {
            stage = await _stages.NewAsync(cancellationToken: cancellationToken);
            stage.Name = type.Name;
            stage.Color = type.Color;
            stage.Position = 200 + (stages.Count * 10);
            stage.SelfService = false;
            stage.CategoryIds.Add((await CategoryAsync("Time off", "TIMEOFF", "#e67700", 30, cancellationToken)).ItemId);

            if (type.IsPaid)
            {
                stage.CategoryIds.Add((await CategoryAsync("Paid", "PAID", "#2b8a3e", 10, cancellationToken)).ItemId);
            }

            await _stages.CreateAsync(stage, cancellationToken: cancellationToken);
        }

        type.StageId = stage.ItemId;
    }

    private async Task<TimeCategory> CategoryAsync(string name, string code, string color, int position, CancellationToken cancellationToken)
    {
        var found = (await _categories.GetAllAsync(cancellationToken)).FirstOrDefault(category => string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(category.Code, code, StringComparison.OrdinalIgnoreCase));

        if (found is not null)
        {
            return found;
        }

        var category = await _categories.NewAsync(cancellationToken: cancellationToken);

        category.Name = name;
        category.Code = code;
        category.Color = color;
        category.Position = position;
        await _categories.CreateAsync(category, cancellationToken: cancellationToken);

        return category;
    }
}

/// <summary>
/// Gives every time-off type without a stage one.
/// </summary>
internal sealed class TimeOffTypeStageSeeder
{
    private readonly ITimeOffTypeManager _types;
    private readonly TimeOffTypeStageAttacher _attacher;

    public TimeOffTypeStageSeeder(ITimeOffTypeManager types, TimeOffTypeStageAttacher attacher)
    {
        _types = types;
        _attacher = attacher;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var type in await _types.GetAllAsync(cancellationToken))
        {
            if (!string.IsNullOrEmpty(type.StageId))
            {
                continue;
            }

            await _attacher.AttachAsync(type, cancellationToken);
            await _types.UpdateAsync(type, cancellationToken: cancellationToken);
        }
    }
}

/// <summary>
/// Gives a new time-off type its stage when it has none.
/// </summary>
internal sealed class TimeOffTypeStageHandler : CatalogEntryHandlerBase<TimeOffType>
{
    private readonly TimeOffTypeStageAttacher _attacher;

    public TimeOffTypeStageHandler(TimeOffTypeStageAttacher attacher)
    {
        _attacher = attacher;
    }

    public override async Task CreatingAsync(CreatingContext<TimeOffType> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.Model.StageId))
        {
            await _attacher.AttachAsync(context.Model, cancellationToken);
        }
    }
}

/// <summary>
/// Names approved time off as the clock that keeps its entries, corrected on the time-off pages.
/// </summary>
internal sealed class TimeOffExternalClock : IExternalTimeClock
{
    private readonly TimeOffTimeTrackerOptions _options;

    internal readonly IStringLocalizer S;

    public TimeOffExternalClock(IOptions<TimeOffTimeTrackerOptions> options, IStringLocalizer<TimeOffExternalClock> stringLocalizer)
    {
        _options = options.Value;
        S = stringLocalizer;
    }

    public Task<ExternalTimeClockInfo> FindAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<ExternalTimeClockInfo>(null);

    public Task<ExternalTimeClockInfo> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(id == TimeOffTimeTracker.ClockId
            ? new ExternalTimeClockInfo { Id = id, Name = S["approved time off"], ManageUrl = _options.ManageUrl }
            : null);
}
