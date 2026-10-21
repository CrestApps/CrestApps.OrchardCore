using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
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
            new DataSetDescriptor(
                ContactCenterReportDataSets.DialerProfiles,
                stringLocalizer["Dialer profiles"],
                stringLocalizer["One row per outbound dialer profile, with how it dials and the rules it applies."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var profile = S["Profile"].Value;
        var compliance = S["Compliance"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Dialer profile ID"], DataFieldType.Text, record => record.ItemId, profile).Identifier();
        AddField(nameof(DialerProfile.Name), S["Name"], DataFieldType.Text, record => record.Name, profile);
        AddField(nameof(DialerProfile.Description), S["Description"], DataFieldType.Text, record => record.Description, profile);
        AddField(nameof(DialerProfile.Mode), S["Mode"], DataFieldType.Text, record => record.Mode, profile);
        AddField(nameof(DialerProfile.ProviderName), S["Provider"], DataFieldType.Text, record => record.ProviderName, profile);
        AddField(nameof(DialerProfile.Enabled), S["Enabled"], DataFieldType.Boolean, record => record.Enabled, profile);
        AddField(nameof(DialerProfile.CallsPerAgent), S["Calls per agent"], DataFieldType.Integer, record => record.CallsPerAgent, profile);
        AddField(nameof(DialerProfile.MaxAttempts), S["Maximum attempts"], DataFieldType.Integer, record => record.MaxAttempts, profile);
        AddField(nameof(DialerProfile.RetryDelayMinutes), S["Retry delay (minutes)"], DataFieldType.Integer, record => record.RetryDelayMinutes, profile);
        AddField(nameof(DialerProfile.AnsweringMachineDetection), S["Answering machine detection"], DataFieldType.Text, record => record.AnsweringMachineDetection, profile);
        AddField(nameof(DialerProfile.RingTimeoutSeconds), S["Ring timeout (seconds)"], DataFieldType.Integer, record => record.RingTimeoutSeconds, profile);
        AddField(nameof(DialerProfile.PredictivePacingModel), S["Predictive pacing"], DataFieldType.Text, record => record.PredictivePacingModel, profile);
        AddField(nameof(DialerProfile.MaxLinesPerAgent), S["Maximum lines per agent"], DataFieldType.Decimal, record => record.MaxLinesPerAgent, profile);
        AddField(nameof(DialerProfile.MaxCallsInFlight), S["Maximum calls in flight"], DataFieldType.Integer, record => record.MaxCallsInFlight, profile);
        AddField(nameof(DialerProfile.CallerId), S["Caller ID"], DataFieldType.Text, record => record.CallerId, profile);
        AddField(nameof(DialerProfile.AlwaysUseCallerId), S["Always use caller ID"], DataFieldType.Boolean, record => record.AlwaysUseCallerId, profile);
        AddField(nameof(DialerProfile.DefaultRegionCode), S["Default region"], DataFieldType.Text, record => record.DefaultRegionCode, profile);
        AddField(nameof(DialerProfile.CallingCalendarId), S["Calling calendar ID"], DataFieldType.Text, record => record.CallingCalendarId, profile).Identifier();
        AddField(nameof(DialerProfile.CreatedUtc), S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, profile);
        AddField(nameof(DialerProfile.ModifiedUtc), S["Modified"], DataFieldType.DateTime, record => record.ModifiedUtc, profile);

        AddField(nameof(DialerProfile.RespectDoNotCall), S["Respects do-not-call"], DataFieldType.Boolean, record => record.RespectDoNotCall, compliance);
        AddField(nameof(DialerProfile.EnforceCallingWindow), S["Enforces calling window"], DataFieldType.Boolean, record => record.EnforceCallingWindow, compliance);
        AddField(nameof(DialerProfile.EnforceAbandonmentCap), S["Enforces abandonment cap"], DataFieldType.Boolean, record => record.EnforceAbandonmentCap, compliance);
        AddField(nameof(DialerProfile.MaxAbandonmentRatePercent), S["Maximum abandonment rate (%)"], DataFieldType.Decimal, record => record.MaxAbandonmentRatePercent, compliance);
        AddField(nameof(DialerProfile.TargetAbandonmentRatePercent), S["Target abandonment rate (%)"], DataFieldType.Decimal, record => record.TargetAbandonmentRatePercent, compliance);
        AddField(nameof(DialerProfile.SafeHarborEnabled), S["Safe harbor message"], DataFieldType.Boolean, record => record.SafeHarborEnabled, compliance);

        CompleteDescription();
    }
}
