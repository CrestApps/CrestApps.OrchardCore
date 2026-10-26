using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;

/// <summary>
/// One row per entry of the dialer decision log: a routing decision (which agent, if any, a queued record was given to)
/// or a dial suppression (why the compliance gate held a call back). A routing failure repeated many times in a row is
/// one row, with how many decisions it stands for and when the first and the last were made.
/// </summary>
/// <remarks>
/// A row holds exactly the values the decision log's index holds for its event, so the data set groups itself in the
/// database, and the date range and the keys of a join are pushed down to the index.
/// </remarks>
public sealed class DialerDecisionReportDataSet : IndexedReportDataSet<InteractionEvent, DialerDecisionIndex, DialerDecisionIndex>, IContactCenterReportDataSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DialerDecisionReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session decisions are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialerDecisionReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<DialerDecisionReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                ContactCenterReportDataSets.DialerDecisions,
                stringLocalizer["Dialer decisions"],
                stringLocalizer["One row per routing decision or dial suppression: the record, the queue or campaign, the agent, the outcome and the reason."]),
            session,
            authorizationService,
            ContactCenterStorage.CollectionName,
            ContactCenterPermissions.ViewReports)
    {
        var S = stringLocalizer;
        var decision = S["Decision"].Value;
        var timeline = S["Timeline"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Event ID"], DataFieldType.Text, row => row.ItemId, decision).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.InteractionEvents));
        AddField(nameof(DialerDecisionIndex.EventType), S["Decision type"], DataFieldType.Text, row => row.EventType, decision);
        AddField(nameof(DialerDecisionIndex.Succeeded), S["Assigned"], DataFieldType.Boolean, row => row.Succeeded, decision);
        AddField(nameof(DialerDecisionIndex.Reason), S["Reason"], DataFieldType.Text, row => row.Reason, decision);
        AddField(nameof(DialerDecisionIndex.OccurrenceCount), S["Decisions"], DataFieldType.Integer, row => row.OccurrenceCount, decision);
        AddField(nameof(DialerDecisionIndex.QueueId), S["Queue ID"], DataFieldType.Text, row => row.QueueId, decision).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField(nameof(DialerDecisionIndex.CampaignId), S["Campaign ID"], DataFieldType.Text, row => row.CampaignId, decision).Identifier(ContactCenterReportDataSets.CampaignReference());
        AddField(nameof(DialerDecisionIndex.ActivityItemId), S["Activity ID"], DataFieldType.Text, row => row.ActivityItemId, decision).Identifier(ContactCenterReportDataSets.ActivityReference());
        AddField(nameof(DialerDecisionIndex.QueueItemId), S["Queue item ID"], DataFieldType.Text, row => row.QueueItemId, decision).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.QueueItems));
        AddField(nameof(DialerDecisionIndex.AgentId), S["Agent ID"], DataFieldType.Text, row => row.AgentId, decision).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentProfiles));

        AddField(nameof(DialerDecisionIndex.OccurredUtc), S["First made"], DataFieldType.DateTime, row => row.OccurredUtc, timeline);
        AddField(nameof(DialerDecisionIndex.LastSeenUtc), S["Last made"], DataFieldType.DateTime, row => row.LastSeenUtc, timeline);

        CompleteDescription();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Each decision event has one row in the index, holding the values the data set's fields read, so the index can
    /// group the decisions exactly.
    /// </remarks>
    protected override bool CanAggregate => true;

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<DialerDecisionIndex, DateTime>> Column)? DateColumn
        => (nameof(DialerDecisionIndex.LastSeenUtc), index => index.LastSeenUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<DialerDecisionIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<DialerDecisionIndex, string>>>(StringComparer.Ordinal)
        {
            [ContactCenterReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(DialerDecisionIndex.QueueId)] = index => index.QueueId,
            [nameof(DialerDecisionIndex.CampaignId)] = index => index.CampaignId,
            [nameof(DialerDecisionIndex.ActivityItemId)] = index => index.ActivityItemId,
            [nameof(DialerDecisionIndex.QueueItemId)] = index => index.QueueItemId,
            [nameof(DialerDecisionIndex.AgentId)] = index => index.AgentId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<DialerDecisionIndex> columns)
    {
        columns
            .Text(nameof(DialerDecisionIndex.EventType), nameof(DialerDecisionIndex.EventType))
            .Text(nameof(DialerDecisionIndex.Reason), nameof(DialerDecisionIndex.Reason));
    }

    /// <inheritdoc/>
    protected override IEnumerable<DialerDecisionIndex> GetRows(InteractionEvent document)
    {
        // The row is built by the same code that builds the index row, so a value is never other than the one the
        // index holds.
        var row = DialerDecisionIndexProvider.BuildDecision(document);

        if (row is not null)
        {
            yield return row;
        }
    }
}
