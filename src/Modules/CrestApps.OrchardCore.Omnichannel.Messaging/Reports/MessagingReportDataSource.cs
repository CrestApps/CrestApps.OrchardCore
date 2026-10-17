using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Reports;

/// <summary>
/// Exposes the conversations of the messaging workspace, and their messages, to the report builder. Only principals
/// allowed to view every messaging conversation read them. Message bodies are never exposed.
/// </summary>
public sealed class MessagingReportDataSource : ReportRecordDataSource
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

    private readonly IReportRecordDataSet[] _dataSets;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingReportDataSource"/> class.
    /// </summary>
    /// <param name="conversations">The conversations data set.</param>
    /// <param name="messages">The messages data set.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MessagingReportDataSource(
        MessagingConversationsReportDataSet conversations,
        MessagingMessagesReportDataSet messages,
        IStringLocalizer<MessagingReportDataSource> stringLocalizer)
    {
        _dataSets = [conversations, messages];
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Name => SourceName;

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Messaging"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["The conversations of the messaging workspace and their messages."];

    /// <inheritdoc/>
    protected override IEnumerable<IReportRecordDataSet> DataSets => _dataSets;
}
