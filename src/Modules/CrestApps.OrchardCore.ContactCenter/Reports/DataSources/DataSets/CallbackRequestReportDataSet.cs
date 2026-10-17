using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
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
            new ReportDataSetDescriptor(
                ContactCenterReportDataSets.CallbackRequests,
                stringLocalizer["Callback requests"],
                stringLocalizer["One row per callback request: who to call back, when, and how it is going."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var group = S["Callback"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Callback ID"], ReportDataType.Text, record => record.ItemId, group, isIdentifier: true);
        AddField(nameof(CallbackRequest.Destination), S["Destination"], ReportDataType.Text, record => record.Destination, group);
        AddField(nameof(CallbackRequest.ContactContentItemId), S["Contact ID"], ReportDataType.Text, record => record.ContactContentItemId, group, isIdentifier: true);
        AddField(nameof(CallbackRequest.ContactContentType), S["Contact type"], ReportDataType.Text, record => record.ContactContentType, group);
        AddField(nameof(CallbackRequest.CampaignId), S["Campaign ID"], ReportDataType.Text, record => record.CampaignId, group, isIdentifier: true);
        AddField(nameof(CallbackRequest.QueueId), S["Queue ID"], ReportDataType.Text, record => record.QueueId, group, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField(nameof(CallbackRequest.AgentId), S["Agent ID"], ReportDataType.Text, record => record.AgentId, group, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentProfiles));
        AddField(nameof(CallbackRequest.ActivityItemId), S["Activity ID"], ReportDataType.Text, record => record.ActivityItemId, group, isIdentifier: true, ContactCenterReportDataSets.ActivityReference());
        AddField(nameof(CallbackRequest.Status), S["Status"], ReportDataType.Text, record => record.Status, group);
        AddField(nameof(CallbackRequest.Attempts), S["Attempts"], ReportDataType.Integer, record => record.Attempts, group);
        AddField(nameof(CallbackRequest.Notes), S["Notes"], ReportDataType.Text, record => record.Notes, group);
        AddField(nameof(CallbackRequest.RequestedUtc), S["Requested"], ReportDataType.DateTime, record => record.RequestedUtc, group);
        AddField(nameof(CallbackRequest.ScheduledUtc), S["Scheduled"], ReportDataType.DateTime, record => record.ScheduledUtc, group);
        AddField(nameof(CallbackRequest.CreatedUtc), S["Created"], ReportDataType.DateTime, record => record.CreatedUtc, group);
        AddField(nameof(CallbackRequest.ModifiedUtc), S["Modified"], ReportDataType.DateTime, record => record.ModifiedUtc, group);
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<CallbackRequestIndex, DateTime>> Column)? DateColumn
        => (nameof(CallbackRequest.ScheduledUtc), index => index.ScheduledUtc);
}
