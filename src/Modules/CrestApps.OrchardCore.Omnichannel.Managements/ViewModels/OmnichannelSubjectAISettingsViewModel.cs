using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for editing the AI-related <see cref="OmnichannelSubjectPart"/> flow settings.
/// </summary>
public class OmnichannelSubjectAISettingsViewModel
{
    /// <summary>
    /// Gets or sets the AI profile identifier used for automated interactions of this subject.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the subject goal used by the AI to determine when to end the conversation.
    /// </summary>
    public string SubjectGoal { get; set; }

    /// <summary>
    /// Gets or sets the optional speech-to-text deployment name.
    /// </summary>
    public string SpeechToTextDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the optional text-to-speech deployment name.
    /// </summary>
    public string TextToSpeechDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the optional text-to-speech voice identifier.
    /// </summary>
    public string TextToSpeechVoiceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI is allowed to update the contact.
    /// </summary>
    public bool AllowAIToUpdateContact { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI is allowed to update the subject.
    /// </summary>
    public bool AllowAIToUpdateSubject { get; set; }

    /// <summary>
    /// Gets or sets the no-response timeout, in minutes.
    /// </summary>
    public int? NoResponseTimeoutInMinutes { get; set; }

    /// <summary>
    /// Gets or sets the SMS response delay, in seconds.
    /// </summary>
    public int? SmsResponseDelayInSeconds { get; set; }

    /// <summary>
    /// Gets or sets the SMS opt-out keywords.
    /// </summary>
    public string SmsOptOutKeywords { get; set; }

    /// <summary>
    /// Gets or sets the available AI profiles.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; }

    /// <summary>
    /// Gets or sets the available speech-to-text deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SpeechToTextDeployments { get; set; }

    /// <summary>
    /// Gets or sets the available text-to-speech deployments.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> TextToSpeechDeployments { get; set; }

    /// <summary>
    /// Gets or sets the available text-to-speech voices.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> TextToSpeechVoices { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the subject can run automated interactions. Outbound subjects
    /// resolve their interaction type when an activity batch is loaded, so they can always be automated.
    /// </summary>
    [BindNever]
    public bool CanAutomate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the voice call automation settings apply to the subject.
    /// </summary>
    [BindNever]
    public bool ShowVoiceSettings { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the SMS automation settings apply to the subject.
    /// </summary>
    [BindNever]
    public bool ShowSmsSettings { get; set; }
}
