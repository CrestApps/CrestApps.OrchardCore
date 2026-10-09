using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;

/// <summary>
/// One row per outbound dialer profile, with how it dials and the compliance rules it applies.
/// </summary>
public sealed class DialerProfileReportDataSet : ContactCenterReportDataSet<DialerProfile, DialerProfileIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProfileReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session dialer profiles are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DialerProfileReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<DialerProfileReportDataSet> stringLocalizer)
        : base(
            new ReportDataSetDescriptor(
                ContactCenterReportDataSets.DialerProfiles,
                stringLocalizer["Dialer profiles"],
                stringLocalizer["One row per outbound dialer profile, with how it dials and the rules it applies."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var profile = S["Profile"].Value;
        var compliance = S["Compliance"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Dialer profile ID"], ReportDataType.Text, record => record.ItemId, profile, isIdentifier: true);
        AddField(nameof(DialerProfile.Name), S["Name"], ReportDataType.Text, record => record.Name, profile);
        AddField(nameof(DialerProfile.Description), S["Description"], ReportDataType.Text, record => record.Description, profile);
        AddField(nameof(DialerProfile.Mode), S["Mode"], ReportDataType.Text, record => record.Mode, profile);
        AddField(nameof(DialerProfile.ProviderName), S["Provider"], ReportDataType.Text, record => record.ProviderName, profile);
        AddField(nameof(DialerProfile.Enabled), S["Enabled"], ReportDataType.Boolean, record => record.Enabled, profile);
        AddField(nameof(DialerProfile.CallsPerAgent), S["Calls per agent"], ReportDataType.Integer, record => record.CallsPerAgent, profile);
        AddField(nameof(DialerProfile.MaxAttempts), S["Maximum attempts"], ReportDataType.Integer, record => record.MaxAttempts, profile);
        AddField(nameof(DialerProfile.RetryDelayMinutes), S["Retry delay (minutes)"], ReportDataType.Integer, record => record.RetryDelayMinutes, profile);
        AddField(nameof(DialerProfile.AnsweringMachineDetection), S["Answering machine detection"], ReportDataType.Text, record => record.AnsweringMachineDetection, profile);
        AddField(nameof(DialerProfile.RingTimeoutSeconds), S["Ring timeout (seconds)"], ReportDataType.Integer, record => record.RingTimeoutSeconds, profile);
        AddField(nameof(DialerProfile.PredictivePacingModel), S["Predictive pacing"], ReportDataType.Text, record => record.PredictivePacingModel, profile);
        AddField(nameof(DialerProfile.MaxLinesPerAgent), S["Maximum lines per agent"], ReportDataType.Decimal, record => record.MaxLinesPerAgent, profile);
        AddField(nameof(DialerProfile.MaxCallsInFlight), S["Maximum calls in flight"], ReportDataType.Integer, record => record.MaxCallsInFlight, profile);
        AddField(nameof(DialerProfile.CallerId), S["Caller ID"], ReportDataType.Text, record => record.CallerId, profile);
        AddField(nameof(DialerProfile.AlwaysUseCallerId), S["Always use caller ID"], ReportDataType.Boolean, record => record.AlwaysUseCallerId, profile);
        AddField(nameof(DialerProfile.DefaultRegionCode), S["Default region"], ReportDataType.Text, record => record.DefaultRegionCode, profile);
        AddField(nameof(DialerProfile.CallingCalendarId), S["Calling calendar ID"], ReportDataType.Text, record => record.CallingCalendarId, profile, isIdentifier: true);
        AddField(nameof(DialerProfile.CreatedUtc), S["Created"], ReportDataType.DateTime, record => record.CreatedUtc, profile);
        AddField(nameof(DialerProfile.ModifiedUtc), S["Modified"], ReportDataType.DateTime, record => record.ModifiedUtc, profile);

        AddField(nameof(DialerProfile.RespectDoNotCall), S["Respects do-not-call"], ReportDataType.Boolean, record => record.RespectDoNotCall, compliance);
        AddField(nameof(DialerProfile.EnforceCallingWindow), S["Enforces calling window"], ReportDataType.Boolean, record => record.EnforceCallingWindow, compliance);
        AddField(nameof(DialerProfile.EnforceAbandonmentCap), S["Enforces abandonment cap"], ReportDataType.Boolean, record => record.EnforceAbandonmentCap, compliance);
        AddField(nameof(DialerProfile.MaxAbandonmentRatePercent), S["Maximum abandonment rate (%)"], ReportDataType.Decimal, record => record.MaxAbandonmentRatePercent, compliance);
        AddField(nameof(DialerProfile.TargetAbandonmentRatePercent), S["Target abandonment rate (%)"], ReportDataType.Decimal, record => record.TargetAbandonmentRatePercent, compliance);
        AddField(nameof(DialerProfile.SafeHarborEnabled), S["Safe harbor message"], ReportDataType.Boolean, record => record.SafeHarborEnabled, compliance);
    }
}
