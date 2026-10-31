using CrestApps.OrchardCore.Calendars.Invitations;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers invitation emails and the answer links.
/// </summary>
[Feature(CalendarsConstants.Features.Invitations)]
public sealed class InvitationsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICalendarEventHandler, CalendarInvitationTokenHandler>();
        services.AddScoped<ICalendarEventObserver, CalendarInvitationObserver>();
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapCalendarResponseEndpoint();
    }
}
