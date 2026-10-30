using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Scheduling.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Scheduling;

/// <summary>
/// Registers the scheduling workflow events and tasks.
/// </summary>
[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAppointmentObserver, AppointmentWorkflowObserver>();

        services.AddActivity<AppointmentRequestedEvent, AppointmentEventActivityDisplayDriver<AppointmentRequestedEvent>>();
        services.AddActivity<AppointmentConfirmedEvent, AppointmentEventActivityDisplayDriver<AppointmentConfirmedEvent>>();
        services.AddActivity<AppointmentRescheduledEvent, AppointmentEventActivityDisplayDriver<AppointmentRescheduledEvent>>();
        services.AddActivity<AppointmentCancelledEvent, AppointmentEventActivityDisplayDriver<AppointmentCancelledEvent>>();
        services.AddActivity<AppointmentCompletedEvent, AppointmentEventActivityDisplayDriver<AppointmentCompletedEvent>>();
        services.AddActivity<AppointmentNoShowEvent, AppointmentEventActivityDisplayDriver<AppointmentNoShowEvent>>();
        services.AddActivity<AppointmentHoldExpiredEvent, AppointmentEventActivityDisplayDriver<AppointmentHoldExpiredEvent>>();
        services.AddActivity<WaitlistSlotAvailableEvent, AppointmentEventActivityDisplayDriver<WaitlistSlotAvailableEvent>>();

        services.AddActivity<FindAppointmentSlotsTask, AppointmentTaskDisplayDriver<FindAppointmentSlotsTask>>();
        services.AddActivity<BookAppointmentTask, AppointmentTaskDisplayDriver<BookAppointmentTask>>();
        services.AddActivity<ConfirmAppointmentTask, AppointmentTaskDisplayDriver<ConfirmAppointmentTask>>();
        services.AddActivity<RescheduleAppointmentTask, AppointmentTaskDisplayDriver<RescheduleAppointmentTask>>();
        services.AddActivity<CancelAppointmentTask, AppointmentTaskDisplayDriver<CancelAppointmentTask>>();
        services.AddActivity<GetAppointmentTask, AppointmentTaskDisplayDriver<GetAppointmentTask>>();
        services.AddActivity<AddToWaitlistTask, AppointmentTaskDisplayDriver<AddToWaitlistTask>>();
    }
}
