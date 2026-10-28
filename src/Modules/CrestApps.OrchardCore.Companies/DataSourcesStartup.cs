using CrestApps.OrchardCore.Companies.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Companies;

/// <summary>
/// Exposes the companies as a data source of Orchard Core, for reports and data pipelines: the data source, and the
/// data sets of the companies, their buyers, the price lists and their prices.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<CompanyReportDataSource>()
            .AddScoped<ICompanyReportDataSet, CompaniesReportDataSet>()
            .AddScoped<ICompanyReportDataSet, CompanyBuyersReportDataSet>()
            .AddScoped<ICompanyReportDataSet, PriceListsReportDataSet>()
            .AddScoped<ICompanyReportDataSet, PriceListEntriesReportDataSet>();
    }
}
