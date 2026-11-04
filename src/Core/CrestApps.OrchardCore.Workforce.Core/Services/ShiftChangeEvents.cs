using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Raises the shift events for every change to a published schedule: a shift assigned, changed or removed.
/// </summary>
internal sealed class ShiftChangeEvents : IScheduleChangeHandler
{
    private readonly IWorkforceEvents _events;

    public ShiftChangeEvents(IWorkforceEvents events)
    {
        _events = events;
    }

    public Task RecordedAsync(ScheduleChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        var name = change.Kind switch
        {
            ScheduleChangeKind.Added or ScheduleChangeKind.OpenShiftFilled => WorkforceEvents.ShiftAssigned,
            ScheduleChangeKind.Removed => WorkforceEvents.ShiftRemoved,
            _ => WorkforceEvents.ShiftChanged,
        };

        return _events.RaiseAsync(name, WorkforceEventInputs.ShiftChange(change), change.ItemId, cancellationToken);
    }
}
