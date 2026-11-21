using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning.Assessments.Reports;

/// <summary>
/// The Learning assessments data source: attempts, their answers, score changes and quiz overrides, for people who may
/// view Learning reports. No data set carries a question's answer key, its feedback or its grader guidance.
/// </summary>
public sealed class AssessmentsReportDataSource : RecordDataSource, IDataSource
{
    private readonly IAssessmentsReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssessmentsReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets.</param>
    public AssessmentsReportDataSource(IEnumerable<IAssessmentsReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <summary>
    /// Gets the name of the data source.
    /// </summary>
    public static string Name => AssessmentsConstants.DataSourceName;

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<AssessmentsReportDataSource>("Learning assessments");

    /// <summary>
    /// Gets the description.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<AssessmentsReportDataSource>("Quiz attempts, the answers and scores of each question, score changes and quiz overrides. Answer keys are never included.");

    /// <inheritdoc />
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
