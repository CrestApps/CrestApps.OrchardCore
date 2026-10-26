using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;

/// <summary>
/// One row per agent state change in the event log: the append-only record of every state an agent entered, who or
/// what changed it, and why. Both the state audit and the older presence events are read, so a period before the audit
/// existed is covered too. The rows are the changes as recorded: a change that a later sign-off superseded is still a
/// row.
/// </summary>
/// <remarks>
/// Each row is read from its event's payload, so the data set does not group itself in the database, and a join narrows
/// the read only on the event and agent identifiers, which the event index holds.
/// </remarks>
public sealed class AgentStateChangeReportDataSet : IndexedReportDataSet<InteractionEvent, InteractionEventIndex, AgentStateChangeReportRow>, IContactCenterReportDataSet
{
    private static readonly string[] _eventTypes =
    [
        ContactCenterConstants.Events.AgentStateChanged,
        ContactCenterConstants.Events.AgentSignedIn,
        ContactCenterConstants.Events.AgentSignedOut,
        ContactCenterConstants.Events.AgentPresenceChanged,
    ];

    private readonly IInteractionEventUpcastService _upcastService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateChangeReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session events are read from.</param>
    /// <param name="upcastService">The service that brings an event written by an earlier release to the current schema.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgentStateChangeReportDataSet(
        ISession session,
        IInteractionEventUpcastService upcastService,
        IAuthorizationService authorizationService,
        IStringLocalizer<AgentStateChangeReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                ContactCenterReportDataSets.AgentStateChanges,
                stringLocalizer["Agent state changes"],
                stringLocalizer["One row per agent state change: the state an agent left and entered, when, why, and who or what changed it."]),
            session,
            authorizationService,
            ContactCenterStorage.CollectionName,
            ContactCenterPermissions.ViewReports)
    {
        _upcastService = upcastService;

        var S = stringLocalizer;
        var change = S["State change"].Value;
        var context = S["Context"].Value;
        var timeline = S["Timeline"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Event ID"], DataFieldType.Text, row => row.EventId, change).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.InteractionEvents));
        AddField(nameof(AgentStateChangeReportRow.EventType), S["Event type"], DataFieldType.Text, row => row.EventType, change);
        AddField(nameof(AgentStateChangeReportRow.FromAudit), S["From the state audit"], DataFieldType.Boolean, row => row.FromAudit, change);
        AddField(nameof(AgentStateChangeReportRow.AgentId), S["Agent ID"], DataFieldType.Text, row => row.AgentId, change).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentProfiles));
        AddField(nameof(AgentStateChangeReportRow.UserId), S["User ID"], DataFieldType.Text, row => row.UserId, change).Identifier(ContactCenterReportDataSets.UserReference());
        AddField(nameof(AgentStateChangeReportRow.PreviousState), S["Previous state"], DataFieldType.Text, row => row.PreviousState, change);
        AddField(nameof(AgentStateChangeReportRow.CurrentState), S["State"], DataFieldType.Text, row => row.CurrentState, change);
        AddField(nameof(AgentStateChangeReportRow.RequestedState), S["Requested state"], DataFieldType.Text, row => row.RequestedState, change);
        AddField(nameof(AgentStateChangeReportRow.ReasonCodeId), S["Reason code ID"], DataFieldType.Text, row => row.ReasonCodeId, change).Identifier();
        AddField(nameof(AgentStateChangeReportRow.Reason), S["Reason"], DataFieldType.Text, row => row.Reason, change);
        AddField(nameof(AgentStateChangeReportRow.Source), S["Caused by"], DataFieldType.Text, row => row.Source, change);
        AddField(nameof(AgentStateChangeReportRow.ActorType), S["Changed by"], DataFieldType.Text, row => row.ActorType, change);
        AddField(nameof(AgentStateChangeReportRow.ActorId), S["Changed by (ID)"], DataFieldType.Text, row => row.ActorId, change).Identifier();

        AddField(nameof(AgentStateChangeReportRow.InteractionId), S["Interaction ID"], DataFieldType.Text, row => row.InteractionId, context).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Interactions));
        AddField(nameof(AgentStateChangeReportRow.ReservationId), S["Reservation ID"], DataFieldType.Text, row => row.ReservationId, context).Identifier();
        AddField(nameof(AgentStateChangeReportRow.ReleaseReason), S["Release reason"], DataFieldType.Text, row => row.ReleaseReason, context);
        AddField(nameof(AgentStateChangeReportRow.AgentSessionId), S["Agent session ID"], DataFieldType.Text, row => row.AgentSessionId, context).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentSessions));
        AddField(nameof(AgentStateChangeReportRow.QueueIds), S["Queue IDs"], DataFieldType.Text, row => Join(row.QueueIds), context);
        AddField(nameof(AgentStateChangeReportRow.CampaignIds), S["Campaign IDs"], DataFieldType.Text, row => Join(row.CampaignIds), context);

        AddField(nameof(AgentStateChangeReportRow.ChangedUtc), S["Changed"], DataFieldType.DateTime, row => row.ChangedUtc, timeline);
        AddField(nameof(AgentStateChangeReportRow.OccurredUtc), S["Occurred"], DataFieldType.DateTime, row => row.OccurredUtc, timeline);
        AddField(nameof(AgentStateChangeReportRow.RecordedUtc), S["Recorded"], DataFieldType.DateTime, row => row.RecordedUtc, timeline);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<InteractionEventIndex, DateTime>> Column)? DateColumn
        => (nameof(AgentStateChangeReportRow.OccurredUtc), index => index.OccurredUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<InteractionEventIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<InteractionEventIndex, string>>>(StringComparer.Ordinal)
        {
            [ContactCenterReportDataSets.ItemIdField] = index => index.ItemId,
            [nameof(AgentStateChangeReportRow.AgentId)] = index => index.AggregateId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<InteractionEventIndex> columns)
    {
        columns.Text(nameof(AgentStateChangeReportRow.EventType), nameof(InteractionEventIndex.EventType));
    }

    /// <inheritdoc/>
    protected override Task<Func<IQueryIndex<InteractionEventIndex>, IQueryIndex<InteractionEventIndex>>> RestrictAsync(
        DataSourceContext context,
        CancellationToken cancellationToken)
    {
        const string aggregateType = nameof(AgentProfile);
        var eventTypes = _eventTypes;

        return Task.FromResult<Func<IQueryIndex<InteractionEventIndex>, IQueryIndex<InteractionEventIndex>>>(rows => rows
            .Where(index => index.AggregateType == aggregateType && index.EventType.IsIn(eventTypes)));
    }

    /// <inheritdoc/>
    protected override IEnumerable<AgentStateChangeReportRow> GetRows(InteractionEvent document)
    {
        var payloadReadable = true;

        try
        {
            _upcastService.Upcast(document);
        }
        catch (InteractionEventUpcastException)
        {
            // A payload this release cannot interpret is never read: the row keeps the event's own values only.
            payloadReadable = false;
        }

        yield return AgentStateChangeReportRow.Create(document, payloadReadable);
    }

    private static string Join(IEnumerable<string> values)
    {
        var text = string.Join(", ", (values ?? []).Where(value => !string.IsNullOrEmpty(value)));

        return text.Length == 0 ? null : text;
    }
}
