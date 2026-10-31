using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Bookings.Workflows;

/// <summary>
/// The booking events have no settings, so they only render their thumbnail and design shapes.
/// </summary>
/// <typeparam name="TActivity">The event type.</typeparam>
public sealed class BookingEventDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity>
    where TActivity : BookingEvent
{
}
