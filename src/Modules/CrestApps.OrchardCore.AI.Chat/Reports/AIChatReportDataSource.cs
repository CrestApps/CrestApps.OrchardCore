using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// Exposes AI chat sessions to the report builder and, when the AI Chat Session Analytics feature is enabled, their
/// analytics and the AI usage that feature records: every metered AI request and every AI voice session summary. Its
/// data sets are the <see cref="IAIChatReportDataSet"/> services of the enabled features.
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
    /// The technical name of the completion usage data set: one row per request sent to an AI provider.
    /// </summary>
    public const string CompletionUsageDataSet = "CompletionUsage";

    /// <summary>
    /// The technical name of the voice usage data set: one row per AI voice session summary.
    /// </summary>
    public const string VoiceUsageDataSet = "VoiceUsage";

    /// <summary>
    /// The technical name of the conversation evaluations data set.
    /// </summary>
    public const string ConversationEvaluationsDataSet = "ConversationEvaluations";

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
    public static LocalizationSource Description { get; } = LocalizationSource.Create<AIChatReportDataSource>("AI chat sessions, their analytics, and AI usage: every request sent to an AI provider and every AI voice call.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
