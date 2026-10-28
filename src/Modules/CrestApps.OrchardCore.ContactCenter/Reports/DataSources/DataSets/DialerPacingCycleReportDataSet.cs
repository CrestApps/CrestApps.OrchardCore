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
/// One row per pacing cycle record of a campaign queue: when the cycle ran, how it ended and why, how many calls it
/// placed, and what routing and the compliance gate decided in it. Consecutive cycles of one queue that ended the same
/// way without placing a call are one record, with how many cycles it stands for.
/// </summary>
public sealed class DialerPacingCycleReportDataSet : ContactCenterReportDataSet<DialerPacingCycle, DialerPacingCycleIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DialerPacingCycleReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session pacing cycles are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialerPacingCycleReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<DialerPacingCycleReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                ContactCenterReportDataSets.DialerPacingCycles,
                stringLocalizer["Dialer pacing cycles"],
                stringLocalizer["One row per pacing cycle of a campaign queue: how it ended, the calls it placed, and what routing and the compliance gate decided."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var cycle = S["Pacing cycle"].Value;
        var decisions = S["Decisions"].Value;
        var timeline = S["Timeline"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Pacing cycle ID"], DataFieldType.Text, record => record.ItemId, cycle).Identifier();
        AddField(nameof(DialerPacingCycle.QueueId), S["Queue ID"], DataFieldType.Text, record => record.QueueId, cycle).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField(nameof(DialerPacingCycle.CampaignId), S["Campaign ID"], DataFieldType.Text, record => record.CampaignId, cycle).Identifier(ContactCenterReportDataSets.CampaignReference());
        AddField(nameof(DialerPacingCycle.DialerProfileId), S["Dialer profile ID"], DataFieldType.Text, record => record.DialerProfileId, cycle).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.DialerProfiles));
        AddField(nameof(DialerPacingCycle.Outcome), S["Outcome"], DataFieldType.Text, record => record.Outcome, cycle);
        AddField(nameof(DialerPacingCycle.Detail), S["Detail"], DataFieldType.Text, record => record.Detail, cycle);
        AddField(nameof(DialerPacingCycle.CycleCount), S["Cycles"], DataFieldType.Integer, record => record.CycleCount, cycle);
        AddField(nameof(DialerPacingCycle.CallsPlaced), S["Calls placed"], DataFieldType.Integer, record => record.CallsPlaced, cycle);

        AddField(nameof(DialerPacingCycle.RoutingSucceeded), S["Records assigned"], DataFieldType.Integer, record => record.RoutingSucceeded, decisions);
        AddField(nameof(DialerPacingCycle.RoutingNoAgent), S["No agent found"], DataFieldType.Integer, record => record.RoutingNoAgent, decisions);
        AddField(nameof(DialerPacingCycle.Suppressed), S["Suppressed"], DataFieldType.Integer, record => record.Suppressed, decisions);
        AddField(nameof(DialerPacingCycle.LastPlacedInteractionId), S["Last placed interaction ID"], DataFieldType.Text, record => record.LastPlacedInteractionId, decisions).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Interactions));
        AddField(nameof(DialerPacingCycle.LastPlacedActivityItemId), S["Last placed activity ID"], DataFieldType.Text, record => record.LastPlacedActivityItemId, decisions).Identifier(ContactCenterReportDataSets.ActivityReference());
        AddField(nameof(DialerPacingCycle.LastPlacedAgentId), S["Last placed for agent ID"], DataFieldType.Text, record => record.LastPlacedAgentId, decisions).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentProfiles));
        AddField(nameof(DialerPacingCycle.LastAssignedAgentId), S["Last assigned agent ID"], DataFieldType.Text, record => record.LastAssignedAgentId, decisions).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentProfiles));
        AddField(nameof(DialerPacingCycle.LastAssignedActivityItemId), S["Last assigned activity ID"], DataFieldType.Text, record => record.LastAssignedActivityItemId, decisions).Identifier(ContactCenterReportDataSets.ActivityReference());

        AddField(nameof(DialerPacingCycle.FirstStartedUtc), S["First started"], DataFieldType.DateTime, record => record.FirstStartedUtc, timeline);
        AddField(nameof(DialerPacingCycle.StartedUtc), S["Started"], DataFieldType.DateTime, record => record.StartedUtc, timeline);
        AddField(nameof(DialerPacingCycle.CompletedUtc), S["Completed"], DataFieldType.DateTime, record => record.CompletedUtc, timeline);
        AddField("DurationSeconds", S["Duration (seconds)"], DataFieldType.Decimal, record => SecondsBetween(record.StartedUtc, record.CompletedUtc), timeline);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<DialerPacingCycleIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<DialerPacingCycleIndex, string>>>(StringComparer.Ordinal)
        {
            [ContactCenterReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(DialerPacingCycle.QueueId)] = index => index.QueueId,
            [nameof(DialerPacingCycle.DialerProfileId)] = index => index.DialerProfileId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<DialerPacingCycleIndex> columns)
    {
        columns.Enum(nameof(DialerPacingCycle.Outcome), index => index.Outcome);
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<DialerPacingCycleIndex, DateTime>> Column)? DateColumn
        => (nameof(DialerPacingCycle.CompletedUtc), index => index.CompletedUtc);
}
