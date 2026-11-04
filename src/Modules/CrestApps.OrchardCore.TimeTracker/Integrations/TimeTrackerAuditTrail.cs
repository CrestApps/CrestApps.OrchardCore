using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.TimeTracker.Integrations;

/// <summary>
/// The Audit Trail events of time tracking.
/// </summary>
internal sealed class TimeTrackerAuditTrailEvents : IConfigureOptions<AuditTrailOptions>
{
    public const string CategoryName = "TimeTracker";
    public const string EntryCreated = "TimeEntryCreated";
    public const string EntryEnded = "TimeEntryEnded";
    public const string EntryEdited = "TimeEntryEdited";
    public const string EntryDeleted = "TimeEntryDeleted";

    public void Configure(AuditTrailOptions options)
    {
        options.For<TimeTrackerAuditTrailEvents>(CategoryName, S => S["Time Tracker"])
            .WithEvent(EntryCreated, S => S["Time entry recorded"], S => S["Someone clocked in, switched stage, or time was added."], enableByDefault: true)
            .WithEvent(EntryEnded, S => S["Time entry ended"], S => S["Someone switched stage or clocked out."], enableByDefault: true)
            .WithEvent(EntryEdited, S => S["Time entry changed"], S => S["Someone changed the stage, times or note of a time entry, with a reason."], enableByDefault: true)
            .WithEvent(EntryDeleted, S => S["Time entry removed"], S => S["Someone removed a time entry, with a reason."], enableByDefault: true);
    }
}

/// <summary>
/// What a time entry event records.
/// </summary>
public sealed class TimeEntryAuditData
{
    /// <summary>
    /// Gets or sets the entry as it is now, or as it was when removed.
    /// </summary>
    public TimeEntry Entry { get; set; }

    /// <summary>
    /// Gets or sets what it was before a change.
    /// </summary>
    public string Before { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }
}

internal sealed class AuditTrailTimeEntryObserver : ITimeEntryObserver
{
    private readonly IAuditTrailManager _auditTrailManager;

    public AuditTrailTimeEntryObserver(IAuditTrailManager auditTrailManager)
    {
        _auditTrailManager = auditTrailManager;
    }

    public Task OnChangedAsync(TimeEntryChangeKind kind, TimeEntry entry, string before, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var name = kind switch
        {
            TimeEntryChangeKind.Created => TimeTrackerAuditTrailEvents.EntryCreated,
            TimeEntryChangeKind.Ended => TimeTrackerAuditTrailEvents.EntryEnded,
            TimeEntryChangeKind.Edited => TimeTrackerAuditTrailEvents.EntryEdited,
            _ => TimeTrackerAuditTrailEvents.EntryDeleted,
        };

        return _auditTrailManager.RecordEventAsync(new AuditTrailContext<TimeEntryAuditData>(
            name,
            TimeTrackerAuditTrailEvents.CategoryName,
            entry.ItemId,
            userId: null,
            userName: actor,
            new TimeEntryAuditData { Entry = entry, Before = before, Reason = reason }));
    }
}
