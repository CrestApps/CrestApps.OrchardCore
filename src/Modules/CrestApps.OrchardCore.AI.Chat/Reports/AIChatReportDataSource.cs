using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// Exposes AI chat sessions, and their analytics when the AI Chat Session Analytics feature is enabled, to the report
/// builder. Its data sets are the <see cref="IAIChatReportDataSet"/> services of the enabled features.
/// </summary>
public sealed class AIChatReportDataSource : ReportRecordDataSource
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
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatReportDataSource"/> class.
    /// </summary>
    /// <param name="dataSets">The data sets of the enabled features.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIChatReportDataSource(
        IEnumerable<IAIChatReportDataSet> dataSets,
        IStringLocalizer<AIChatReportDataSource> stringLocalizer)
    {
        _dataSets = dataSets.ToArray();
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Name => SourceName;

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["AI chat"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["AI chat sessions and their analytics."];

    /// <inheritdoc/>
    protected override IEnumerable<IReportRecordDataSet> DataSets => _dataSets;
}
