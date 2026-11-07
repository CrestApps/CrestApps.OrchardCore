using CrestApps.OrchardCore.Accounts.B2B.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Accounts.B2B;

/// <summary>
/// Exposes the accounts as a data source of Orchard Core, for reports and data pipelines: the data source, and the
/// data sets of the accounts, their buyers, the price lists and their prices.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<B2BAccountReportDataSource>()
            .AddScoped<IB2BAccountReportDataSet, B2BAccountsReportDataSet>()
            .AddScoped<IB2BAccountReportDataSet, B2BBuyersReportDataSet>()
            .AddScoped<IB2BAccountReportDataSet, PriceListsReportDataSet>()
            .AddScoped<IB2BAccountReportDataSet, PriceListEntriesReportDataSet>();
    }
}
