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
/// One row per entry of the Contact Center's durable event log: what happened, to which record, by whom, and when.
/// The event payload is not exposed.
/// </summary>
public sealed class InteractionEventReportDataSet : ContactCenterReportDataSet<InteractionEvent, InteractionEventIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionEventReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session events are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InteractionEventReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<InteractionEventReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                ContactCenterReportDataSets.InteractionEvents,
                stringLocalizer["Interaction events"],
                stringLocalizer["One row per Contact Center event: what happened, to which record, by whom, and when."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var group = S["Event"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Event ID"], DataFieldType.Text, record => record.ItemId, group).Identifier();
        AddField(nameof(InteractionEvent.InteractionId), S["Interaction ID"], DataFieldType.Text, record => record.InteractionId, group).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Interactions));
        AddField(nameof(InteractionEvent.EventType), S["Event type"], DataFieldType.Text, record => record.EventType, group);
        AddField(nameof(InteractionEvent.AggregateType), S["Record type"], DataFieldType.Text, record => record.AggregateType, group);
        AddField(nameof(InteractionEvent.AggregateId), S["Record ID"], DataFieldType.Text, record => record.AggregateId, group).Identifier();
        AddField(nameof(InteractionEvent.CorrelationId), S["Correlation ID"], DataFieldType.Text, record => record.CorrelationId, group).Identifier();
        AddField(nameof(InteractionEvent.CausationId), S["Caused by event ID"], DataFieldType.Text, record => record.CausationId, group).Identifier();
        AddField(nameof(InteractionEvent.ActorId), S["Actor ID"], DataFieldType.Text, record => record.ActorId, group).Identifier();
        AddField(nameof(InteractionEvent.ActorType), S["Actor type"], DataFieldType.Text, record => record.ActorType, group);
        AddField(nameof(InteractionEvent.SourceComponent), S["Source component"], DataFieldType.Text, record => record.SourceComponent, group);
        AddField(nameof(InteractionEvent.SchemaVersion), S["Schema version"], DataFieldType.Integer, record => record.SchemaVersion, group);
        AddField(nameof(InteractionEvent.OccurredUtc), S["Occurred"], DataFieldType.DateTime, record => record.OccurredUtc, group);
        AddField(nameof(InteractionEvent.RecordedUtc), S["Recorded"], DataFieldType.DateTime, record => record.RecordedUtc, group);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<InteractionEventIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<InteractionEventIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["InteractionId"] = index => index.InteractionId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<InteractionEventIndex> columns)
    {
        columns
            .Text(nameof(InteractionEvent.EventType), nameof(InteractionEventIndex.EventType))
            .Text(nameof(InteractionEvent.AggregateType), nameof(InteractionEventIndex.AggregateType));
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<InteractionEventIndex, DateTime>> Column)? DateColumn
        => (nameof(InteractionEvent.OccurredUtc), index => index.OccurredUtc);
}
