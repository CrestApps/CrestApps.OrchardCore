using CrestApps.OrchardCore.AI.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

/// <summary>
/// The reportable columns of an <see cref="AIVoiceSessionSummary"/>, so the usage report aggregates index rows
/// rather than loading and deserializing every call's document.
/// </summary>
public sealed class AIVoiceSessionSummaryIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the summary identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the activity the call belongs to.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the AI chat session of the call.
    /// </summary>
    public string AISessionId { get; set; }

    /// <summary>
    /// Gets or sets the AI profile that held the call.
    /// </summary>
    public string AIProfileId { get; set; }

    /// <summary>
    /// Gets or sets the AI profile name when the call ended.
    /// </summary>
    public string AIProfileName { get; set; }

    /// <summary>
    /// Gets or sets the campaign the call was placed for.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the campaign name when the call ended.
    /// </summary>
    public string CampaignName { get; set; }

    /// <summary>
    /// Gets or sets the channel the call was carried on.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the channel endpoint the call was carried on.
    /// </summary>
    public string ChannelEndpointId { get; set; }

    /// <summary>
    /// Gets or sets the engine, by name.
    /// </summary>
    public string Engine { get; set; }

    /// <summary>
    /// Gets or sets the outcome, by name.
    /// </summary>
    public string Outcome { get; set; }

    /// <summary>
    /// Gets or sets the deployment that held the call.
    /// </summary>
    public string DeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the model behind the deployment.
    /// </summary>
    public string ModelName { get; set; }

    /// <summary>
    /// Gets or sets the connection behind the deployment.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets when the assistant took the answered call.
    /// </summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the assistant's part of the call ended.
    /// </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the summary was written. The report's date range filters on this column.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the session duration in milliseconds.
    /// </summary>
    public long? SessionDurationMs { get; set; }

    /// <summary>
    /// Gets or sets the assistant speaking time in milliseconds.
    /// </summary>
    public long? AssistantSpeakingMs { get; set; }

    /// <summary>
    /// Gets or sets the caller speaking time in milliseconds.
    /// </summary>
    public long? CallerSpeakingMs { get; set; }

    /// <summary>
    /// Gets or sets the mutual silence in milliseconds.
    /// </summary>
    public long? MutualSilenceMs { get; set; }

    /// <summary>
    /// Gets or sets the time to the first assistant audio in milliseconds.
    /// </summary>
    public long? TimeToFirstAssistantAudioMs { get; set; }

    /// <summary>
    /// Gets or sets the number of assistant turns.
    /// </summary>
    public int AssistantTurns { get; set; }

    /// <summary>
    /// Gets or sets the number of caller turns.
    /// </summary>
    public int CallerTurns { get; set; }

    /// <summary>
    /// Gets or sets the number of barge-ins.
    /// </summary>
    public int? BargeIns { get; set; }

    /// <summary>
    /// Gets or sets the number of idle prompts.
    /// </summary>
    public int IdlePrompts { get; set; }

    /// <summary>
    /// Gets or sets the echo-held milliseconds.
    /// </summary>
    public long? EchoHeldMs { get; set; }

    /// <summary>
    /// Gets or sets the audio input tokens.
    /// </summary>
    public long? InputAudioTokens { get; set; }

    /// <summary>
    /// Gets or sets the audio output tokens.
    /// </summary>
    public long? OutputAudioTokens { get; set; }

    /// <summary>
    /// Gets or sets the text input tokens.
    /// </summary>
    public long? InputTextTokens { get; set; }

    /// <summary>
    /// Gets or sets the text output tokens.
    /// </summary>
    public long? OutputTextTokens { get; set; }

    /// <summary>
    /// Gets or sets the cached input tokens.
    /// </summary>
    public long? CachedInputTokens { get; set; }
}
