namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the AI-related, type-level flow settings for <see cref="OmnichannelSubjectPart"/>. These
/// settings are stored on the content-type part definition and are only editable when the AI feature is
/// enabled. They describe how automated (AI-driven) interactions behave for the subject.
/// </summary>
public sealed class OmnichannelSubjectAISettings
{
    /// <summary>
    /// Gets or sets the AI profile identifier used for automated interactions of this subject. It is used
    /// as the pre-selected profile when an activity batch is loaded and may be overridden per batch.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets a clear description of what success looks like for this automated subject. The AI uses
    /// it to determine when the interaction can be terminated.
    /// </summary>
    public string SubjectGoal { get; set; }

    /// <summary>
    /// Gets or sets the initial message used to start an automated conversation with the customer.
    /// </summary>
    public string InitialOutboundPromptPattern { get; set; }

    /// <summary>
    /// Gets or sets the optional speech-to-text deployment name used for automated phone calls. When empty,
    /// the site default speech-to-text deployment is used.
    /// </summary>
    public string SpeechToTextDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the optional text-to-speech deployment name used for automated phone calls. When empty,
    /// the site default text-to-speech deployment is used.
    /// </summary>
    public string TextToSpeechDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the optional text-to-speech voice identifier used for automated phone calls. When empty,
    /// the site default voice is used.
    /// </summary>
    public string TextToSpeechVoiceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI is allowed to update the contact.
    /// </summary>
    public bool AllowAIToUpdateContact { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI is allowed to update the subject.
    /// </summary>
    public bool AllowAIToUpdateSubject { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of minutes to wait before an automated SMS activity is marked as failed when
    /// the contact stops responding.
    /// </summary>
    public int? NoResponseTimeoutInMinutes { get; set; }

    /// <summary>
    /// Gets or sets the number of seconds to wait before sending each automated SMS response.
    /// </summary>
    public int? SmsResponseDelayInSeconds { get; set; }

    /// <summary>
    /// Gets or sets the SMS opt-out keywords that stop automated SMS conversations and set the contact's
    /// do-not-SMS preference.
    /// </summary>
    public string[] SmsOptOutKeywords { get; set; }
}
