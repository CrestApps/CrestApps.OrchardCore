using CrestApps.OrchardCore.Bookings.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Bookings;

/// <summary>
/// Exposes the bookings, the dates of booking products and the balances left to pay as a data source of Orchard Core,
/// for reports and data pipelines.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<BookingReportDataSource>()
            .AddScoped<IBookingReportDataSet, BookingsReportDataSet>()
            .AddScoped<IBookingReportDataSet, BookingSlotsReportDataSet>()
            .AddScoped<IBookingReportDataSet, BookingBalancesReportDataSet>();
    }
}
