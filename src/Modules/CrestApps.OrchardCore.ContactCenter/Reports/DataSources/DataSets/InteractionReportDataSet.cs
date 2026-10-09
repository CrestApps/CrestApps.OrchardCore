using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;

/// <summary>
/// One row per interaction: a communication attempt on a channel, with its routing, its timeline, the times derived
/// from it, and what the outbound dialer recorded about it.
/// </summary>
public sealed class InteractionReportDataSet : ContactCenterReportDataSet<Interaction, InteractionIndex>
{
    /// <summary>
    /// The prefix of the fields the outbound dialer records on an interaction.
    /// </summary>
    public const string DialerPrefix = "Dialer.";

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session interactions are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InteractionReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IStringLocalizer<InteractionReportDataSet> stringLocalizer)
        : base(
            new ReportDataSetDescriptor(
                ContactCenterReportDataSets.Interactions,
                stringLocalizer["Interactions"],
                stringLocalizer["One row per interaction: a call, message, or other communication attempt, with its routing and timeline."]),
            session,
            authorizationService)
    {
        var S = stringLocalizer;
        var interaction = S["Interaction"].Value;
        var timeline = S["Timeline"].Value;
        var dialer = S["Dialer"].Value;

        AddField(ContactCenterReportDataSets.ItemIdField, S["Interaction ID"], ReportDataType.Text, record => record.ItemId, interaction, isIdentifier: true);
        AddField(nameof(Interaction.Channel), S["Channel"], ReportDataType.Text, record => record.Channel, interaction);
        AddField(nameof(Interaction.Direction), S["Direction"], ReportDataType.Text, record => record.Direction, interaction);
        AddField(nameof(Interaction.Status), S["Status"], ReportDataType.Text, record => record.Status, interaction);
        AddField(nameof(Interaction.ActivityItemId), S["Activity ID"], ReportDataType.Text, record => record.ActivityItemId, interaction, isIdentifier: true, ContactCenterReportDataSets.ActivityReference());
        AddField(nameof(Interaction.ProviderName), S["Provider"], ReportDataType.Text, record => record.ProviderName, interaction);
        AddField(nameof(Interaction.CustomerAddress), S["Customer address"], ReportDataType.Text, record => record.CustomerAddress, interaction);
        AddField(nameof(Interaction.QueueId), S["Queue ID"], ReportDataType.Text, record => record.QueueId, interaction, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField(nameof(Interaction.AgentId), S["Agent ID"], ReportDataType.Text, record => record.AgentId, interaction, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentProfiles));
        AddField(nameof(Interaction.CorrelationId), S["Correlation ID"], ReportDataType.Text, record => record.CorrelationId, interaction, isIdentifier: true);
        AddField(nameof(Interaction.RecordingState), S["Recording state"], ReportDataType.Text, record => record.RecordingState, interaction);
        AddField(nameof(Interaction.RecordingLegalHold), S["Recording on legal hold"], ReportDataType.Boolean, record => record.RecordingLegalHold, interaction);
        AddField(nameof(Interaction.HandoffReason), S["Handoff reason"], ReportDataType.Text, record => record.HandoffReason, interaction);
        AddField("QueueHistoryCount", S["Queue history entries"], ReportDataType.Integer, record => record.QueueHistory?.Count ?? 0, interaction);
        AddField("TransferCount", S["Transfers"], ReportDataType.Integer, record => record.TransferHistory?.Count ?? 0, interaction);
        AddField(nameof(Interaction.CreatedById), S["Created by user ID"], ReportDataType.Text, record => record.CreatedById, interaction, isIdentifier: true, ContactCenterReportDataSets.UserReference());
        AddField(nameof(Interaction.CreatedByUserName), S["Created by"], ReportDataType.Text, record => record.CreatedByUserName, interaction);

        AddField(nameof(Interaction.CreatedUtc), S["Created"], ReportDataType.DateTime, record => record.CreatedUtc, timeline);
        AddField(nameof(Interaction.StartedUtc), S["Started"], ReportDataType.DateTime, record => record.StartedUtc, timeline);
        AddField(nameof(Interaction.AnsweredUtc), S["Answered"], ReportDataType.DateTime, record => record.AnsweredUtc, timeline);
        AddField(nameof(Interaction.EndedUtc), S["Ended"], ReportDataType.DateTime, record => record.EndedUtc, timeline);
        AddField(nameof(Interaction.WrapUpStartedUtc), S["Wrap-up started"], ReportDataType.DateTime, record => record.WrapUpStartedUtc, timeline);
        AddField(nameof(Interaction.WrapUpCompletedUtc), S["Wrap-up completed"], ReportDataType.DateTime, record => record.WrapUpCompletedUtc, timeline);
        AddField(nameof(Interaction.ModifiedUtc), S["Modified"], ReportDataType.DateTime, record => record.ModifiedUtc, timeline);

        // The same measures the Contact Center reports use, left empty when the interaction never reached the point
        // they are measured from, so an average counts only the interactions that have one.
        AddField("WaitSeconds", S["Wait (seconds)"], ReportDataType.Decimal, record => SecondsBetween(record.CreatedUtc, record.AnsweredUtc), timeline);
        AddField("TalkSeconds", S["Talk (seconds)"], ReportDataType.Decimal, record => SecondsBetween(record.AnsweredUtc, record.EndedUtc), timeline);
        AddField("WrapUpSeconds", S["Wrap-up (seconds)"], ReportDataType.Decimal, record => SecondsBetween(record.WrapUpStartedUtc, record.WrapUpCompletedUtc), timeline);

        AddField(DialerPrefix + "ProfileId", S["Dialer profile ID"], ReportDataType.Text, DialerCallMetadata.GetDialerProfileId, dialer, isIdentifier: true, ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.DialerProfiles));
        AddField(DialerPrefix + "IsCampaignDial", S["Campaign dial"], ReportDataType.Boolean, record => DialerCallMetadata.IsCampaignDial(record), dialer);
        AddField(DialerPrefix + "AttemptNumber", S["Dial attempt"], ReportDataType.Integer, record => DialerCallMetadata.GetAttemptNumber(record), dialer);
        AddField(DialerPrefix + "MaxAttempts", S["Dial attempts allowed"], ReportDataType.Integer, record => DialerCallMetadata.GetMaxAttempts(record), dialer);
        AddField(DialerPrefix + "Outcome", S["Dial outcome"], ReportDataType.Text, DialerCallMetadata.GetOutcome, dialer);
        AddField(DialerPrefix + "AnswerClassification", S["Answered by (machine detection)"], ReportDataType.Text, record => Metadata(record, ContactCenterConstants.TelephonyMetadata.AnswerClassification), dialer);
        AddField(DialerPrefix + "AbandonedReason", S["Abandoned reason"], ReportDataType.Text, DialerCallMetadata.GetAbandonedReason, dialer);
        AddField(DialerPrefix + "PacingModel", S["Pacing"], ReportDataType.Text, record => Metadata(record, DialerCallMetadata.PacingModelKey), dialer);
        AddField(DialerPrefix + "LiveAnsweredUtc", S["Person answered"], ReportDataType.DateTime, record => DialerCallMetadata.GetLiveAnsweredUtc(record), dialer);
        AddField(DialerPrefix + "AgentJoinedUtc", S["Agent joined"], ReportDataType.DateTime, record => DialerCallMetadata.GetAgentJoinedUtc(record), dialer);
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<InteractionIndex, DateTime>> Column)? DateColumn
        => (nameof(Interaction.CreatedUtc), index => index.CreatedUtc);

    // Technical metadata values are kept as plain strings; a value the store hands back as JSON reads as its text.
    private static string Metadata(Interaction interaction, string key)
    {
        return interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(key, out var value) &&
            value?.ToString() is { Length: > 0 } text
            ? text
            : null;
    }
}
