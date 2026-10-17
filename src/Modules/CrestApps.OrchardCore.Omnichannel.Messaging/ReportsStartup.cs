using CrestApps.OrchardCore.Omnichannel.Messaging.Reports;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging;

/// <summary>
/// Exposes the messaging workspace's conversations and their messages to the report builder when it is enabled.
/// </summary>
[Feature(MessagingConstants.Feature.Workspace)]
[RequireFeatures(ReportsConstants.BuilderFeature)]
public sealed class ReportsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<MessagingConversationsReportDataSet>()
            .AddScoped<MessagingMessagesReportDataSet>()
            .AddScoped<IReportDataSource, MessagingReportDataSource>();
    }
}
