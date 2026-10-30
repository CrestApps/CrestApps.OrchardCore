using CrestApps.Core.Services;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Calendar;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

/// <summary>
/// With calendars on, an agent can put a follow-up activity on their calendar while completing an activity, and the activity
/// and its calendar event stay at the same time whichever one is moved.
/// </summary>
[Feature(OmnichannelConstants.Features.Activities)]
[RequireFeatures(CalendarsConstants.Features.Calendars)]
public sealed class CalendarStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ActivityCalendarService>();
        services.AddScoped<IActivityDispositionHandler, ActivityCalendarDispositionHandler>();
        services.AddScoped<ICatalogEntryHandler<OmnichannelActivity>, ActivityCalendarEntryHandler>();
        services.AddScoped<ICalendarEventHandler, ActivityCalendarEventHandler>();
        services.TryAddScoped(sp => new Lazy<IOmnichannelActivityManager>(sp.GetRequiredService<IOmnichannelActivityManager>));
        services.AddDisplayDriver<OmnichannelActivity, OmnichannelActivityCalendarDisplayDriver>();
    }
}
