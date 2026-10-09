using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
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
            new ReportDataSetDescriptor(
                ContactCenterReportDataSets.InteractionEvents,
                stringLocalizer["Interaction events"],
                stringLocalizer["One row per Contact Center event: what happened, to which record, by whom, and when."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var group = S["Event"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Event ID"], ReportDataType.Text, record => record.ItemId, group, isIdentifier: true);
        AddField(nameof(InteractionEvent.InteractionId), S["Interaction ID"], ReportDataType.Text, record => record.InteractionId, group, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Interactions));
        AddField(nameof(InteractionEvent.EventType), S["Event type"], ReportDataType.Text, record => record.EventType, group);
        AddField(nameof(InteractionEvent.AggregateType), S["Record type"], ReportDataType.Text, record => record.AggregateType, group);
        AddField(nameof(InteractionEvent.AggregateId), S["Record ID"], ReportDataType.Text, record => record.AggregateId, group, isIdentifier: true);
        AddField(nameof(InteractionEvent.CorrelationId), S["Correlation ID"], ReportDataType.Text, record => record.CorrelationId, group, isIdentifier: true);
        AddField(nameof(InteractionEvent.CausationId), S["Caused by event ID"], ReportDataType.Text, record => record.CausationId, group, isIdentifier: true);
        AddField(nameof(InteractionEvent.ActorId), S["Actor ID"], ReportDataType.Text, record => record.ActorId, group, isIdentifier: true);
        AddField(nameof(InteractionEvent.ActorType), S["Actor type"], ReportDataType.Text, record => record.ActorType, group);
        AddField(nameof(InteractionEvent.SourceComponent), S["Source component"], ReportDataType.Text, record => record.SourceComponent, group);
        AddField(nameof(InteractionEvent.SchemaVersion), S["Schema version"], ReportDataType.Integer, record => record.SchemaVersion, group);
        AddField(nameof(InteractionEvent.OccurredUtc), S["Occurred"], ReportDataType.DateTime, record => record.OccurredUtc, group);
        AddField(nameof(InteractionEvent.RecordedUtc), S["Recorded"], ReportDataType.DateTime, record => record.RecordedUtc, group);
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<InteractionEventIndex, DateTime>> Column)? DateColumn
        => (nameof(InteractionEvent.OccurredUtc), index => index.OccurredUtc);
}
