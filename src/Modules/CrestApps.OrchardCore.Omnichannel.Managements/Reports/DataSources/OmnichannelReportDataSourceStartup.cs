using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports.DataSources;

/// <summary>
/// Exposes the Omnichannel records as a data source of Orchard Core, for reports and data pipelines.
/// </summary>
[Feature(OmnichannelConstants.Features.Activities)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class OmnichannelReportDataSourceStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataSource<OmnichannelReportDataSource>();
    }
}
