using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
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
            new DataSetDescriptor(
                ContactCenterReportDataSets.QueueItems,
                stringLocalizer["Queue items"],
                stringLocalizer["One row per piece of work that entered a queue: where it waited, who took it, and for how long."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var item = S["Queue item"].Value;
        var timeline = S["Timeline"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Queue item ID"], DataFieldType.Text, record => record.ItemId, item).Identifier();
        AddField(nameof(QueueItem.QueueId), S["Queue ID"], DataFieldType.Text, record => record.QueueId, item).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField(nameof(QueueItem.ActivityItemId), S["Activity ID"], DataFieldType.Text, record => record.ActivityItemId, item).Identifier(ContactCenterReportDataSets.ActivityReference());
        AddField(nameof(QueueItem.DialerProfileId), S["Dialer profile ID"], DataFieldType.Text, record => record.DialerProfileId, item).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.DialerProfiles));
        AddField(nameof(QueueItem.Status), S["Status"], DataFieldType.Text, record => record.Status, item);
        AddField(nameof(QueueItem.Priority), S["Priority"], DataFieldType.Text, record => record.Priority, item);
        AddField(nameof(QueueItem.AgentId), S["Agent ID"], DataFieldType.Text, record => record.AgentId, item).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentProfiles));
        AddField(nameof(QueueItem.StickyAgentUserId), S["Preferred agent user ID"], DataFieldType.Text, record => record.StickyAgentUserId, item).Identifier(ContactCenterReportDataSets.UserReference());
        AddField(nameof(QueueItem.OverflowedFromQueueId), S["Overflowed from queue ID"], DataFieldType.Text, record => record.OverflowedFromQueueId, item).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField("DeclineCount", S["Declines"], DataFieldType.Integer, record => record.DeclinedAgentIds?.Count ?? 0, item);
        AddField("OverflowCount", S["Overflows"], DataFieldType.Integer, record => record.OverflowHistory?.Count ?? 0, item);
        AddField(nameof(QueueItem.TreatmentStepsPlayed), S["Treatment steps played"], DataFieldType.Integer, record => record.TreatmentStepsPlayed, item);

        AddField(nameof(QueueItem.EnqueuedUtc), S["Enqueued"], DataFieldType.DateTime, record => record.EnqueuedUtc, timeline);
        AddField(nameof(QueueItem.QueueEnteredUtc), S["Entered current queue"], DataFieldType.DateTime, record => record.QueueEnteredUtc, timeline);
        AddField(nameof(QueueItem.DequeuedUtc), S["Dequeued"], DataFieldType.DateTime, record => record.DequeuedUtc, timeline);
        AddField(nameof(QueueItem.CallbackOfferedUtc), S["Callback offered"], DataFieldType.DateTime, record => record.CallbackOfferedUtc, timeline);
        AddField(nameof(QueueItem.CallbackAcceptedUtc), S["Callback accepted"], DataFieldType.DateTime, record => record.CallbackAcceptedUtc, timeline);
        AddField(nameof(QueueItem.DialedUtc), S["Dialed"], DataFieldType.DateTime, record => record.DialedUtc, timeline);
        AddField(nameof(QueueItem.ModifiedUtc), S["Modified"], DataFieldType.DateTime, record => record.ModifiedUtc, timeline);
        AddField("WaitSeconds", S["Wait (seconds)"], DataFieldType.Decimal, record => SecondsBetween(record.EnqueuedUtc, record.DequeuedUtc), timeline);

        CompleteDescription();
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
    protected override void DescribeColumns(ReportIndexConditions<QueueItemIndex> columns)
    {
        columns
            .Enum(nameof(QueueItem.Status), index => index.Status)
            .Enum(nameof(QueueItem.Priority), index => index.Priority);
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<QueueItemIndex, DateTime>> Column)? DateColumn
        => (nameof(QueueItem.EnqueuedUtc), index => index.EnqueuedUtc);
}
