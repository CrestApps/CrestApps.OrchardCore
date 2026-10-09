using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Reports;

/// <summary>
/// The <c>Messages</c> data set: one row per message of a messaging conversation. Only principals allowed to view
/// every messaging conversation read it. Message bodies are never exposed; their length is.
/// </summary>
public sealed class MessagingMessagesReportDataSet : ReportRecordDataSet<MessagingReportRecord<OmnichannelMessage>>
{
    /// <summary>
    /// The technical name of the creation time field, which reads are narrowed by.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    private static readonly string[] _agentFields = ["SentByAgentUserId", "SentByAgentName"];

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
        : base(new ReportDataSetDescriptor(
            MessagingReportDataSource.MessagesDataSet,
            stringLocalizer["Messages"],
            stringLocalizer["One row per message of a messaging conversation, without its text."]))
    {
        _session = session;
        _agentProfileStore = agentProfileStore;
        _authorizationService = authorizationService;

        var S = stringLocalizer;
        var details = S["Message"].Value;
        var delivery = S["Delivery"].Value;

        AddField("Id", S["Message ID"], ReportDataType.Text, record => record.Document.Id, details, isIdentifier: true);
        AddField(
            "ConversationId",
            S["Conversation ID"],
            ReportDataType.Text,
            record => record.Document.ConversationId,
            details,
            isIdentifier: true,
            new ReportFieldReference(MessagingReportDataSource.SourceName, MessagingReportDataSource.ConversationsDataSet, MessagingReportDataSource.ConversationIdField));
        AddField("Channel", S["Channel"], ReportDataType.Text, record => record.Document.Channel, details);
        AddField("CustomerAddress", S["Customer address"], ReportDataType.Text, record => record.Document.CustomerAddress, details);
        AddField("ServiceAddress", S["Service address"], ReportDataType.Text, record => record.Document.ServiceAddress, details);
        AddField("IsInbound", S["Inbound"], ReportDataType.Boolean, record => record.Document.IsInbound, details);
        AddField(CreatedUtcField, S["Sent"], ReportDataType.DateTime, record => record.Document.CreatedUtc, details);
        AddField("Length", S["Length"], ReportDataType.Integer, record => record.Document.Content?.Length ?? 0, details);
        AddField("MediaCount", S["Attachments"], ReportDataType.Integer, record => record.Document.MediaReferences?.Count ?? 0, details);
        AddField("SentByAgentId", S["Sent by agent ID"], ReportDataType.Text, record => record.Document.SentByAgentId, details, isIdentifier: true);
        AddField(
            "SentByAgentUserId",
            S["Sent by agent user ID"],
            ReportDataType.Text,
            record => record.Agent?.UserId,
            details,
            isIdentifier: true,
            new ReportFieldReference(ReportsConstants.UsersDataSource, ReportsConstants.UsersDataSet, ReportsConstants.UserIdField));
        AddField("SentByAgentName", S["Sent by agent"], ReportDataType.Text, record => record.AgentName, details);
        AddField("DeliveryStatus", S["Delivery status"], ReportDataType.Text, record => record.Document.DeliveryStatus, delivery);
        AddField("ErrorCode", S["Error code"], ReportDataType.Text, record => record.Document.ErrorCode, delivery);
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    public override string DefaultDateField => CreatedUtcField;

    public override Task<bool> CanReadAsync(ReportDataSourceContext context)
    {
        return MessagingReportPermissions.CanReadAsync(_authorizationService, context);
    }

    /// <inheritdoc/>
    protected override async Task<IEnumerable<MessagingReportRecord<OmnichannelMessage>>> LoadAsync(ReportDataSourceQuery query, int take, CancellationToken cancellationToken)
    {
        var (from, to) = ReportDateRange.For(query.Conditions, CreatedUtcField);

        // Only the messages of messaging conversations: other channel traffic, such as automated activities, is not
        // the workspace's to report.
        var messages = _session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
            index => index.ConversationId != null,
            collection: OmnichannelConstants.CollectionName);

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

        var documents = (await messages
            .OrderByDescending(index => index.CreatedUtc)
            .ThenByDescending(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken))
            .ToArray();
        var agents = await MessagingReportAgents.LoadAsync(
            _agentProfileStore,
            query,
            documents.Select(document => document.SentByAgentId),
            _agentFields,
            cancellationToken);

        return documents
            .Select(document => new MessagingReportRecord<OmnichannelMessage>(document, MessagingReportAgents.Find(agents, document.SentByAgentId)))
            .ToArray();
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
    public static async Task<bool> CanReadAsync(IAuthorizationService authorizationService, ReportDataSourceContext context)
    {
        return context?.User is not null &&
            await authorizationService.AuthorizeAsync(context.User, MessagingPermissions.ViewAllConversations);
    }
}
