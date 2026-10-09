using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
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
            new ReportDataSetDescriptor(
                ContactCenterReportDataSets.AgentSessions,
                stringLocalizer["Agent sessions"],
                stringLocalizer["One row per agent session: who signed in, to which queues and campaigns, and when they were last seen."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var group = S["Agent session"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Agent session ID"], ReportDataType.Text, record => record.ItemId, group, isIdentifier: true);
        AddField(nameof(AgentSession.UserId), S["User ID"], ReportDataType.Text, record => record.UserId, group, isIdentifier: true, ContactCenterReportDataSets.UserReference());
        AddField(nameof(AgentSession.UserName), S["User name"], ReportDataType.Text, record => record.UserName, group);
        AddField(nameof(AgentSession.DisplayName), S["Display name"], ReportDataType.Text, record => record.DisplayName, group);
        AddField(nameof(AgentSession.IsOnline), S["Online"], ReportDataType.Boolean, record => record.IsOnline, group);
        AddField("ConnectionCount", S["Connections"], ReportDataType.Integer, record => record.ConnectionIds?.Count ?? 0, group);
        AddField(nameof(AgentSession.QueueIds), S["Queue IDs"], ReportDataType.Text, record => Join(record.QueueIds), group);
        AddField(nameof(AgentSession.CampaignIds), S["Campaign IDs"], ReportDataType.Text, record => Join(record.CampaignIds), group);
        AddField(nameof(AgentSession.ConnectedUtc), S["Connected"], ReportDataType.DateTime, record => record.ConnectedUtc, group);
        AddField(nameof(AgentSession.LastHeartbeatUtc), S["Last seen"], ReportDataType.DateTime, record => record.LastHeartbeatUtc, group);
        AddField(nameof(AgentSession.LastDisconnectedUtc), S["Last disconnected"], ReportDataType.DateTime, record => record.LastDisconnectedUtc, group);
        AddField(nameof(AgentSession.CreatedUtc), S["Created"], ReportDataType.DateTime, record => record.CreatedUtc, group);
        AddField(nameof(AgentSession.ModifiedUtc), S["Modified"], ReportDataType.DateTime, record => record.ModifiedUtc, group);
    }
}
