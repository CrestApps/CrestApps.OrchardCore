using System.Linq.Expressions;
using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// The <c>VoiceUsage</c> data set: one row per AI voice session summary, the record an automated AI voice call leaves
/// when the assistant's part of it ends, with its talk time, silence, turns, outcome, model and audio tokens. The AI
/// Chat Session Analytics feature records the summaries while AI usage tracking is on. Only principals allowed to view
/// AI chat analytics read it, as on the AI Usage Analytics page. The provider's call identifier is never exposed.
/// </summary>
/// <remarks>
/// The index keeps at most <see cref="AIVoiceSessionSummaryIndexSchema.NameLength"/> characters of each name, so the
/// name fields read the same, and the database groups by them exactly as the rows do.
/// </remarks>
public sealed class AIVoiceUsageReportDataSet : IndexedReportDataSet<AIVoiceSessionSummary, AIVoiceSessionSummaryIndex>, IAIChatReportDataSet
{
    /// <summary>
    /// The technical name of the field that holds when the summary was written, the data set's main date.
    /// </summary>
    public const string CreatedUtcField = "CreatedUtc";

    private const decimal MillisecondsPerSecond = 1_000m;
    private const decimal MillisecondsPerMinute = 60_000m;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceUsageReportDataSet"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the summaries are read from.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="storeOptions">The YesSql store options, which name the AI collection.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIVoiceUsageReportDataSet(
        ISession session,
        IAuthorizationService authorizationService,
        IOptions<YesSqlStoreOptions> storeOptions,
        IStringLocalizer<AIVoiceUsageReportDataSet> stringLocalizer)
        : base(
            new DataSetDescriptor(
                AIChatReportDataSource.VoiceUsageDataSet,
                stringLocalizer["AI voice usage"],
                stringLocalizer["One row per automated AI voice call summary: how long the call lasted, how long each side spoke, its turns and outcome, the profile, campaign, model and deployment that held it, and its audio and text tokens."]),
            session,
            authorizationService,
            storeOptions.Value.AICollectionName,
            ChatAnalyticsPermissionProvider.ViewChatAnalytics)
    {
        var S = stringLocalizer;
        var call = S["Call"].Value;
        var model = S["Model"].Value;
        var time = S["Talk time"].Value;
        var turns = S["Turns"].Value;
        var tokens = S["Tokens"].Value;
        var unknown = S["Empty when the engine that held the call can't measure it."].Value;

        AddField(nameof(AIVoiceSessionSummary.ItemId), S["Summary ID"], DataFieldType.Text, record => record.ItemId, call).Identifier();
        AddField(nameof(AIVoiceSessionSummary.ActivityId), S["Activity ID"], DataFieldType.Text, record => record.ActivityId, call).Identifier()
            .Description = S["The activity of the call. Count the distinct activities to count calls."].Value;
        AddField(nameof(AIVoiceSessionSummary.AISessionId), S["Chat session ID"], DataFieldType.Text, record => record.AISessionId, call)
            .Identifier(new DataFieldReference(AIChatReportDataSource.SourceName, AIChatReportDataSource.ChatSessionsDataSet, AIChatReportDataSource.SessionIdField));
        AddField(nameof(AIVoiceSessionSummary.Channel), S["Channel"], DataFieldType.Text, record => record.Channel, call);
        AddField(nameof(AIVoiceSessionSummary.ChannelEndpointId), S["Channel endpoint ID"], DataFieldType.Text, record => record.ChannelEndpointId, call).Identifier();
        AddField(nameof(AIVoiceSessionSummary.ProviderName), S["Telephony provider"], DataFieldType.Text, record => record.ProviderName, call);
        AddField(nameof(AIVoiceSessionSummary.CampaignId), S["Campaign ID"], DataFieldType.Text, record => record.CampaignId, call).Identifier();
        AddField(nameof(AIVoiceSessionSummary.CampaignName), S["Campaign"], DataFieldType.Text, record => Name(record.CampaignName), call);
        AddField(nameof(AIVoiceSessionSummary.Engine), S["Engine"], DataFieldType.Text, record => record.Engine, call)
            .Description = S["Realtime (speech-to-speech) or TurnBased."].Value;
        AddField(nameof(AIVoiceSessionSummary.Outcome), S["Outcome"], DataFieldType.Text, record => record.Outcome, call);
        AddField(nameof(AIVoiceSessionSummary.StartedUtc), S["Started"], DataFieldType.DateTime, record => record.StartedUtc, call);
        AddField(nameof(AIVoiceSessionSummary.EndedUtc), S["Ended"], DataFieldType.DateTime, record => record.EndedUtc, call);
        AddField(CreatedUtcField, S["Recorded"], DataFieldType.DateTime, record => record.CreatedUtc, call)
            .Description = S["When the summary was written: when the assistant's part of the call ended."].Value;

        AddField(nameof(AIVoiceSessionSummary.AIProfileId), S["Profile ID"], DataFieldType.Text, record => record.AIProfileId, model).Identifier();
        AddField(nameof(AIVoiceSessionSummary.AIProfileName), S["Profile"], DataFieldType.Text, record => Name(record.AIProfileName), model)
            .Description = S["The profile's name when the call ended."].Value;
        AddField(nameof(AIVoiceSessionSummary.DeploymentName), S["Deployment"], DataFieldType.Text, record => Name(record.DeploymentName), model);
        AddField(nameof(AIVoiceSessionSummary.ModelName), S["Model"], DataFieldType.Text, record => Name(record.ModelName), model);
        AddField(nameof(AIVoiceSessionSummary.ConnectionName), S["Connection"], DataFieldType.Text, record => Name(record.ConnectionName), model);

        AddField(nameof(AIVoiceSessionSummary.SessionDurationMs), S["Session duration (ms)"], DataFieldType.Integer, record => record.SessionDurationMs, time).Description = unknown;
        AddField("SessionSeconds", S["Session duration (seconds)"], DataFieldType.Decimal, record => record.SessionDurationMs / MillisecondsPerSecond, time).Description = unknown;
        AddField("SessionMinutes", S["Session duration (minutes)"], DataFieldType.Decimal, record => record.SessionDurationMs / MillisecondsPerMinute, time).Description = unknown;
        AddField(nameof(AIVoiceSessionSummary.AssistantSpeakingMs), S["AI speaking (ms)"], DataFieldType.Integer, record => record.AssistantSpeakingMs, time).Description = unknown;
        AddField("AIMinutes", S["AI minutes"], DataFieldType.Decimal, record => record.AssistantSpeakingMs / MillisecondsPerMinute, time)
            .Description = S["Minutes the caller heard the assistant speak."].Value;
        AddField(nameof(AIVoiceSessionSummary.CallerSpeakingMs), S["Caller speaking (ms)"], DataFieldType.Integer, record => record.CallerSpeakingMs, time).Description = unknown;
        AddField("CallerMinutes", S["Caller minutes"], DataFieldType.Decimal, record => record.CallerSpeakingMs / MillisecondsPerMinute, time).Description = unknown;
        AddField(nameof(AIVoiceSessionSummary.MutualSilenceMs), S["Silence (ms)"], DataFieldType.Integer, record => record.MutualSilenceMs, time).Description = unknown;
        AddField("SilenceMinutes", S["Silence minutes"], DataFieldType.Decimal, record => record.MutualSilenceMs / MillisecondsPerMinute, time).Description = unknown;
        AddField(nameof(AIVoiceSessionSummary.TimeToFirstAssistantAudioMs), S["Time to first AI audio (ms)"], DataFieldType.Integer, record => record.TimeToFirstAssistantAudioMs, time).Description = unknown;
        AddField(nameof(AIVoiceSessionSummary.EchoHeldMs), S["Echo held (ms)"], DataFieldType.Integer, record => record.EchoHeldMs, time).Description = unknown;

        AddField(nameof(AIVoiceSessionSummary.AssistantTurns), S["AI turns"], DataFieldType.Integer, record => record.AssistantTurns, turns);
        AddField(nameof(AIVoiceSessionSummary.CallerTurns), S["Caller turns"], DataFieldType.Integer, record => record.CallerTurns, turns);
        AddField(nameof(AIVoiceSessionSummary.BargeIns), S["Barge-ins"], DataFieldType.Integer, record => record.BargeIns, turns).Description = unknown;
        AddField(nameof(AIVoiceSessionSummary.IdlePrompts), S["Idle prompts"], DataFieldType.Integer, record => record.IdlePrompts, turns);

        AddField(nameof(AIVoiceSessionSummary.InputAudioTokens), S["Audio input tokens"], DataFieldType.Integer, record => record.InputAudioTokens, tokens);
        AddField(nameof(AIVoiceSessionSummary.OutputAudioTokens), S["Audio output tokens"], DataFieldType.Integer, record => record.OutputAudioTokens, tokens);
        AddField(nameof(AIVoiceSessionSummary.InputTextTokens), S["Text input tokens"], DataFieldType.Integer, record => record.InputTextTokens, tokens);
        AddField(nameof(AIVoiceSessionSummary.OutputTextTokens), S["Text output tokens"], DataFieldType.Integer, record => record.OutputTextTokens, tokens);
        AddField(nameof(AIVoiceSessionSummary.CachedInputTokens), S["Cached input tokens"], DataFieldType.Integer, record => record.CachedInputTokens, tokens);

        CompleteDescription();
    }

    /// <inheritdoc/>
    protected override (string Field, Expression<Func<AIVoiceSessionSummaryIndex, DateTime>> Column)? DateColumn
        => (CreatedUtcField, index => index.CreatedUtc);

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, Expression<Func<AIVoiceSessionSummaryIndex, string>>> KeyColumns =>
        new Dictionary<string, Expression<Func<AIVoiceSessionSummaryIndex, string>>>(StringComparer.Ordinal)
        {
            [nameof(AIVoiceSessionSummary.ItemId)] = index => index.ItemId,
            [nameof(AIVoiceSessionSummary.ActivityId)] = index => index.ActivityId,
            [nameof(AIVoiceSessionSummary.AISessionId)] = index => index.AISessionId,
            [nameof(AIVoiceSessionSummary.AIProfileId)] = index => index.AIProfileId,
            [nameof(AIVoiceSessionSummary.CampaignId)] = index => index.CampaignId,
        };

    /// <inheritdoc/>
    protected override void DescribeColumns(ReportIndexConditions<AIVoiceSessionSummaryIndex> columns)
    {
        columns
            .Text(nameof(AIVoiceSessionSummary.Channel), nameof(AIVoiceSessionSummaryIndex.Channel))
            .Text(nameof(AIVoiceSessionSummary.Engine), nameof(AIVoiceSessionSummaryIndex.Engine))
            .Text(nameof(AIVoiceSessionSummary.Outcome), nameof(AIVoiceSessionSummaryIndex.Outcome))
            .Text(nameof(AIVoiceSessionSummary.DeploymentName), nameof(AIVoiceSessionSummaryIndex.DeploymentName))
            .Text(nameof(AIVoiceSessionSummary.ModelName), nameof(AIVoiceSessionSummaryIndex.ModelName));
    }

    // The index keeps the first characters of a name only, so the field reads what the index holds.
    private static string Name(string value)
    {
        return value is not null && value.Length > AIVoiceSessionSummaryIndexSchema.NameLength
            ? value.Substring(0, AIVoiceSessionSummaryIndexSchema.NameLength)
            : value;
    }
}
