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
            new DataSetDescriptor(
                ContactCenterReportDataSets.CallQuality,
                stringLocalizer["Call quality"],
                stringLocalizer["One row per measured call leg: its rating, mean opinion score, loss, jitter and round-trip time."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var leg = S["Call leg"].Value;
        var quality = S["Quality"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Measurement ID"], DataFieldType.Text, record => record.ItemId, leg).Identifier();
        AddField(nameof(CallQualityRecord.Source), S["Measured by"], DataFieldType.Text, record => record.Source, leg);
        AddField(nameof(CallQualityRecord.ProviderName), S["Provider"], DataFieldType.Text, record => record.ProviderName, leg);
        AddField(nameof(CallQualityRecord.InteractionId), S["Interaction ID"], DataFieldType.Text, record => record.InteractionId, leg).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Interactions));
        AddField(nameof(CallQualityRecord.CallSessionId), S["Call session ID"], DataFieldType.Text, record => record.CallSessionId, leg).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.CallSessions));
        AddField(nameof(CallQualityRecord.LegRole), S["Party"], DataFieldType.Text, record => record.LegRole, leg);
        AddField(nameof(CallQualityRecord.AgentId), S["Agent ID"], DataFieldType.Text, record => record.AgentId, leg).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentProfiles));
        AddField(nameof(CallQualityRecord.UserId), S["User ID"], DataFieldType.Text, record => record.UserId, leg).Identifier(ContactCenterReportDataSets.UserReference());
        AddField(nameof(CallQualityRecord.QueueId), S["Queue ID"], DataFieldType.Text, record => record.QueueId, leg).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField(nameof(CallQualityRecord.ObservedUtc), S["Observed"], DataFieldType.DateTime, record => record.ObservedUtc, leg);
        AddField(nameof(CallQualityRecord.CreatedUtc), S["Recorded"], DataFieldType.DateTime, record => record.CreatedUtc, leg);

        AddField(nameof(CallQualityRecord.Rating), S["Rating"], DataFieldType.Text, record => record.Rating, quality);
        AddField(nameof(CallQualityRecord.Mos), S["Mean opinion score"], DataFieldType.Decimal, record => record.Mos, quality);
        AddField(nameof(CallQualityRecord.LossPercent), S["Packet loss (%)"], DataFieldType.Decimal, record => record.LossPercent, quality);
        AddField(nameof(CallQualityRecord.JitterMs), S["Jitter (ms)"], DataFieldType.Decimal, record => record.JitterMs, quality);
        AddField(nameof(CallQualityRecord.RoundTripMs), S["Round trip (ms)"], DataFieldType.Decimal, record => record.RoundTripMs, quality);
        AddField(nameof(CallQualityRecord.DurationSeconds), S["Measured (seconds)"], DataFieldType.Decimal, record => record.DurationSeconds, quality);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<CallQualityRecordIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<CallQualityRecordIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["InteractionId"] = index => index.InteractionId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<CallQualityRecordIndex> columns)
    {
        columns
            .Enum(nameof(CallQualityRecord.Source), index => index.Source)
            .Enum(nameof(CallQualityRecord.Rating), index => index.Rating);
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<CallQualityRecordIndex, DateTime>> Column)? DateColumn
        => (nameof(CallQualityRecord.ObservedUtc), index => index.ObservedUtc);
}
