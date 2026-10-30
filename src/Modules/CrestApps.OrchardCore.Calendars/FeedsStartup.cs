using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Drivers;
using CrestApps.OrchardCore.Calendars.Endpoints;
using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers the private iCalendar feeds and .ics import and export.
/// </summary>
[Feature(CalendarsConstants.Features.Feeds)]
public sealed class FeedsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICalendarImporter>();
        services.AddDisplayDriver<Calendar, CalendarFeedsDisplayDriver>();
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapCalendarFeedEndpoint();
    }
}
