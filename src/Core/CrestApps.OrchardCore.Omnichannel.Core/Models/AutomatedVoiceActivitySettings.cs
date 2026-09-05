namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the effective AI and speech settings for an automated phone activity.
/// </summary>
public sealed class AutomatedVoiceActivitySettings
{
    /// <summary>
    /// Gets or sets the AI chat profile identifier.
    /// </summary>
    public string AIProfileId { get; set; }

    /// <summary>
    /// Gets or sets the effective speech-to-text deployment name.
    /// </summary>
    public string SpeechToTextDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the effective text-to-speech deployment name.
    /// </summary>
    public string TextToSpeechDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the effective text-to-speech voice identifier.
    /// </summary>
    public string TextToSpeechVoiceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI may update the contact during an automated conversation.
    /// </summary>
    public bool AllowAIToUpdateContact { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI may update the subject during an automated conversation.
    /// </summary>
    public bool AllowAIToUpdateSubject { get; set; } = true;

    /// <summary>
    /// Gets or sets how long the automated conversation waits before sending each AI reply.
    /// </summary>
    public OmnichannelResponseDelayMode ResponseDelayMode { get; set; }

    /// <summary>
    /// Gets or sets the reply delay in seconds (the exact wait when fixed, or the base when random).
    /// </summary>
    public int ResponseDelaySeconds { get; set; }

    /// <summary>
    /// Gets or sets the jitter, in seconds, applied around <see cref="ResponseDelaySeconds"/> when random.
    /// </summary>
    public int ResponseDelayJitterSeconds { get; set; }

    /// <summary>
    /// Gets or sets the business-hours calendar id that gates background-initiated sends (re-engagement nudges),
    /// evaluated in the contact's local time zone. Empty is unrestricted.
    /// </summary>
    public string BusinessHoursCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the reusable cadence that governs re-engagement. Empty means never nudge.
    /// </summary>
    public string CadenceId { get; set; }
}
