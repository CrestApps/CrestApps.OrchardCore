using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Reports;

/// <summary>
/// Exposes the conversations of the messaging workspace, and their messages, to reports and data pipelines. Only principals
/// allowed to view every messaging conversation read them. Message bodies are never exposed.
/// </summary>
public sealed class MessagingReportDataSource : RecordDataSource, IDataSource
{
    /// <summary>
    /// The technical name of the data source.
    /// </summary>
    public const string SourceName = "Messaging";

    /// <summary>
    /// The technical name of the conversations data set.
    /// </summary>
    public const string ConversationsDataSet = "Conversations";

    /// <summary>
    /// The technical name of the messages data set.
    /// </summary>
    public const string MessagesDataSet = "Messages";

    /// <summary>
    /// The technical name of the conversation identifier field, which messages reference.
    /// </summary>
    public const string ConversationIdField = "ItemId";

    /// <summary>
    /// The technical name of the AI chat data source, whose sessions conversations handled by AI reference.
    /// </summary>
    public const string AIChatSource = "AIChat";

    /// <summary>
    /// The technical name of the AI chat sessions data set.
    /// </summary>
    public const string AIChatSessionsDataSet = "ChatSessions";

    /// <summary>
    /// The technical name of the AI chat sessions data set's identifier field.
    /// </summary>
    public const string AIChatSessionIdField = "SessionId";

    private readonly IRecordDataSet[] _dataSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingReportDataSource"/> class.
    /// </summary>
    /// <param name="conversations">The conversations data set.</param>
    /// <param name="messages">The messages data set.</param>
    public MessagingReportDataSource(
        MessagingConversationsReportDataSet conversations,
        MessagingMessagesReportDataSet messages)
    {
        _dataSets = [conversations, messages];
    }

    /// <inheritdoc/>
    public static string Name => SourceName;

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<MessagingReportDataSource>("Messaging");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<MessagingReportDataSource>("The conversations of the messaging workspace and their messages.");

    /// <inheritdoc/>
    protected override IEnumerable<IRecordDataSet> DataSets => _dataSets;
}
