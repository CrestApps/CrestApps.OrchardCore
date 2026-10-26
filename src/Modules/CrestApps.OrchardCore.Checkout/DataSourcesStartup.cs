using CrestApps.OrchardCore.Checkout.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Exposes the checkout as a data source of Orchard Core, for reports and data pipelines: the data source, and the data
/// sets of the checkouts buyers started, the coupons, the uses of their codes, and the payment methods customers kept.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<CheckoutReportDataSource>()
            .AddScoped<ICheckoutReportDataSet, CheckoutSessionsReportDataSet>()
            .AddScoped<ICheckoutReportDataSet, CouponsReportDataSet>()
            .AddScoped<ICheckoutReportDataSet, CouponRedemptionsReportDataSet>()
            .AddScoped<ICheckoutReportDataSet, SavedPaymentMethodsReportDataSet>();
    }
}
