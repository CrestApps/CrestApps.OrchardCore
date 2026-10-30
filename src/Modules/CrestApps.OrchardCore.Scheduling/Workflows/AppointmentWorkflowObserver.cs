using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// Starts the appointment workflow events once a change is committed.
/// </summary>
internal sealed class AppointmentWorkflowObserver : IAppointmentObserver
{
    private readonly IWorkflowManager _workflowManager;

    public AppointmentWorkflowObserver(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    public Task OnChangedAsync(AppointmentChange change)
    {
        var name = change.Kind switch
        {
            AppointmentChangeKind.Requested => nameof(AppointmentRequestedEvent),
            AppointmentChangeKind.Confirmed => nameof(AppointmentConfirmedEvent),
            AppointmentChangeKind.Rescheduled => nameof(AppointmentRescheduledEvent),
            AppointmentChangeKind.Cancelled => nameof(AppointmentCancelledEvent),
            AppointmentChangeKind.Completed => nameof(AppointmentCompletedEvent),
            AppointmentChangeKind.NoShow => nameof(AppointmentNoShowEvent),
            AppointmentChangeKind.HoldExpired => nameof(AppointmentHoldExpiredEvent),
            AppointmentChangeKind.WaitlistSlotAvailable => nameof(WaitlistSlotAvailableEvent),
            _ => null,
        };

        if (name is null)
        {
            return Task.CompletedTask;
        }

        Dictionary<string, object> input;

        if (change.Kind == AppointmentChangeKind.WaitlistSlotAvailable)
        {
            input = new Dictionary<string, object>
            {
                ["AppointmentTypeId"] = change.AppointmentType?.ItemId,
                ["AppointmentTypeName"] = change.AppointmentType?.Name,
                ["WaitlistEntryId"] = change.WaitlistEntry?.ItemId,
                ["CustomerName"] = change.WaitlistEntry?.CustomerName,
                ["CustomerEmail"] = change.WaitlistEntry?.CustomerEmail,
                ["CustomerPhone"] = change.WaitlistEntry?.CustomerPhone,
                ["StartUtc"] = change.PreviousStartUtc.HasValue ? AppointmentWorkflowInput.Format(change.PreviousStartUtc.Value) : null,
            };
        }
        else
        {
            input = AppointmentWorkflowInput.ToInput(change.Appointment, change.AppointmentType);
            input["Actor"] = change.Actor;
            input["PreviousStartUtc"] = change.PreviousStartUtc.HasValue ? AppointmentWorkflowInput.Format(change.PreviousStartUtc.Value) : null;
        }

        return _workflowManager.TriggerEventAsync(name, input, change.Appointment?.ItemId ?? change.WaitlistEntry?.ItemId);
    }
}
