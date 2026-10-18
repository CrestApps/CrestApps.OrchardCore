using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Reports;

/// <summary>
/// The <c>Messages</c> data set: one row per message of a messaging conversation. Only principals allowed to view
/// every messaging conversation read it. Message bodies are never exposed; their length is.
/// </summary>
public sealed class MessagingMessagesReportDataSet : RecordDataSet<MessagingReportRecord<OmnichannelMessage>>
{
    /// <summary>
    /// The technical name of the creation time field, which reads are narrowed by.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    private static readonly string[] _agentFields = ["SentByAgentUserId", "SentByAgentName"];

    // The index columns a join can narrow the read on.
    private static readonly string[] _keyFields = ["ConversationId"];

    private readonly ISession _session;
    private readonly IAgentProfileStore _agentProfileStore;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingMessagesReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session messages are read from.</param>
    /// <param name="agentProfileStore">The agent profile store, which names the sending agents.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MessagingMessagesReportDataSet(
        ISession session,
        IAgentProfileStore agentProfileStore,
        IAuthorizationService authorizationService,
        IStringLocalizer<MessagingMessagesReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(
            MessagingReportDataSource.MessagesDataSet,
            stringLocalizer["Messages"],
            stringLocalizer["One row per message of a messaging conversation, without its text."])
        {
            DefaultDateField = CreatedUtcField,
        })
    {
        _session = session;
        _agentProfileStore = agentProfileStore;
        _authorizationService = authorizationService;

        var S = stringLocalizer;
        var details = S["Message"].Value;
        var delivery = S["Delivery"].Value;

        AddField("Id", S["Message ID"], DataFieldType.Text, record => record.Document.Id, details).Identifier();
        AddField(
            "ConversationId",
            S["Conversation ID"],
            DataFieldType.Text,
            record => record.Document.ConversationId, details).Identifier(new DataFieldReference(MessagingReportDataSource.SourceName, MessagingReportDataSource.ConversationsDataSet, MessagingReportDataSource.ConversationIdField));
        AddField("Channel", S["Channel"], DataFieldType.Text, record => record.Document.Channel, details);
        AddField("CustomerAddress", S["Customer address"], DataFieldType.Text, record => record.Document.CustomerAddress, details);
        AddField("ServiceAddress", S["Service address"], DataFieldType.Text, record => record.Document.ServiceAddress, details);
        AddField("IsInbound", S["Inbound"], DataFieldType.Boolean, record => record.Document.IsInbound, details);
        AddField(CreatedUtcField, S["Sent"], DataFieldType.DateTime, record => record.Document.CreatedUtc, details);
        AddField("Length", S["Length"], DataFieldType.Integer, record => record.Document.Content?.Length ?? 0, details);
        AddField("MediaCount", S["Attachments"], DataFieldType.Integer, record => record.Document.MediaReferences?.Count ?? 0, details);
        AddField("SentByAgentId", S["Sent by agent ID"], DataFieldType.Text, record => record.Document.SentByAgentId, details).Identifier();
        AddField(
            "SentByAgentUserId",
            S["Sent by agent user ID"],
            DataFieldType.Text,
            record => record.Agent?.UserId, details).Identifier(new DataFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField));
        AddField("SentByAgentName", S["Sent by agent"], DataFieldType.Text, record => record.AgentName, details);
        AddField("DeliveryStatus", S["Delivery status"], DataFieldType.Text, record => record.Document.DeliveryStatus, delivery);
        AddField("ErrorCode", S["Error code"], DataFieldType.Text, record => record.Document.ErrorCode, delivery);

        Fields.MarkKeyFilterable(_keyFields);
    }

    /// <inheritdoc/>
    public override Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        return MessagingReportPermissions.CanReadAsync(_authorizationService, context);
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<MessagingReportRecord<OmnichannelMessage>> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (from, to) = DataConditions.DateRange(query.Conditions, CreatedUtcField);
        string[] conversationIds = null;

        if (DataConditions.Keys(query.Conditions, "ConversationId") is { } keys)
        {
            if (keys.Count == 0)
            {
                yield break;
            }

            conversationIds = keys.Select(key => key?.ToString()).ToArray();
        }

        IQueryIndex<OmnichannelMessageIndex> Filter(IQueryIndex<OmnichannelMessageIndex> messages)
        {
            // Only the messages of messaging conversations: other channel traffic, such as automated activities, is not
            // the workspace's to report.
            messages = messages.Where(index => index.ConversationId != null);

            if (conversationIds is not null)
            {
                messages = messages.Where(index => index.ConversationId.IsIn(conversationIds));
            }

            if (from.HasValue)
            {
                var earliest = from.Value;
                messages = messages.Where(index => index.CreatedUtc >= earliest);
            }

            if (to.HasValue)
            {
                var latest = to.Value;
                messages = messages.Where(index => index.CreatedUtc <= latest);
            }

            return messages;
        }

        await foreach (var documents in ReportIndexPages.ReadPagesAsync<OmnichannelMessage, OmnichannelMessageIndex>(
            _session,
            Filter,
            index => index.CreatedUtc,
            OmnichannelConstants.CollectionName,
            cancellationToken))
        {
            var agents = await MessagingReportAgents.LoadAsync(
                _agentProfileStore,
                query,
                documents.Select(document => document.SentByAgentId),
                _agentFields,
                cancellationToken);

            foreach (var document in documents)
            {
                yield return new MessagingReportRecord<OmnichannelMessage>(document, MessagingReportAgents.Find(agents, document.SentByAgentId));
            }
        }
    }

}

/// <summary>
/// The permission the messaging data sets require.
/// </summary>
internal static class MessagingReportPermissions
{
    /// <summary>
    /// Determines whether the principal of a run may view every messaging conversation.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="context">The context of the run.</param>
    /// <returns><see langword="true"/> when it may.</returns>
    public static async Task<bool> CanReadAsync(IAuthorizationService authorizationService, DataSourceContext context)
    {
        return context?.User is not null &&
            await authorizationService.AuthorizeAsync(context.User, MessagingPermissions.ViewAllConversations);
    }
}
