using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.TimeTracker;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.TimeTracker;

/// <summary>
/// With Time Tracker on, a desk records its agents' work on tickets in a stage, and each ticket shows its time.
/// </summary>
[RequireFeatures(TimeTrackerConstants.Features.TimeTracker)]
public sealed class TimeTrackerStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<TicketTime>();
        services.AddDisplayDriver<TicketDesk, TicketDeskTimeDisplayDriver>();
        services.AddContentPart<TicketPart>()
            .UseDisplayDriver<TicketTimeDisplayDriver>();
    }
}
