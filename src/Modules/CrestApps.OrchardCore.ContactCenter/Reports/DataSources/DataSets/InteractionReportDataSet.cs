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
            new DataSetDescriptor(
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

        AddField(ContactCenterReportDataSets.ItemIdField, S["Interaction ID"], DataFieldType.Text, record => record.ItemId, interaction).Identifier();
        AddField(nameof(Interaction.Channel), S["Channel"], DataFieldType.Text, record => record.Channel, interaction);
        AddField(nameof(Interaction.Direction), S["Direction"], DataFieldType.Text, record => record.Direction, interaction);
        AddField(nameof(Interaction.Status), S["Status"], DataFieldType.Text, record => record.Status, interaction);
        AddField(nameof(Interaction.ActivityItemId), S["Activity ID"], DataFieldType.Text, record => record.ActivityItemId, interaction).Identifier(ContactCenterReportDataSets.ActivityReference());
        AddField(nameof(Interaction.ProviderName), S["Provider"], DataFieldType.Text, record => record.ProviderName, interaction);
        AddField(nameof(Interaction.CustomerAddress), S["Customer address"], DataFieldType.Text, record => record.CustomerAddress, interaction);
        AddField(nameof(Interaction.QueueId), S["Queue ID"], DataFieldType.Text, record => record.QueueId, interaction).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.Queues));
        AddField(nameof(Interaction.AgentId), S["Agent ID"], DataFieldType.Text, record => record.AgentId, interaction).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.AgentProfiles));
        AddField(nameof(Interaction.CorrelationId), S["Correlation ID"], DataFieldType.Text, record => record.CorrelationId, interaction).Identifier();
        AddField(nameof(Interaction.RecordingState), S["Recording state"], DataFieldType.Text, record => record.RecordingState, interaction);
        AddField(nameof(Interaction.RecordingLegalHold), S["Recording on legal hold"], DataFieldType.Boolean, record => record.RecordingLegalHold, interaction);
        AddField(nameof(Interaction.HandoffReason), S["Handoff reason"], DataFieldType.Text, record => record.HandoffReason, interaction);
        AddField(nameof(Interaction.HandoffAiSessionId), S["Handoff AI chat session ID"], DataFieldType.Text, record => record.HandoffAiSessionId, interaction).Identifier(ContactCenterReportDataSets.ChatSessionReference());
        AddField("TransferCount", S["Transfers"], DataFieldType.Integer, record => record.TransferHistory?.Count ?? 0, interaction);
        AddField(nameof(Interaction.CreatedById), S["Created by user ID"], DataFieldType.Text, record => record.CreatedById, interaction).Identifier(ContactCenterReportDataSets.UserReference());
        AddField(nameof(Interaction.CreatedByUserName), S["Created by"], DataFieldType.Text, record => record.CreatedByUserName, interaction);

        AddField(nameof(Interaction.CreatedUtc), S["Created"], DataFieldType.DateTime, record => record.CreatedUtc, timeline);
        AddField(nameof(Interaction.StartedUtc), S["Started"], DataFieldType.DateTime, record => record.StartedUtc, timeline);
        AddField(nameof(Interaction.AnsweredUtc), S["Answered"], DataFieldType.DateTime, record => record.AnsweredUtc, timeline);
        AddField(nameof(Interaction.EndedUtc), S["Ended"], DataFieldType.DateTime, record => record.EndedUtc, timeline);
        AddField(nameof(Interaction.WrapUpStartedUtc), S["Wrap-up started"], DataFieldType.DateTime, record => record.WrapUpStartedUtc, timeline);
        AddField(nameof(Interaction.WrapUpCompletedUtc), S["Wrap-up completed"], DataFieldType.DateTime, record => record.WrapUpCompletedUtc, timeline);
        AddField(nameof(Interaction.ModifiedUtc), S["Modified"], DataFieldType.DateTime, record => record.ModifiedUtc, timeline);

        // The same measures the Contact Center reports use, left empty when the interaction never reached the point
        // they are measured from, so an average counts only the interactions that have one.
        AddField("WaitSeconds", S["Wait (seconds)"], DataFieldType.Decimal, record => SecondsBetween(record.CreatedUtc, record.AnsweredUtc), timeline);
        AddField("TalkSeconds", S["Talk (seconds)"], DataFieldType.Decimal, record => SecondsBetween(record.AnsweredUtc, record.EndedUtc), timeline);
        AddField("WrapUpSeconds", S["Wrap-up (seconds)"], DataFieldType.Decimal, record => SecondsBetween(record.WrapUpStartedUtc, record.WrapUpCompletedUtc), timeline);

        AddField(DialerPrefix + "ProfileId", S["Dialer profile ID"], DataFieldType.Text, DialerCallMetadata.GetDialerProfileId, dialer).Identifier(ContactCenterReportDataSets.Reference(ContactCenterReportDataSets.DialerProfiles));
        AddField(DialerPrefix + "IsCampaignDial", S["Campaign dial"], DataFieldType.Boolean, record => DialerCallMetadata.IsCampaignDial(record), dialer);
        AddField(DialerPrefix + "AttemptNumber", S["Dial attempt"], DataFieldType.Integer, record => DialerCallMetadata.GetAttemptNumber(record), dialer);
        AddField(DialerPrefix + "MaxAttempts", S["Dial attempts allowed"], DataFieldType.Integer, record => DialerCallMetadata.GetMaxAttempts(record), dialer);
        AddField(DialerPrefix + "Outcome", S["Dial outcome"], DataFieldType.Text, DialerCallMetadata.GetOutcome, dialer);
        AddField(DialerPrefix + "AnswerClassification", S["Answered by (machine detection)"], DataFieldType.Text, record => Metadata(record, ContactCenterConstants.TelephonyMetadata.AnswerClassification), dialer);
        AddField(DialerPrefix + "AbandonedReason", S["Abandoned reason"], DataFieldType.Text, DialerCallMetadata.GetAbandonedReason, dialer);
        AddField(DialerPrefix + "PacingModel", S["Pacing"], DataFieldType.Text, record => Metadata(record, DialerCallMetadata.PacingModelKey), dialer);
        AddField(DialerPrefix + "LiveAnsweredUtc", S["Person answered"], DataFieldType.DateTime, record => DialerCallMetadata.GetLiveAnsweredUtc(record), dialer);
        AddField(DialerPrefix + "AgentJoinedUtc", S["Agent joined"], DataFieldType.DateTime, record => DialerCallMetadata.GetAgentJoinedUtc(record), dialer);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<InteractionIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<InteractionIndex, string>>>(StringComparer.Ordinal)
        {
            ["ItemId"] = index => index.ItemId,
            ["ActivityItemId"] = index => index.ActivityItemId,
            ["QueueId"] = index => index.QueueId,
            ["AgentId"] = index => index.AgentId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<InteractionIndex> columns)
    {
        columns
            .Enum(nameof(Interaction.Channel), index => index.Channel)
            .Enum(nameof(Interaction.Direction), index => index.Direction)
            .Enum(nameof(Interaction.Status), index => index.Status)
            .Enum(nameof(Interaction.RecordingState), index => index.RecordingState)
            .Text(nameof(Interaction.ProviderName), nameof(InteractionIndex.ProviderName));
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
