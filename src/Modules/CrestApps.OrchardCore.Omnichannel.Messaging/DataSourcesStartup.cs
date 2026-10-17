using CrestApps.OrchardCore.Omnichannel.Messaging.Reports;
using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging;

/// <summary>
/// Exposes the messaging workspace's conversations and their messages as a data source of Orchard Core, for reports and
/// data pipelines.
/// </summary>
[Feature(MessagingConstants.Feature.Workspace)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<MessagingConversationsReportDataSet>()
            .AddScoped<MessagingMessagesReportDataSet>()
            .AddDataSource<MessagingReportDataSource>();
    }
}
