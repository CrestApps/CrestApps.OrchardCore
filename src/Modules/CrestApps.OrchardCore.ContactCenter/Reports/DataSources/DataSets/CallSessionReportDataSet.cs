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
/// One row per voice call session: the provider call behind an interaction, its state, parties, and timings. Provider
/// troubleshooting metadata and the call topology are not exposed.
/// </summary>
public sealed class CallSessionReportDataSet : ContactCenterReportDataSet<CallSession, CallSessionIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallSessionReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session call sessions are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CallSessionReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<CallSessionReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                ContactCenterReportDataSets.CallSessions,
                stringLocalizer["Call sessions"],
                stringLocalizer["One row per voice call: its provider call, state, parties and timings."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var call = S["Call"].Value;
        var timeline = S["Timeline"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Call session ID"], DataFieldType.Text, record => record.ItemId, call).Identifier();
        AddField(nameof(CallSession.InteractionId), S["Interaction ID"], DataFieldType.Text, record => record.InteractionId, call).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Interactions));
        AddField(nameof(CallSession.ActivityItemId), S["Activity ID"], DataFieldType.Text, record => record.ActivityItemId, call).Identifier(ContactCenterReportDataSets.ActivityReference());
        AddField(nameof(CallSession.ProviderName), S["Provider"], DataFieldType.Text, record => record.ProviderName, call);
        AddField(nameof(CallSession.ProviderCallId), S["Provider call ID"], DataFieldType.Text, record => record.ProviderCallId, call).Identifier();
        AddField(nameof(CallSession.DeliveryModel), S["Delivery model"], DataFieldType.Text, record => record.DeliveryModel, call);
        AddField(nameof(CallSession.Direction), S["Direction"], DataFieldType.Text, record => record.Direction, call);
        AddField(nameof(CallSession.State), S["State"], DataFieldType.Text, record => record.State, call);
        AddField(nameof(CallSession.HangupCause), S["Hangup cause"], DataFieldType.Text, record => record.HangupCause, call);
        AddField(nameof(CallSession.AgentId), S["Agent ID"], DataFieldType.Text, record => record.AgentId, call).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentProfiles));
        AddField(nameof(CallSession.AgentSessionId), S["Agent session ID"], DataFieldType.Text, record => record.AgentSessionId, call).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentSessions));
        AddField(nameof(CallSession.QueueId), S["Queue ID"], DataFieldType.Text, record => record.QueueId, call).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField(nameof(CallSession.FromAddress), S["From"], DataFieldType.Text, record => record.FromAddress, call);
        AddField(nameof(CallSession.ToAddress), S["To"], DataFieldType.Text, record => record.ToAddress, call);
        AddField(nameof(CallSession.RecordingState), S["Recording state"], DataFieldType.Text, record => record.RecordingState, call);
        AddField(nameof(CallSession.IsOnHold), S["On hold"], DataFieldType.Boolean, record => record.IsOnHold, call);
        AddField(nameof(CallSession.IsMuted), S["Muted"], DataFieldType.Boolean, record => record.IsMuted, call);
        AddField("LegCount", S["Legs"], DataFieldType.Integer, record => record.Legs?.Count ?? 0, call);

        AddField(nameof(CallSession.CreatedUtc), S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, timeline);
        AddField(nameof(CallSession.StartedUtc), S["Started"], DataFieldType.DateTime, record => record.StartedUtc, timeline);
        AddField(nameof(CallSession.AnsweredUtc), S["Answered"], DataFieldType.DateTime, record => record.AnsweredUtc, timeline);
        AddField(nameof(CallSession.EndedUtc), S["Ended"], DataFieldType.DateTime, record => record.EndedUtc, timeline);
        AddField(nameof(CallSession.ModifiedUtc), S["Modified"], DataFieldType.DateTime, record => record.ModifiedUtc, timeline);
        AddField(nameof(CallSession.TalkSeconds), S["Talk (seconds)"], DataFieldType.Decimal, record => record.TalkSeconds, timeline);
        AddField(nameof(CallSession.HoldSeconds), S["Hold (seconds)"], DataFieldType.Decimal, record => record.HoldSeconds, timeline);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CallSessionIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CallSessionIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["InteractionId"] = index => index.InteractionId,
            ["ActivityItemId"] = index => index.ActivityItemId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<CallSessionIndex> columns)
    {
        // The index holds the canonical name of the provider rather than the record's, so a filter on the provider is
        // left to the consumer.
        columns.Enum(nameof(CallSession.State), index => index.State);
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<CallSessionIndex, DateTime>> Column)? DateColumn
        => (nameof(CallSession.CreatedUtc), index => index.CreatedUtc);
}
