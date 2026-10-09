using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;

/// <summary>
/// One row per work distribution queue, with how it routes work and the service targets it keeps.
/// </summary>
public sealed class QueueReportDataSet : ContactCenterReportDataSet<ActivityQueue, ActivityQueueIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session queues are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public QueueReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<QueueReportDataSet> stringLocalizer)
        : base(
            new ReportDataSetDescriptor(
                ContactCenterReportDataSets.Queues,
                stringLocalizer["Queues"],
                stringLocalizer["One row per queue, with how it routes work and the service targets it keeps."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var queue = S["Queue"].Value;
        var targets = S["Service targets"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Queue ID"], ReportDataType.Text, record => record.ItemId, queue, isIdentifier: true);
        AddField(nameof(ActivityQueue.Name), S["Name"], ReportDataType.Text, record => record.Name, queue);
        AddField(nameof(ActivityQueue.Description), S["Description"], ReportDataType.Text, record => record.Description, queue);
        AddField(nameof(ActivityQueue.QueueGroupId), S["Queue group ID"], ReportDataType.Text, record => record.QueueGroupId, queue, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.QueueGroups));
        AddField(nameof(ActivityQueue.Enabled), S["Enabled"], ReportDataType.Boolean, record => record.Enabled, queue);
        AddField(nameof(ActivityQueue.DefaultPriority), S["Default priority"], ReportDataType.Text, record => record.DefaultPriority, queue);
        AddField(nameof(ActivityQueue.RoutingStrategy), S["Routing strategy"], ReportDataType.Text, record => record.RoutingStrategy, queue);
        AddField(nameof(ActivityQueue.PreferStickyAgent), S["Prefers the last agent"], ReportDataType.Boolean, record => record.PreferStickyAgent, queue);
        AddField(nameof(ActivityQueue.RequiredSkills), S["Required skills"], ReportDataType.Text, record => Join(record.RequiredSkills), queue);
        AddField(nameof(ActivityQueue.UnansweredOfferAction), S["Unanswered offer"], ReportDataType.Text, record => record.UnansweredOfferAction, queue);
        AddField(nameof(ActivityQueue.BusinessHoursCalendarId), S["Business hours calendar ID"], ReportDataType.Text, record => record.BusinessHoursCalendarId, queue, isIdentifier: true);
        AddField(nameof(ActivityQueue.AfterHoursAction), S["After hours"], ReportDataType.Text, record => record.AfterHoursAction, queue);
        AddField(nameof(ActivityQueue.OverflowQueueId), S["Overflow queue ID"], ReportDataType.Text, record => record.OverflowQueueId, queue, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField(nameof(ActivityQueue.InboundChannelEndpointId), S["Inbound channel endpoint ID"], ReportDataType.Text, record => record.InboundChannelEndpointId, queue, isIdentifier: true);
        AddField(nameof(ActivityQueue.CreatedUtc), S["Created"], ReportDataType.DateTime, record => record.CreatedUtc, queue);
        AddField(nameof(ActivityQueue.ModifiedUtc), S["Modified"], ReportDataType.DateTime, record => record.ModifiedUtc, queue);

        AddField(nameof(ActivityQueue.EnableSlaAging), S["Ages by service level"], ReportDataType.Boolean, record => record.EnableSlaAging, targets);
        AddField(nameof(ActivityQueue.SlaThresholdSeconds), S["Service level threshold (seconds)"], ReportDataType.Integer, record => record.SlaThresholdSeconds, targets);
        AddField(nameof(ActivityQueue.FirstResponseTargetSeconds), S["First response target (seconds)"], ReportDataType.Integer, record => record.FirstResponseTargetSeconds, targets);
        AddField(nameof(ActivityQueue.ReservationTimeoutSeconds), S["Offer timeout (seconds)"], ReportDataType.Integer, record => record.ReservationTimeoutSeconds, targets);
        AddField(nameof(ActivityQueue.OverflowAfterSeconds), S["Overflow after (seconds)"], ReportDataType.Integer, record => record.OverflowAfterSeconds, targets);
        AddField(nameof(ActivityQueue.MaxWaitSeconds), S["Maximum wait (seconds)"], ReportDataType.Integer, record => record.MaxWaitSeconds, targets);
        AddField(nameof(ActivityQueue.MaxWaitAction), S["When the wait is too long"], ReportDataType.Text, record => record.MaxWaitAction, targets);
        AddField(nameof(ActivityQueue.MaxQueueSize), S["Maximum queue size"], ReportDataType.Integer, record => record.MaxQueueSize, targets);
        AddField(nameof(ActivityQueue.QueueFullAction), S["When the queue is full"], ReportDataType.Text, record => record.QueueFullAction, targets);
    }
}
