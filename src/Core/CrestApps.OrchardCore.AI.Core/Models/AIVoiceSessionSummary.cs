using System.Text.Json.Serialization;
using CrestApps.OrchardCore.YesSql.Core.Serialization;

namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// What one automated AI voice conversation cost and how it went: which model held it, how long each side spoke,
/// how long the line was silent, and how it ended. Written once, when the conversation is over.
/// </summary>
/// <remarks>
/// A completion record describes one request to a model. A voice call is dozens of them, or, on a speech-to-speech
/// session, none that the completion pipeline ever sees. This is the per-call view the completion records cannot
/// give: talk time, silence and interruptions measured from the audio itself.
/// <para>
/// Every duration is <see langword="null"/> when the engine that held the call could not measure it, which is
/// different from zero: a turn-based call has no event for when the caller starts and stops speaking, so its caller
/// speaking time is unknown rather than nothing.
/// </para>
/// </remarks>
public sealed class AIVoiceSessionSummary
{
    /// <summary>
    /// Gets or sets the unique identifier of this summary.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the omnichannel activity the call belongs to.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the AI chat session the conversation was transcribed into, which is also the session the
    /// call's text completions are recorded against.
    /// </summary>
    public string AISessionId { get; set; }

    /// <summary>
    /// Gets or sets the AI profile that held the conversation.
    /// </summary>
    public string AIProfileId { get; set; }

    /// <summary>
    /// Gets or sets the name of the AI profile when the call ended, kept so the report still reads after the
    /// profile is renamed or deleted.
    /// </summary>
    public string AIProfileName { get; set; }

    /// <summary>
    /// Gets or sets the campaign the call was placed for, if any.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the name of the campaign when the call ended.
    /// </summary>
    public string CampaignName { get; set; }

    /// <summary>
    /// Gets or sets the channel the call was carried on.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the channel endpoint (the number) the call was carried on.
    /// </summary>
    public string ChannelEndpointId { get; set; }

    /// <summary>
    /// Gets or sets the telephony provider that carried the call.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier for the call.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets which conversation engine held the call.
    /// </summary>
    public AIVoiceSessionEngine Engine { get; set; }

    /// <summary>
    /// Gets or sets the deployment that actually held the call.
    /// </summary>
    public string DeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the model behind <see cref="DeploymentName"/>.
    /// </summary>
    public string ModelName { get; set; }

    /// <summary>
    /// Gets or sets the provider connection behind <see cref="DeploymentName"/>.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets how the conversation ended.
    /// </summary>
    public AIVoiceSessionOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets when the assistant took the answered call, or <see langword="null"/> when it was never
    /// answered or the start was not observed.
    /// </summary>
    [JsonConverter(typeof(PreciseUtcDateTimeJsonConverter))]
    public DateTime? StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the assistant's part of the call ended: the end of the session, the handoff to a person,
    /// or the hangup.
    /// </summary>
    [JsonConverter(typeof(PreciseUtcDateTimeJsonConverter))]
    public DateTime? EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets when this summary was written.
    /// </summary>
    [JsonConverter(typeof(PreciseUtcDateTimeJsonConverter))]
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the length of the assistant's part of the call, in milliseconds.
    /// </summary>
    public long? SessionDurationMs { get; set; }

    /// <summary>
    /// Gets or sets how long the caller heard the assistant speak, in milliseconds.
    /// </summary>
    public long? AssistantSpeakingMs { get; set; }

    /// <summary>
    /// Gets or sets how long the caller spoke, in milliseconds.
    /// </summary>
    public long? CallerSpeakingMs { get; set; }

    /// <summary>
    /// Gets or sets how long neither side was speaking, in milliseconds.
    /// </summary>
    public long? MutualSilenceMs { get; set; }

    /// <summary>
    /// Gets or sets how long after the answer the caller first heard the assistant, in milliseconds.
    /// </summary>
    public long? TimeToFirstAssistantAudioMs { get; set; }

    /// <summary>
    /// Gets or sets the number of lines the assistant said.
    /// </summary>
    public int AssistantTurns { get; set; }

    /// <summary>
    /// Gets or sets the number of transcribed caller turns.
    /// </summary>
    public int CallerTurns { get; set; }

    /// <summary>
    /// Gets or sets how many times the caller talked over the assistant and the assistant stopped.
    /// </summary>
    public int? BargeIns { get; set; }

    /// <summary>
    /// Gets or sets how many times the assistant spoke up because nobody had said anything.
    /// </summary>
    public int IdlePrompts { get; set; }

    /// <summary>
    /// Gets or sets how much caller audio heard while the assistant was speaking was held back as echo, in
    /// milliseconds.
    /// </summary>
    public long? EchoHeldMs { get; set; }

    /// <summary>
    /// Gets or sets the audio input tokens the session consumed, when the provider reports them.
    /// </summary>
    public long? InputAudioTokens { get; set; }

    /// <summary>
    /// Gets or sets the audio output tokens the session consumed, when the provider reports them.
    /// </summary>
    public long? OutputAudioTokens { get; set; }

    /// <summary>
    /// Gets or sets the text input tokens the session consumed, when the provider reports them.
    /// </summary>
    public long? InputTextTokens { get; set; }

    /// <summary>
    /// Gets or sets the text output tokens the session consumed, when the provider reports them.
    /// </summary>
    public long? OutputTextTokens { get; set; }

    /// <summary>
    /// Gets or sets the input tokens served from the provider's cache, when the provider reports them.
    /// </summary>
    public long? CachedInputTokens { get; set; }
}
