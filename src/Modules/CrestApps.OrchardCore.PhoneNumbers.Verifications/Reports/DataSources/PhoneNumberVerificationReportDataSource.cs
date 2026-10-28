using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Reports.DataSources;

/// <summary>
/// Exposes the phone number verification records as a data source of Orchard Core, for reports and data pipelines.
/// The data set checks that the principal may run the phone number verifications report.
/// </summary>
public sealed class PhoneNumberVerificationReportDataSource : RecordDataSource, IDataSource
{
    private readonly IRecordDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneNumberVerificationReportDataSource"/> class.
    /// </summary>
    /// <param name="verifications">The verifications data set.</param>
    public PhoneNumberVerificationReportDataSource(PhoneNumberVerificationsReportDataSet verifications)
    {
        _dataSets = [verifications];
    }

    /// <inheritdoc/>
    public static string Name => PhoneNumberVerificationReportDataSets.Source;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<PhoneNumberVerificationReportDataSource>("Phone number verifications");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<PhoneNumberVerificationReportDataSource>("The phone numbers stored for verification, their verification status, provider and attempts, and the line details the provider reported.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
