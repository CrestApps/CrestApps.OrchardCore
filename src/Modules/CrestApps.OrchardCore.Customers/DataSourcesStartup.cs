using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Customers;

/// <summary>
/// Exposes the customers as a data source of Orchard Core, for reports and data pipelines: the data source, and the
/// data set of the customers. The store credit, loyalty points and referrals features register the data sets of their
/// own records, so a data set is listed only while its feature is on.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<CustomerReportDataSource>()
            .AddScoped<ICustomerReportDataSet, CustomersReportDataSet>();
    }
}

/// <summary>
/// Registers the data sets of the store credit feature.
/// </summary>
[Feature(CustomersConstants.Features.StoreCredit)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class StoreCreditDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<ICustomerReportDataSet, StoreCreditAccountsReportDataSet>()
            .AddScoped<ICustomerReportDataSet, StoreCreditActivityReportDataSet>();
    }
}

/// <summary>
/// Registers the data sets of the loyalty points feature.
/// </summary>
[Feature(CustomersConstants.Features.Loyalty)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class LoyaltyDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<ICustomerReportDataSet, LoyaltyAccountsReportDataSet>()
            .AddScoped<ICustomerReportDataSet, LoyaltyActivityReportDataSet>();
    }
}

/// <summary>
/// Registers the data sets of the referrals feature.
/// </summary>
[Feature(CustomersConstants.Features.Referrals)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class ReferralsDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<ICustomerReportDataSet, ReferralsReportDataSet>()
            .AddScoped<ICustomerReportDataSet, ReferralRewardsReportDataSet>();
    }
}
