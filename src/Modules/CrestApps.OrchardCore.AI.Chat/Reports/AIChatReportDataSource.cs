using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// Exposes AI chat sessions, and their analytics when the AI Chat Session Analytics feature is enabled, to the report
/// builder. Its data sets are the <see cref="IAIChatReportDataSet"/> services of the enabled features.
/// </summary>
public sealed class AIChatReportDataSource : RecordDataSource, IDataSource
{
    /// <summary>
    /// The technical name of the data source.
    /// </summary>
    public const string SourceName = "AIChat";

    /// <summary>
    /// The technical name of the chat sessions data set.
    /// </summary>
    public const string ChatSessionsDataSet = "ChatSessions";

    /// <summary>
    /// The technical name of the chat session metrics data set.
    /// </summary>
    public const string ChatSessionMetricsDataSet = "ChatSessionMetrics";

    /// <summary>
    /// The technical name of the session identifier field, which other data sets reference.
    /// </summary>
    public const string SessionIdField = "SessionId";

    private readonly IAIChatReportDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets of the enabled features.</param>
    public AIChatReportDataSource(IEnumerable<IAIChatReportDataSet> dataSets)
    {
        _dataSets = dataSets.ToArray();
    }

    /// <inheritdoc/>
    public static string Name => SourceName;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<AIChatReportDataSource>("AI chat");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<AIChatReportDataSource>("AI chat sessions and their analytics.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
