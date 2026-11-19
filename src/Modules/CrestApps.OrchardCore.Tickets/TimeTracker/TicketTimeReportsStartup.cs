using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Tickets.Reports;
using CrestApps.OrchardCore.Tickets.Reports.DataSources;
using CrestApps.OrchardCore.TimeTracker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.TimeTracker;

/// <summary>
/// With Reports and Time Tracker on, the ticket time reports under Reports: the time logged on tickets, and the
/// resolution time of solved tickets against the time worked on them.
/// </summary>
[RequireFeatures(ReportsConstants.Feature, TimeTrackerConstants.Features.TimeTracker)]
public sealed class TicketTimeReportsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<TicketReportAccess>();
        services.TryAddScoped<TicketReportData>();
        services.TryAddScoped<TicketTimeReportData>();
        services
            .AddReport<TicketTimeReport>()
            .AddReport<TicketResolutionWorkReport>();
    }
}
