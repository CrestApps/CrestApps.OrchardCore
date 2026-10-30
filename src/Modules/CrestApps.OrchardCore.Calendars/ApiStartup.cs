using CrestApps.OrchardCore.Calendars.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Maps the calendars HTTP API.
/// </summary>
[Feature(CalendarsConstants.Features.Api)]
public sealed class ApiStartup : StartupBase
{
    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapCalendarApi();
    }
}
