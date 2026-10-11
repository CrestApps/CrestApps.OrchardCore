using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;

/// <summary>
/// One row per piece of work that entered a queue: where it waited, who it went to, and how long it waited.
/// </summary>
public sealed class QueueItemReportDataSet : ContactCenterReportDataSet<QueueItem, QueueItemIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueItemReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session queue items are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public QueueItemReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<QueueItemReportDataSet> stringLocalizer)
        : base(
            new ReportDataSetDescriptor(
                ContactCenterReportDataSets.QueueItems,
                stringLocalizer["Queue items"],
                stringLocalizer["One row per piece of work that entered a queue: where it waited, who took it, and for how long."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var item = S["Queue item"].Value;
        var timeline = S["Timeline"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Queue item ID"], ReportDataType.Text, record => record.ItemId, item, isIdentifier: true);
        AddField(nameof(QueueItem.QueueId), S["Queue ID"], ReportDataType.Text, record => record.QueueId, item, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField(nameof(QueueItem.ActivityItemId), S["Activity ID"], ReportDataType.Text, record => record.ActivityItemId, item, isIdentifier: true, ContactCenterReportDataSets.ActivityReference());
        AddField(nameof(QueueItem.DialerProfileId), S["Dialer profile ID"], ReportDataType.Text, record => record.DialerProfileId, item, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.DialerProfiles));
        AddField(nameof(QueueItem.Status), S["Status"], ReportDataType.Text, record => record.Status, item);
        AddField(nameof(QueueItem.Priority), S["Priority"], ReportDataType.Text, record => record.Priority, item);
        AddField(nameof(QueueItem.AgentId), S["Agent ID"], ReportDataType.Text, record => record.AgentId, item, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentProfiles));
        AddField(nameof(QueueItem.StickyAgentUserId), S["Preferred agent user ID"], ReportDataType.Text, record => record.StickyAgentUserId, item, isIdentifier: true, ContactCenterReportDataSets.UserReference());
        AddField(nameof(QueueItem.OverflowedFromQueueId), S["Overflowed from queue ID"], ReportDataType.Text, record => record.OverflowedFromQueueId, item, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField("DeclineCount", S["Declines"], ReportDataType.Integer, record => record.DeclinedAgentIds?.Count ?? 0, item);
        AddField("OverflowCount", S["Overflows"], ReportDataType.Integer, record => record.OverflowHistory?.Count ?? 0, item);
        AddField(nameof(QueueItem.TreatmentStepsPlayed), S["Treatment steps played"], ReportDataType.Integer, record => record.TreatmentStepsPlayed, item);

        AddField(nameof(QueueItem.EnqueuedUtc), S["Enqueued"], ReportDataType.DateTime, record => record.EnqueuedUtc, timeline);
        AddField(nameof(QueueItem.QueueEnteredUtc), S["Entered current queue"], ReportDataType.DateTime, record => record.QueueEnteredUtc, timeline);
        AddField(nameof(QueueItem.DequeuedUtc), S["Dequeued"], ReportDataType.DateTime, record => record.DequeuedUtc, timeline);
        AddField(nameof(QueueItem.CallbackOfferedUtc), S["Callback offered"], ReportDataType.DateTime, record => record.CallbackOfferedUtc, timeline);
        AddField(nameof(QueueItem.CallbackAcceptedUtc), S["Callback accepted"], ReportDataType.DateTime, record => record.CallbackAcceptedUtc, timeline);
        AddField(nameof(QueueItem.DialedUtc), S["Dialed"], ReportDataType.DateTime, record => record.DialedUtc, timeline);
        AddField(nameof(QueueItem.ModifiedUtc), S["Modified"], ReportDataType.DateTime, record => record.ModifiedUtc, timeline);
        AddField("WaitSeconds", S["Wait (seconds)"], ReportDataType.Decimal, record => SecondsBetween(record.EnqueuedUtc, record.DequeuedUtc), timeline);
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<QueueItemIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<QueueItemIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["QueueId"] = index => index.QueueId,
            ["ActivityItemId"] = index => index.ActivityItemId,
        };

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<QueueItemIndex, DateTime>> Column)? DateColumn
        => (nameof(QueueItem.EnqueuedUtc), index => index.EnqueuedUtc);
}
