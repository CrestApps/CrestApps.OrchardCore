namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// What was measured on one automated call's audio while the assistant held it.
/// </summary>
/// <remarks>
/// A <see langword="null"/> duration was not measurable by the engine that held the call, which is different from
/// zero.
/// </remarks>
public sealed class AIVoiceSessionMeasurements
{
    /// <summary>
    /// Gets or sets when the assistant took the call.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the assistant's part of the call ended.
    /// </summary>
    public DateTime EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets the length of the assistant's part of the call, in milliseconds.
    /// </summary>
    public long SessionDurationMs { get; set; }

    /// <summary>
    /// Gets or sets how long the caller heard the assistant, in milliseconds.
    /// </summary>
    public long? AssistantSpeakingMs { get; set; }

    /// <summary>
    /// Gets or sets how long the caller's voice was detected, in milliseconds.
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
    /// Gets or sets how many times the caller talked over the assistant and it stopped.
    /// </summary>
    public int BargeIns { get; set; }

    /// <summary>
    /// Gets or sets how many times the assistant spoke up because the line had gone quiet.
    /// </summary>
    public int IdlePrompts { get; set; }

    /// <summary>
    /// Gets or sets how much caller audio was held back as the assistant's echo, in milliseconds.
    /// </summary>
    public long? EchoHeldMs { get; set; }
}
