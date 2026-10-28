using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// Exposes the subscriptions as a data source of Orchard Core, for reports and data pipelines: the data source, and the
/// data sets of the subscriptions and the lines they bill. The installment plans feature registers the data sets of
/// its own records, so a data set is listed only while its feature is on.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<SubscriptionReportDataSource>()
            .AddScoped<ISubscriptionReportDataSet, SubscriptionsReportDataSet>()
            .AddScoped<ISubscriptionReportDataSet, SubscriptionLinesReportDataSet>();
    }
}

/// <summary>
/// Registers the data sets of the installment plans feature. The feature can be on without the Subscriptions feature,
/// so it registers the data source too; a data source registered twice is listed once.
/// </summary>
[Feature(SubscriptionConstants.Features.Installments)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class InstallmentPlansDataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<SubscriptionReportDataSource>()
            .AddScoped<ISubscriptionReportDataSet, InstallmentPlansReportDataSet>()
            .AddScoped<ISubscriptionReportDataSet, InstallmentsReportDataSet>();
    }
}
