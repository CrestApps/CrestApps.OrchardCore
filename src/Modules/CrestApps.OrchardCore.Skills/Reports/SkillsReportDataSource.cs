using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Skills.Reports;

/// <summary>
/// The Skills data source: certifications, the certificates people hold and the skills assigned to them, for people
/// who may view skills. No data set carries a certificate's verification code.
/// </summary>
public sealed class SkillsReportDataSource : RecordDataSource, IDataSource
{
    private readonly ISkillsReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillsReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets.</param>
    public SkillsReportDataSource(IEnumerable<ISkillsReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <summary>
    /// Gets the name of the data source.
    /// </summary>
    public static string Name => "Skills";

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SkillsReportDataSource>("Skills");

    /// <summary>
    /// Gets the description.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<SkillsReportDataSource>("Certifications, the certificates people hold with their dates, status and renewals, and the skills assigned to people.");

    /// <inheritdoc />
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
