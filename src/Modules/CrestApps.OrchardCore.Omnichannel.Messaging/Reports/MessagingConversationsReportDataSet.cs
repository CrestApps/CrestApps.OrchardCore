using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Reports;

/// <summary>
/// The <c>Conversations</c> data set: one row per messaging conversation. Only principals allowed to view every
/// messaging conversation read it. The last message preview, the AI summary, and the history are never exposed.
/// </summary>
public sealed class MessagingConversationsReportDataSet : ReportRecordDataSet<MessagingReportRecord<MessagingConversation>>
{
    /// <summary>
    /// The technical name of the last message field, which reads are narrowed by.
    /// </summary>
    public const string LastMessageUtcField = "LastMessageUtc";

    private static readonly string[] _agentFields = ["AssignedAgentUserId", "AssignedAgentName"];

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
        : base(new ReportDataSetDescriptor(
            MessagingReportDataSource.ConversationsDataSet,
            stringLocalizer["Conversations"],
            stringLocalizer["One row per messaging conversation."]))
    {
        _session = session;
        _agentProfileStore = agentProfileStore;
        _authorizationService = authorizationService;

        var S = stringLocalizer;
        var details = S["Conversation"].Value;
        var assignment = S["Assignment"].Value;
        var activity = S["Activity"].Value;
        var firstResponse = S["First response"].Value;

        AddField(MessagingReportDataSource.ConversationIdField, S["Conversation ID"], ReportDataType.Text, record => record.Document.ItemId, details, isIdentifier: true);
        AddField("Channel", S["Channel"], ReportDataType.Text, record => record.Document.Channel, details);
        AddField("ServiceAddress", S["Service address"], ReportDataType.Text, record => record.Document.ServiceAddress, details);
        AddField("ContactAddress", S["Contact address"], ReportDataType.Text, record => record.Document.ContactAddress, details);
        AddField("ContactContentItemId", S["Contact ID"], ReportDataType.Text, record => record.Document.ContactContentItemId, details, isIdentifier: true);
        AddField("Status", S["Status"], ReportDataType.Text, record => record.Document.Status, details);
        AddField(
            "AISessionId",
            S["AI session ID"],
            ReportDataType.Text,
            record => record.Document.AISessionId,
            details,
            isIdentifier: true,
            new ReportFieldReference(MessagingReportDataSource.AIChatSource, MessagingReportDataSource.AIChatSessionsDataSet, MessagingReportDataSource.AIChatSessionIdField));
        AddField("OwnerType", S["Owner type"], ReportDataType.Text, record => record.Document.OwnerType, assignment);
        AddField("OwnerId", S["Owner ID"], ReportDataType.Text, record => record.Document.OwnerId, assignment, isIdentifier: true);
        AddField("AssignedAgentId", S["Assigned agent ID"], ReportDataType.Text, record => record.Document.AssignedAgentId, assignment, isIdentifier: true);
        AddField(
            "AssignedAgentUserId",
            S["Assigned agent user ID"],
            ReportDataType.Text,
            record => record.Agent?.UserId,
            assignment,
            isIdentifier: true,
            new ReportFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField));
        AddField("AssignedAgentName", S["Assigned agent"], ReportDataType.Text, record => record.AgentName, assignment);
        AddField("AssignmentStatus", S["Assignment status"], ReportDataType.Text, record => record.Document.AssignmentStatus, assignment);
        AddField("AssignedUtc", S["Assigned"], ReportDataType.DateTime, record => record.Document.AssignedUtc, assignment);
        AddField("ReassignmentAttempts", S["Reassignment attempts"], ReportDataType.Integer, record => record.Document.ReassignmentAttempts, assignment);
        AddField("CreatedUtc", S["Created"], ReportDataType.DateTime, record => record.Document.CreatedUtc, activity);
        AddField("ModifiedUtc", S["Modified"], ReportDataType.DateTime, record => record.Document.ModifiedUtc, activity);
        AddField(LastMessageUtcField, S["Last message"], ReportDataType.DateTime, record => record.Document.LastMessageUtc, activity);
        AddField("IsRead", S["Read"], ReportDataType.Boolean, record => record.Document.IsRead, activity);
        AddField("UnreadCount", S["Unread messages"], ReportDataType.Integer, record => record.Document.UnreadCount, activity);
        AddField("FirstResponseDueUtc", S["First response due"], ReportDataType.DateTime, record => record.Document.FirstResponseDueUtc, firstResponse);
        AddField("FirstRespondedUtc", S["First responded"], ReportDataType.DateTime, record => record.Document.FirstRespondedUtc, firstResponse);
        AddField("FirstResponseBreached", S["First response breached"], ReportDataType.Boolean, record => record.Document.FirstResponseBreached, firstResponse);
        AddField("FirstResponseSeconds", S["First response time (seconds)"], ReportDataType.Decimal, record => FirstResponseSeconds(record.Document), firstResponse);
    }

    /// <inheritdoc/>
    public override Task<bool> CanReadAsync(ReportDataSourceContext context)
    {
        return MessagingReportPermissions.CanReadAsync(_authorizationService, context);
    }

    /// <inheritdoc/>
    protected override async Task<IEnumerable<MessagingReportRecord<MessagingConversation>>> LoadAsync(ReportDataSourceQuery query, int take, CancellationToken cancellationToken)
    {
        var (from, to) = ReportDateRange.For(query.Conditions, LastMessageUtcField);
        var conversations = _session.Query<MessagingConversation, MessagingConversationIndex>(collection: MessagingStorage.CollectionName);

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

        var documents = (await conversations
            .OrderByDescending(index => index.LastMessageUtc)
            .ThenByDescending(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken))
            .ToArray();
        var agents = await MessagingReportAgents.LoadAsync(
            _agentProfileStore,
            query,
            documents.Select(document => document.AssignedAgentId),
            _agentFields,
            cancellationToken);

        return documents
            .Select(document => new MessagingReportRecord<MessagingConversation>(document, MessagingReportAgents.Find(agents, document.AssignedAgentId)))
            .ToArray();
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
