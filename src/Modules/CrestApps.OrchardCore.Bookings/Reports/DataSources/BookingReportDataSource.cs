using CrestApps.OrchardCore.Reports.DataSources;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Bookings.Reports.DataSources;

/// <summary>
/// Exposes the store's bookings, the dates of its booking products, and the balances left to pay on bookings paid by
/// deposit as a data source of Orchard Core, for reports and data pipelines. The data sets are registered as
/// <see cref="IBookingReportDataSet"/>s, and each data set checks that the principal may read it.
/// </summary>
public sealed class BookingReportDataSource : RecordDataSource, IDataSource
{
    private readonly IBookingReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets the enabled bookings features register.</param>
    public BookingReportDataSource(IEnumerable<IBookingReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => CommerceReportDataSets.BookingSource.Name;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<BookingReportDataSource>("Bookings");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<BookingReportDataSource>("The store's bookings, the dates of its booking products with what is held, sold and left on each, and the balances left to pay on bookings paid by deposit.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
