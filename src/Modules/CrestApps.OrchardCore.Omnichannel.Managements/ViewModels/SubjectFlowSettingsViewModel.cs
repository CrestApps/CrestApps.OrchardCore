using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for subject flow settings.
/// </summary>
public class SubjectFlowSettingsViewModel
{
    /// <summary>
    /// Gets or sets the campaign identifier.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the interaction type.
    /// </summary>
    public ActivityInteractionType InteractionType { get; set; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the channel endpoint identifier.
    /// </summary>
    public string ChannelEndpointId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a disposition must be selected before an activity using this subject can be completed.
    /// </summary>
    public bool RequireDisposition { get; set; }

    /// <summary>
    /// Gets or sets the initial outbound prompt pattern.
    /// </summary>
    public string InitialOutboundPromptPattern { get; set; }

    /// <summary>
    /// Gets or sets the subject goal.
    /// </summary>
    public string SubjectGoal { get; set; }

    /// <summary>
    /// Gets or sets the AI profile identifier.
    /// </summary>
    public string ProfileId { get; set; }

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
    /// Gets or sets a value indicating whether allow AI to update contact.
    /// </summary>
    public bool AllowAIToUpdateContact { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether allow AI to update subject.
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
    /// Gets or sets the available campaigns.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Campaigns { get; set; }

    /// <summary>
    /// Gets or sets the available interaction types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> InteractionTypes { get; set; }

    /// <summary>
    /// Gets or sets the available channels.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Channels { get; set; }

    /// <summary>
    /// Gets or sets the available channel endpoints.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> ChannelEndpoints { get; set; }

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
}
