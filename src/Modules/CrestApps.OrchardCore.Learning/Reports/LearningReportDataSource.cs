using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning.Reports;

/// <summary>
/// The Learning data source of Data Pipelines and the Report Builder: enrollments, progress on each course item,
/// completions and course staff. Courses and lessons themselves are content, read through the Content items source.
/// </summary>
public sealed class LearningReportDataSource : RecordDataSource, IDataSource
{
    private readonly ILearningReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets of the enabled Learning features.</param>
    public LearningReportDataSource(IEnumerable<ILearningReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <summary>
    /// Gets the stable name of the source.
    /// </summary>
    public static string Name => LearningConstants.DataSourceName;

    /// <summary>
    /// Gets the name shown in the designer.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<LearningReportDataSource>("Learning");

    /// <summary>
    /// Gets the description shown in the designer.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<LearningReportDataSource>("Enrollments, progress on each course item, completions and course staff.");

    /// <inheritdoc />
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
