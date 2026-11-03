using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// With calendars on, shows the open callbacks reserved for an agent on the calendar, read-only, from the callbacks
/// the dialer feature stores.
/// </summary>
[Feature(ContactCenterConstants.Feature.Dialer)]
[RequireFeatures(CalendarsConstants.Features.Calendars)]
public sealed class DialerCalendarStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCalendarEventSource<CallbackCalendarEventSource>();
    }
}
