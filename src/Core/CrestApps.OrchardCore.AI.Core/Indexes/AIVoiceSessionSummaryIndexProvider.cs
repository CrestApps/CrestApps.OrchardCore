using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Options;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

/// <summary>
/// Maps <see cref="AIVoiceSessionSummary"/> documents to <see cref="AIVoiceSessionSummaryIndex"/> rows in the AI
/// collection, beside the completion usage records the same report reads.
/// </summary>
public sealed class AIVoiceSessionSummaryIndexProvider : IndexProvider<AIVoiceSessionSummary>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceSessionSummaryIndexProvider"/> class.
    /// </summary>
    /// <param name="options">The YesSql store options that name the AI collection.</param>
    public AIVoiceSessionSummaryIndexProvider(IOptions<YesSqlStoreOptions> options)
    {
        CollectionName = options.Value.AICollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AIVoiceSessionSummary> context)
    {
        context.For<AIVoiceSessionSummaryIndex>()
            .Map(summary => ToIndex(summary));
    }

    /// <summary>
    /// The index row for a summary.
    /// </summary>
    /// <param name="summary">The summary to index.</param>
    internal static AIVoiceSessionSummaryIndex ToIndex(AIVoiceSessionSummary summary)
        => new()
        {
            ItemId = summary.ItemId,
            ActivityId = summary.ActivityId,
            AISessionId = summary.AISessionId,
            AIProfileId = summary.AIProfileId,
            AIProfileName = Truncate(summary.AIProfileName, AIVoiceSessionSummaryIndexSchema.NameLength),
            CampaignId = summary.CampaignId,
            CampaignName = Truncate(summary.CampaignName, AIVoiceSessionSummaryIndexSchema.NameLength),
            Channel = summary.Channel,
            ChannelEndpointId = summary.ChannelEndpointId,
            Engine = summary.Engine.ToString(),
            Outcome = summary.Outcome.ToString(),
            DeploymentName = Truncate(summary.DeploymentName, AIVoiceSessionSummaryIndexSchema.NameLength),
            ModelName = Truncate(summary.ModelName, AIVoiceSessionSummaryIndexSchema.NameLength),
            ConnectionName = Truncate(summary.ConnectionName, AIVoiceSessionSummaryIndexSchema.NameLength),
            StartedUtc = summary.StartedUtc,
            EndedUtc = summary.EndedUtc,
            CreatedUtc = summary.CreatedUtc,
            SessionDurationMs = summary.SessionDurationMs,
            AssistantSpeakingMs = summary.AssistantSpeakingMs,
            CallerSpeakingMs = summary.CallerSpeakingMs,
            MutualSilenceMs = summary.MutualSilenceMs,
            TimeToFirstAssistantAudioMs = summary.TimeToFirstAssistantAudioMs,
            AssistantTurns = summary.AssistantTurns,
            CallerTurns = summary.CallerTurns,
            BargeIns = summary.BargeIns,
            IdlePrompts = summary.IdlePrompts,
            EchoHeldMs = summary.EchoHeldMs,
            InputAudioTokens = summary.InputAudioTokens,
            OutputAudioTokens = summary.OutputAudioTokens,
            InputTextTokens = summary.InputTextTokens,
            OutputTextTokens = summary.OutputTextTokens,
            CachedInputTokens = summary.CachedInputTokens,
        };

    private static string Truncate(string value, int length)
        => value is not null && value.Length > length ? value[..length] : value;
}
