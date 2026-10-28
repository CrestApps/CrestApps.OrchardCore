using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders;

/// <summary>
/// Exposes the orders as a data source of Orchard Core, for reports and data pipelines: the data source, and the data
/// sets of the orders, their lines, refunds, disputes, discounts and taxes. The returns and gift cards features register
/// the data sets of their own records, so a data set is listed only while its feature is on.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<OrderReportDataSource>()
            .AddScoped<IOrderReportDataSet, OrdersReportDataSet>()
            .AddScoped<IOrderReportDataSet, OrderLinesReportDataSet>()
            .AddScoped<IOrderReportDataSet, OrderRefundsReportDataSet>()
            .AddScoped<IOrderReportDataSet, OrderDisputesReportDataSet>()
            .AddScoped<IOrderReportDataSet, OrderDiscountsReportDataSet>()
            .AddScoped<IOrderReportDataSet, OrderTaxesReportDataSet>();
    }
}

/// <summary>
/// Registers the data set of the returns feature.
/// </summary>
[Feature(OrderConstants.Features.Returns)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class ReturnsDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IOrderReportDataSet, ReturnsReportDataSet>();
    }
}

/// <summary>
/// Registers the data set of the gift cards feature.
/// </summary>
[Feature(OrderConstants.Features.GiftCards)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class GiftCardsDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IOrderReportDataSet, GiftCardsReportDataSet>();
    }
}
