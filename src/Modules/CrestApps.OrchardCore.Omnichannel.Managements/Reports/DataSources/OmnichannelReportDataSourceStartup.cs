using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports.DataSources;

/// <summary>
/// Offers the Omnichannel records to the report builder as soon as it is enabled along with Omnichannel activities.
/// </summary>
[Feature(OmnichannelConstants.Features.Activities)]
[RequireFeatures(ReportsConstants.BuilderFeature)]
public sealed class OmnichannelReportDataSourceStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IReportDataSource, OmnichannelReportDataSource>();
    }
}
