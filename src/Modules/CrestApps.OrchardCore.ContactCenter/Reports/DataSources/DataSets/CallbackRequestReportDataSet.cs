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
/// One row per callback request: who is to be called back, when, for which queue or campaign, and how it is going.
/// The dialer's lease tokens are not exposed.
/// </summary>
public sealed class CallbackRequestReportDataSet : ContactCenterReportDataSet<CallbackRequest, CallbackRequestIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackRequestReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session callback requests are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CallbackRequestReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<CallbackRequestReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                ContactCenterReportDataSets.CallbackRequests,
                stringLocalizer["Callback requests"],
                stringLocalizer["One row per callback request: who to call back, when, and how it is going."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var group = S["Callback"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Callback ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField(nameof(CallbackRequest.Destination), S["Destination"], DataFieldType.Text, record => record.Destination, group);
        AddField(nameof(CallbackRequest.ContactContentItemId), S["Contact ID"], DataFieldType.Text, record => record.ContactContentItemId, group).Identifier();
        AddField(nameof(CallbackRequest.ContactContentType), S["Contact type"], DataFieldType.Text, record => record.ContactContentType, group);
        AddField(nameof(CallbackRequest.CampaignId), S["Campaign ID"], DataFieldType.Text, record => record.CampaignId, group).Identifier();
        AddField(nameof(CallbackRequest.QueueId), S["Queue ID"], DataFieldType.Text, record => record.QueueId, group).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField(nameof(CallbackRequest.AgentId), S["Agent ID"], DataFieldType.Text, record => record.AgentId, group).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentProfiles));
        AddField(nameof(CallbackRequest.ActivityItemId), S["Activity ID"], DataFieldType.Text, record => record.ActivityItemId, group).Identifier(ContactCenterReportDataSets.ActivityReference());
        AddField(nameof(CallbackRequest.Status), S["Status"], DataFieldType.Text, record => record.Status, group);
        AddField(nameof(CallbackRequest.Attempts), S["Attempts"], DataFieldType.Integer, record => record.Attempts, group);
        AddField(nameof(CallbackRequest.Notes), S["Notes"], DataFieldType.Text, record => record.Notes, group);
        AddField(nameof(CallbackRequest.RequestedUtc), S["Requested"], DataFieldType.DateTime, record => record.RequestedUtc, group);
        AddField(nameof(CallbackRequest.ScheduledUtc), S["Scheduled"], DataFieldType.DateTime, record => record.ScheduledUtc, group);
        AddField(nameof(CallbackRequest.CreatedUtc), S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, group);
        AddField(nameof(CallbackRequest.ModifiedUtc), S["Modified"], DataFieldType.DateTime, record => record.ModifiedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<CallbackRequestIndex, DateTime>> Column)? DateColumn
        => (nameof(CallbackRequest.ScheduledUtc), index => index.ScheduledUtc);
}
