using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;

/// <summary>
/// One row per agent session: an agent signed in to the workspace, the queues and campaigns they signed in to, and
/// when they connected. The connection identifiers are not exposed.
/// </summary>
public sealed class AgentSessionReportDataSet : ContactCenterReportDataSet<AgentSession, AgentSessionIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSessionReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session agent sessions are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgentSessionReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<AgentSessionReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                ContactCenterReportDataSets.AgentSessions,
                stringLocalizer["Agent sessions"],
                stringLocalizer["One row per agent session: who signed in, to which queues and campaigns, and when they were last seen."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var group = S["Agent session"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Agent session ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField(nameof(AgentSession.UserId), S["User ID"], DataFieldType.Text, record => record.UserId, group).Identifier(ContactCenterReportDataSets.UserReference());
        AddField(nameof(AgentSession.UserName), S["User name"], DataFieldType.Text, record => record.UserName, group);
        AddField(nameof(AgentSession.DisplayName), S["Display name"], DataFieldType.Text, record => record.DisplayName, group);
        AddField(nameof(AgentSession.IsOnline), S["Online"], DataFieldType.Boolean, record => record.IsOnline, group);
        AddField("ConnectionCount", S["Connections"], DataFieldType.Integer, record => record.ConnectionIds?.Count ?? 0, group);
        AddField(nameof(AgentSession.QueueIds), S["Queue IDs"], DataFieldType.Text, record => Join(record.QueueIds), group);
        AddField(nameof(AgentSession.CampaignIds), S["Campaign IDs"], DataFieldType.Text, record => Join(record.CampaignIds), group);
        AddField(nameof(AgentSession.ConnectedUtc), S["Connected"], DataFieldType.DateTime, record => record.ConnectedUtc, group);
        AddField(nameof(AgentSession.LastHeartbeatUtc), S["Last seen"], DataFieldType.DateTime, record => record.LastHeartbeatUtc, group);
        AddField(nameof(AgentSession.LastDisconnectedUtc), S["Last disconnected"], DataFieldType.DateTime, record => record.LastDisconnectedUtc, group);
        AddField(nameof(AgentSession.CreatedUtc), S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, group);
        AddField(nameof(AgentSession.ModifiedUtc), S["Modified"], DataFieldType.DateTime, record => record.ModifiedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<AgentSessionIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AgentSessionIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["UserId"] = index => index.UserId,
        };
}
