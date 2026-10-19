using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
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
/// The <c>Conversations</c> data set: one row per messaging conversation. Only principals allowed to view every
/// messaging conversation read it. The last message preview, the AI summary, and the history are never exposed.
/// </summary>
public sealed class MessagingConversationsReportDataSet : RecordDataSet<MessagingReportRecord<MessagingConversation>>
{
    /// <summary>
    /// The technical name of the last message field, which reads are narrowed by.
    /// </summary>
    public const string LastMessageUtcField = "LastMessageUtc";

    private static readonly string[] _agentFields = ["AssignedAgentUserId", "AssignedAgentName"];

    // The index columns a join can narrow the read on.
    private static readonly string[] _keyFields = [MessagingReportDataSource.ConversationIdField];

    private readonly ISession _session;
    private readonly IAgentProfileStore _agentProfileStore;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingConversationsReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session conversations are read from.</param>
    /// <param name="agentProfileStore">The agent profile store, which names the assigned agents.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MessagingConversationsReportDataSet(
        ISession session,
        IAgentProfileStore agentProfileStore,
        IAuthorizationService authorizationService,
        IStringLocalizer<MessagingConversationsReportDataSet> stringLocalizer)
        : base(new DataSetDescriptor(
            MessagingReportDataSource.ConversationsDataSet,
            stringLocalizer["Conversations"],
            stringLocalizer["One row per messaging conversation."])
        {
            DefaultDateField = LastMessageUtcField,
        })
    {
        _session = session;
        _agentProfileStore = agentProfileStore;
        _authorizationService = authorizationService;

        var S = stringLocalizer;
        var details = S["Conversation"].Value;
        var assignment = S["Assignment"].Value;
        var activity = S["Activity"].Value;
        var firstResponse = S["First response"].Value;

        AddField(MessagingReportDataSource.ConversationIdField, S["Conversation ID"], DataFieldType.Text, record => record.Document.ItemId, details).Identifier();
        AddField("Channel", S["Channel"], DataFieldType.Text, record => record.Document.Channel, details);
        AddField("ServiceAddress", S["Service address"], DataFieldType.Text, record => record.Document.ServiceAddress, details);
        AddField("ContactAddress", S["Contact address"], DataFieldType.Text, record => record.Document.ContactAddress, details);
        AddField("ContactContentItemId", S["Contact ID"], DataFieldType.Text, record => record.Document.ContactContentItemId, details).Identifier();
        AddField("Status", S["Status"], DataFieldType.Text, record => record.Document.Status, details);
        AddField(
            "AISessionId",
            S["AI session ID"],
            DataFieldType.Text,
            record => record.Document.AISessionId, details).Identifier(new DataFieldReference(MessagingReportDataSource.AIChatSource, MessagingReportDataSource.AIChatSessionsDataSet, MessagingReportDataSource.AIChatSessionIdField));
        AddField("OwnerType", S["Owner type"], DataFieldType.Text, record => record.Document.OwnerType, assignment);
        AddField("OwnerId", S["Owner ID"], DataFieldType.Text, record => record.Document.OwnerId, assignment).Identifier();
        AddField("AssignedAgentId", S["Assigned agent ID"], DataFieldType.Text, record => record.Document.AssignedAgentId, assignment).Identifier();
        AddField(
            "AssignedAgentUserId",
            S["Assigned agent user ID"],
            DataFieldType.Text,
            record => record.Agent?.UserId, assignment).Identifier(new DataFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField));
        AddField("AssignedAgentName", S["Assigned agent"], DataFieldType.Text, record => record.AgentName, assignment);
        AddField("AssignmentStatus", S["Assignment status"], DataFieldType.Text, record => record.Document.AssignmentStatus, assignment);
        AddField("AssignedUtc", S["Assigned"], DataFieldType.DateTime, record => record.Document.AssignedUtc, assignment);
        AddField("ReassignmentAttempts", S["Reassignment attempts"], DataFieldType.Integer, record => record.Document.ReassignmentAttempts, assignment);
        AddField("CreatedUtc", S["Created"], DataFieldType.DateTime, record => record.Document.CreatedUtc, activity);
        AddField("ModifiedUtc", S["Modified"], DataFieldType.DateTime, record => record.Document.ModifiedUtc, activity);
        AddField(LastMessageUtcField, S["Last message"], DataFieldType.DateTime, record => record.Document.LastMessageUtc, activity);
        AddField("IsRead", S["Read"], DataFieldType.Boolean, record => record.Document.IsRead, activity);
        AddField("UnreadCount", S["Unread messages"], DataFieldType.Integer, record => record.Document.UnreadCount, activity);
        AddField("FirstResponseDueUtc", S["First response due"], DataFieldType.DateTime, record => record.Document.FirstResponseDueUtc, firstResponse);
        AddField("FirstRespondedUtc", S["First responded"], DataFieldType.DateTime, record => record.Document.FirstRespondedUtc, firstResponse);
        AddField("FirstResponseBreached", S["First response breached"], DataFieldType.Boolean, record => record.Document.FirstResponseBreached, firstResponse);
        AddField("FirstResponseSeconds", S["First response time (seconds)"], DataFieldType.Decimal, record => FirstResponseSeconds(record.Document), firstResponse);

        Fields.MarkKeyFilterable(_keyFields);
    }

    /// <inheritdoc/>
    public override Task<bool> CanReadAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        return MessagingReportPermissions.CanReadAsync(_authorizationService, context);
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<MessagingReportRecord<MessagingConversation>> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (from, to) = DataConditions.DateRange(query.Conditions, LastMessageUtcField);
        string[] conversationIds = null;

        if (DataConditions.Keys(query.Conditions, MessagingReportDataSource.ConversationIdField) is { } keys)
        {
            if (keys.Count == 0)
            {
                yield break;
            }

            conversationIds = keys.Select(key => key?.ToString()).ToArray();
        }

        IQueryIndex<MessagingConversationIndex> Filter(IQueryIndex<MessagingConversationIndex> conversations)
        {
            if (conversationIds is not null)
            {
                conversations = conversations.Where(index => index.ItemId.IsIn(conversationIds));
            }

            if (from.HasValue)
            {
                var earliest = from.Value;
                conversations = conversations.Where(index => index.LastMessageUtc >= earliest);
            }

            if (to.HasValue)
            {
                var latest = to.Value;
                conversations = conversations.Where(index => index.LastMessageUtc <= latest);
            }

            return conversations;
        }

        await foreach (var documents in ReportIndexPages.ReadPagesAsync<MessagingConversation, MessagingConversationIndex>(
            _session,
            Filter,
            index => index.LastMessageUtc,
            MessagingStorage.CollectionName,
            cancellationToken))
        {
            var agents = await MessagingReportAgents.LoadAsync(
                _agentProfileStore,
                query,
                documents.Select(document => document.AssignedAgentId),
                _agentFields,
                cancellationToken);

            foreach (var document in documents)
            {
                yield return new MessagingReportRecord<MessagingConversation>(document, MessagingReportAgents.Find(agents, document.AssignedAgentId));
            }
        }
    }

    // The time from the conversation's start to the first reply, when it has had one.
    private static decimal? FirstResponseSeconds(MessagingConversation conversation)
    {
        if (conversation.FirstRespondedUtc is not DateTime responded || conversation.CreatedUtc == default)
        {
            return null;
        }

        var seconds = (responded - conversation.CreatedUtc).TotalSeconds;

        return seconds < 0 ? null : Math.Round((decimal)seconds, 3);
    }
}
