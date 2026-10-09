using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;

/// <summary>
/// One row per agent profile: the user it belongs to, the queues and campaigns it works, and its presence. The
/// voicemail greeting is not exposed.
/// </summary>
public sealed class AgentProfileReportDataSet : ContactCenterReportDataSet<AgentProfile, AgentProfileIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentProfileReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session agent profiles are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgentProfileReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<AgentProfileReportDataSet> stringLocalizer)
        : base(
            new ReportDataSetDescriptor(
                ContactCenterReportDataSets.AgentProfiles,
                stringLocalizer["Agent profiles"],
                stringLocalizer["One row per agent: the user, the queues and campaigns they work, and their presence."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var agent = S["Agent"].Value;
        var presence = S["Presence"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Agent ID"], ReportDataType.Text, record => record.ItemId, agent, isIdentifier: true);
        AddField(nameof(AgentProfile.Name), S["Name"], ReportDataType.Text, record => record.Name, agent);
        AddField(nameof(AgentProfile.DisplayName), S["Display name"], ReportDataType.Text, record => record.DisplayName, agent);
        AddField(nameof(AgentProfile.UserId), S["User ID"], ReportDataType.Text, record => record.UserId, agent, isIdentifier: true, ContactCenterReportDataSets.UserReference());
        AddField(nameof(AgentProfile.UserName), S["User name"], ReportDataType.Text, record => record.UserName, agent);
        AddField(nameof(AgentProfile.MaxConcurrentInteractions), S["Concurrent interactions"], ReportDataType.Integer, record => record.MaxConcurrentInteractions, agent);
        AddField(nameof(AgentProfile.QueueIds), S["Queue IDs"], ReportDataType.Text, record => Join(record.QueueIds), agent);
        AddField(nameof(AgentProfile.AllowedQueueIds), S["Allowed queue IDs"], ReportDataType.Text, record => Join(record.AllowedQueueIds), agent);
        AddField(nameof(AgentProfile.CampaignIds), S["Campaign IDs"], ReportDataType.Text, record => Join(record.CampaignIds), agent);
        AddField(nameof(AgentProfile.AllowedCampaignIds), S["Allowed campaign IDs"], ReportDataType.Text, record => Join(record.AllowedCampaignIds), agent);
        AddField(nameof(AgentProfile.Skills), S["Skills"], ReportDataType.Text, record => Join(record.Skills), agent);
        AddField(nameof(AgentProfile.CreatedUtc), S["Created"], ReportDataType.DateTime, record => record.CreatedUtc, agent);
        AddField(nameof(AgentProfile.ModifiedUtc), S["Modified"], ReportDataType.DateTime, record => record.ModifiedUtc, agent);

        AddField(nameof(AgentProfile.PresenceStatus), S["Presence"], ReportDataType.Text, record => record.PresenceStatus, presence);
        AddField(nameof(AgentProfile.PresenceReason), S["Presence reason"], ReportDataType.Text, record => record.PresenceReason, presence);
        AddField(nameof(AgentProfile.PresenceReasonCodeId), S["Presence reason code ID"], ReportDataType.Text, record => record.PresenceReasonCodeId, presence, isIdentifier: true);
        AddField(nameof(AgentProfile.RequestedPresenceStatus), S["Requested presence"], ReportDataType.Text, record => record.RequestedPresenceStatus, presence);
        AddField(nameof(AgentProfile.PresenceRequestedUtc), S["Presence requested"], ReportDataType.DateTime, record => record.PresenceRequestedUtc, presence);
        AddField(nameof(AgentProfile.PresenceChangedUtc), S["Presence changed"], ReportDataType.DateTime, record => record.PresenceChangedUtc, presence);
        AddField(nameof(AgentProfile.IdleSinceUtc), S["Idle since"], ReportDataType.DateTime, record => record.IdleSinceUtc, presence);
        AddField(nameof(AgentProfile.LastAssignedUtc), S["Last assigned"], ReportDataType.DateTime, record => record.LastAssignedUtc, presence);
        AddField(nameof(AgentProfile.LastWorkCompletedUtc), S["Last work completed"], ReportDataType.DateTime, record => record.LastWorkCompletedUtc, presence);
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<AgentProfileIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AgentProfileIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["UserId"] = index => index.UserId,
        };
}
