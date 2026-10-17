using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the dialer activity and health report when both the Contact Center Paced Dialing and the Reports features are
/// enabled. The report reads the pacing cycle records only Paced Dialing writes, so it is registered with that feature
/// rather than with the reports every Work Distribution tenant gets.
/// </summary>
[Feature(ContactCenterConstants.Feature.DialerPaced)]
[RequireFeatures(ReportsConstants.Feature)]
public sealed class DialerPacedReportsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddReport<DialerActivityReportProvider>();
    }
}
