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
    /// Gets or sets a value indicating whether the automated conversation may hand off to a live human agent.
    /// </summary>
    public bool EnableAgentHandoff { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the Contact Center queue an escalated conversation is handed to.
    /// </summary>
    public string HandoffQueueId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI may hand off when the customer asks for a human.
    /// </summary>
    public bool HandoffOnUserRequest { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI may hand off once the customer is a qualified lead.
    /// </summary>
    public bool HandoffOnQualifiedLead { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the AI may hand off when the customer is frustrated.
    /// </summary>
    public bool HandoffOnFrustration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the live-agent handoff settings apply to the subject. Unlike the
    /// rest of the AI configuration (inbound-only), handoff also applies to outbound automated campaigns whose AI
    /// settings are chosen at inventory load, so it is shown for those too.
    /// </summary>
    [BindNever]
    public bool ShowHandoffSettings { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a handoff queue picker is available (Contact Center is enabled).
    /// When false, the editor shows a free-text queue id field instead.
    /// </summary>
    [BindNever]
    public bool HasHandoffQueuePicker { get; set; }

    /// <summary>
    /// Gets or sets the selectable handoff queues, when a picker is available.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> HandoffQueues { get; set; }

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
    /// Gets or sets a value indicating whether the subject can run automated interactions. Only an inbound
    /// subject configured with an automated interaction type shows the AI configuration; outbound automation
    /// is configured when the inventory is loaded.
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
