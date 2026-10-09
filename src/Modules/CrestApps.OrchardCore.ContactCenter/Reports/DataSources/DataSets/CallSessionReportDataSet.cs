using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
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
            new ReportDataSetDescriptor(
                ContactCenterReportDataSets.CallSessions,
                stringLocalizer["Call sessions"],
                stringLocalizer["One row per voice call: its provider call, state, parties and timings."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var call = S["Call"].Value;
        var timeline = S["Timeline"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Call session ID"], ReportDataType.Text, record => record.ItemId, call, isIdentifier: true);
        AddField(nameof(CallSession.InteractionId), S["Interaction ID"], ReportDataType.Text, record => record.InteractionId, call, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Interactions));
        AddField(nameof(CallSession.ActivityItemId), S["Activity ID"], ReportDataType.Text, record => record.ActivityItemId, call, isIdentifier: true, ContactCenterReportDataSets.ActivityReference());
        AddField(nameof(CallSession.ProviderName), S["Provider"], ReportDataType.Text, record => record.ProviderName, call);
        AddField(nameof(CallSession.ProviderCallId), S["Provider call ID"], ReportDataType.Text, record => record.ProviderCallId, call, isIdentifier: true);
        AddField(nameof(CallSession.DeliveryModel), S["Delivery model"], ReportDataType.Text, record => record.DeliveryModel, call);
        AddField(nameof(CallSession.Direction), S["Direction"], ReportDataType.Text, record => record.Direction, call);
        AddField(nameof(CallSession.State), S["State"], ReportDataType.Text, record => record.State, call);
        AddField(nameof(CallSession.HangupCause), S["Hangup cause"], ReportDataType.Text, record => record.HangupCause, call);
        AddField(nameof(CallSession.AgentId), S["Agent ID"], ReportDataType.Text, record => record.AgentId, call, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentProfiles));
        AddField(nameof(CallSession.AgentSessionId), S["Agent session ID"], ReportDataType.Text, record => record.AgentSessionId, call, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentSessions));
        AddField(nameof(CallSession.QueueId), S["Queue ID"], ReportDataType.Text, record => record.QueueId, call, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField(nameof(CallSession.FromAddress), S["From"], ReportDataType.Text, record => record.FromAddress, call);
        AddField(nameof(CallSession.ToAddress), S["To"], ReportDataType.Text, record => record.ToAddress, call);
        AddField(nameof(CallSession.RecordingState), S["Recording state"], ReportDataType.Text, record => record.RecordingState, call);
        AddField(nameof(CallSession.IsOnHold), S["On hold"], ReportDataType.Boolean, record => record.IsOnHold, call);
        AddField(nameof(CallSession.IsMuted), S["Muted"], ReportDataType.Boolean, record => record.IsMuted, call);
        AddField("LegCount", S["Legs"], ReportDataType.Integer, record => record.Legs?.Count ?? 0, call);

        AddField(nameof(CallSession.CreatedUtc), S["Created"], ReportDataType.DateTime, record => record.CreatedUtc, timeline);
        AddField(nameof(CallSession.StartedUtc), S["Started"], ReportDataType.DateTime, record => record.StartedUtc, timeline);
        AddField(nameof(CallSession.AnsweredUtc), S["Answered"], ReportDataType.DateTime, record => record.AnsweredUtc, timeline);
        AddField(nameof(CallSession.EndedUtc), S["Ended"], ReportDataType.DateTime, record => record.EndedUtc, timeline);
        AddField(nameof(CallSession.ModifiedUtc), S["Modified"], ReportDataType.DateTime, record => record.ModifiedUtc, timeline);
        AddField(nameof(CallSession.TalkSeconds), S["Talk (seconds)"], ReportDataType.Decimal, record => record.TalkSeconds, timeline);
        AddField(nameof(CallSession.HoldSeconds), S["Hold (seconds)"], ReportDataType.Decimal, record => record.HoldSeconds, timeline);
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
    protected override (string Field, Expression<Func<CallSessionIndex, DateTime>> Column)? DateColumn
        => (nameof(CallSession.CreatedUtc), index => index.CreatedUtc);
}
