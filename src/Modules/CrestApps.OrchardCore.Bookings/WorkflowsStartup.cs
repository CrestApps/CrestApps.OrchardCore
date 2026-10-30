using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Bookings.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Bookings;

/// <summary>
/// Starts workflows as bookings are made, confirmed, moved, canceled, completed, missed and expire.
/// </summary>
[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<BookingCreatedEvent, BookingEventDisplayDriver<BookingCreatedEvent>>();
        services.AddActivity<BookingConfirmedEvent, BookingEventDisplayDriver<BookingConfirmedEvent>>();
        services.AddActivity<BookingRescheduledEvent, BookingEventDisplayDriver<BookingRescheduledEvent>>();
        services.AddActivity<BookingCanceledEvent, BookingEventDisplayDriver<BookingCanceledEvent>>();
        services.AddActivity<BookingCompletedEvent, BookingEventDisplayDriver<BookingCompletedEvent>>();
        services.AddActivity<BookingNoShowEvent, BookingEventDisplayDriver<BookingNoShowEvent>>();
        services.AddActivity<BookingExpiredEvent, BookingEventDisplayDriver<BookingExpiredEvent>>();
        services.AddScoped<IBookingHandler, BookingWorkflowHandler>();
    }
}
