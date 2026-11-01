using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Turns what happens to a worker's schedule into portal notifications: a publish, a change to one shift, a decision
/// on time off and a shift trade.
/// </summary>
internal sealed class PortalNotices : ISchedulePublishHandler, IScheduleChangeHandler
{
    private readonly IWorkforceNotifier _notifier;
    private readonly DraftShiftStore _draftStore;
    private readonly TeamStore _teamStore;
    private readonly TeamTimeZoneResolver _timeZones;

    internal readonly IStringLocalizer S;

    public PortalNotices(IWorkforceNotifier notifier, DraftShiftStore draftStore, TeamStore teamStore, TeamTimeZoneResolver timeZones, IStringLocalizer<PortalNotices> stringLocalizer)
    {
        _notifier = notifier;
        _draftStore = draftStore;
        _teamStore = teamStore;
        _timeZones = timeZones;
        S = stringLocalizer;
    }

    public async Task PublishedAsync(SchedulePublishedContext context, CancellationToken cancellationToken = default)
    {
        var period = context.Period;
        var team = await _teamStore.FindByIdAsync(period.TeamId, cancellationToken);
        var teamName = team?.Name ?? string.Empty;
        var range = $"{period.StartDate.ToString("d MMM", null)} - {period.EndDate.ToString("d MMM", null)}";

        if (period.PublishedVersion <= 1)
        {
            var workers = (await _draftStore.ListForScenarioAsync(period.PrimaryScenarioId, cancellationToken))
                .Select(draft => draft.WorkerId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct(StringComparer.Ordinal);

            foreach (var workerId in workers)
            {
                await NotifyAsync(workerId, WorkforceNotificationKind.SchedulePublished, S["Your {0} schedule is out", teamName], S["Your shifts for {0} are published.", range], cancellationToken);
            }

            return;
        }

        var touched = context.Result.Changes
            .SelectMany(change => new[] { change.WorkerId, change.PreviousWorkerId })
            .Where(id => !string.IsNullOrEmpty(id))
            .GroupBy(id => id, StringComparer.Ordinal);

        foreach (var group in touched)
        {
            var count = context.Result.Changes.Count(change => change.WorkerId == group.Key || change.PreviousWorkerId == group.Key);

            await NotifyAsync(group.Key, WorkforceNotificationKind.SchedulePublished, S["Your {0} schedule changed", teamName], count == 1 ? S["1 of your shifts for {0} changed.", range] : S["{0} of your shifts for {1} changed.", count, range], cancellationToken);
        }
    }

    public async Task RecordedAsync(ScheduleChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        // A republish tells each worker once, and approved time off tells them itself.
        if (change.Source is ScheduleChangeSources.Publish or ScheduleChangeSources.TimeOff)
        {
            return;
        }

        var zone = await _timeZones.GetAsync(change.TeamId, cancellationToken);
        var start = change.AfterStartUtc ?? change.BeforeStartUtc;
        var day = start.HasValue ? CalendarTime.ToLocal(start.Value, zone).ToString("ddd d MMM", null) : string.Empty;
        var hours = Hours(change.AfterStartUtc, change.AfterEndUtc, zone);

        switch (change.Kind)
        {
            case ScheduleChangeKind.Reassigned:
                await NotifyAsync(change.WorkerId, WorkforceNotificationKind.ShiftChanged, S["You have a shift on {0}", day], hours, cancellationToken);
                await NotifyAsync(change.PreviousWorkerId, WorkforceNotificationKind.ShiftChanged, S["Your shift on {0} went to someone else", day], change.Reason, cancellationToken);
                break;
            case ScheduleChangeKind.OpenShiftFilled:
                await NotifyAsync(change.WorkerId, WorkforceNotificationKind.OpenShift, S["You got the open shift on {0}", day], hours, cancellationToken);
                break;
            case ScheduleChangeKind.Swapped:
                await NotifyAsync(change.WorkerId, WorkforceNotificationKind.ShiftChanged, S["Your swap went through: you work {0}", day], hours, cancellationToken);
                break;
            case ScheduleChangeKind.Removed:
                await NotifyAsync(change.WorkerId, WorkforceNotificationKind.ShiftChanged, S["Your shift on {0} was removed", day], change.Reason, cancellationToken);
                break;
            case ScheduleChangeKind.Added:
                await NotifyAsync(change.WorkerId, WorkforceNotificationKind.ShiftChanged, S["New shift on {0}", day], hours, cancellationToken);
                break;
            default:
                await NotifyAsync(change.WorkerId, WorkforceNotificationKind.ShiftChanged, S["Your shift on {0} changed", day], hours, cancellationToken);
                break;
        }
    }

    private static string Hours(DateTime? startUtc, DateTime? endUtc, string zone)
    {
        return startUtc.HasValue && endUtc.HasValue
            ? $"{CalendarTime.ToLocal(startUtc.Value, zone):HH:mm}-{CalendarTime.ToLocal(endUtc.Value, zone):HH:mm}"
            : null;
    }

    private Task NotifyAsync(string workerId, WorkforceNotificationKind kind, string title, string body, CancellationToken cancellationToken)
    {
        return string.IsNullOrEmpty(workerId)
            ? Task.CompletedTask
            : _notifier.NotifyAsync(new WorkforceNotification
            {
                WorkerId = workerId,
                Kind = kind,
                Title = title,
                Body = body ?? string.Empty,
                Url = "my-work/schedule",
            }, cancellationToken);
    }
}
