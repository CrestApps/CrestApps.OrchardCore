using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;

/// <summary>
/// One row per call quality measurement of a call leg: who was on it and how the audio held up.
/// </summary>
public sealed class CallQualityReportDataSet : ContactCenterReportDataSet<CallQualityRecord, CallQualityRecordIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallQualityReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session measurements are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CallQualityReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<CallQualityReportDataSet> stringLocalizer)
        : base(
            new ReportDataSetDescriptor(
                ContactCenterReportDataSets.CallQuality,
                stringLocalizer["Call quality"],
                stringLocalizer["One row per measured call leg: its rating, mean opinion score, loss, jitter and round-trip time."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var leg = S["Call leg"].Value;
        var quality = S["Quality"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Measurement ID"], ReportDataType.Text, record => record.ItemId, leg, isIdentifier: true);
        AddField(nameof(CallQualityRecord.Source), S["Measured by"], ReportDataType.Text, record => record.Source, leg);
        AddField(nameof(CallQualityRecord.ProviderName), S["Provider"], ReportDataType.Text, record => record.ProviderName, leg);
        AddField(nameof(CallQualityRecord.InteractionId), S["Interaction ID"], ReportDataType.Text, record => record.InteractionId, leg, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Interactions));
        AddField(nameof(CallQualityRecord.CallSessionId), S["Call session ID"], ReportDataType.Text, record => record.CallSessionId, leg, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.CallSessions));
        AddField(nameof(CallQualityRecord.LegRole), S["Party"], ReportDataType.Text, record => record.LegRole, leg);
        AddField(nameof(CallQualityRecord.AgentId), S["Agent ID"], ReportDataType.Text, record => record.AgentId, leg, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentProfiles));
        AddField(nameof(CallQualityRecord.UserId), S["User ID"], ReportDataType.Text, record => record.UserId, leg, isIdentifier: true, ContactCenterReportDataSets.UserReference());
        AddField(nameof(CallQualityRecord.QueueId), S["Queue ID"], ReportDataType.Text, record => record.QueueId, leg, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField(nameof(CallQualityRecord.ObservedUtc), S["Observed"], ReportDataType.DateTime, record => record.ObservedUtc, leg);
        AddField(nameof(CallQualityRecord.CreatedUtc), S["Recorded"], ReportDataType.DateTime, record => record.CreatedUtc, leg);

        AddField(nameof(CallQualityRecord.Rating), S["Rating"], ReportDataType.Text, record => record.Rating, quality);
        AddField(nameof(CallQualityRecord.Mos), S["Mean opinion score"], ReportDataType.Decimal, record => record.Mos, quality);
        AddField(nameof(CallQualityRecord.LossPercent), S["Packet loss (%)"], ReportDataType.Decimal, record => record.LossPercent, quality);
        AddField(nameof(CallQualityRecord.JitterMs), S["Jitter (ms)"], ReportDataType.Decimal, record => record.JitterMs, quality);
        AddField(nameof(CallQualityRecord.RoundTripMs), S["Round trip (ms)"], ReportDataType.Decimal, record => record.RoundTripMs, quality);
        AddField(nameof(CallQualityRecord.DurationSeconds), S["Measured (seconds)"], ReportDataType.Decimal, record => record.DurationSeconds, quality);
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<CallQualityRecordIndex, DateTime>> Column)? DateColumn
        => (nameof(CallQualityRecord.ObservedUtc), index => index.ObservedUtc);
}
