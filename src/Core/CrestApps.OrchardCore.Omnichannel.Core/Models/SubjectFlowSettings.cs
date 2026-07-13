using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the flow settings for a subject content type.
/// Stores interaction type, channel, campaign association, and AI configuration.
/// </summary>
public sealed class SubjectFlowSettings : CatalogItem, IDisplayTextAwareModel, IModifiedUtcAwareModel, ICloneable<SubjectFlowSettings>
{
    /// <summary>
    /// Gets or sets the display text for this flow settings entry.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the subject content type this flow applies to.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the campaign identifier that this subject belongs to.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the interaction type for activities using this subject.
    /// </summary>
    public ActivityInteractionType InteractionType { get; set; }

    /// <summary>
    /// Gets or sets the communication channel for activities using this subject.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the channel endpoint identifier for activities using this subject.
    /// </summary>
    public string ChannelEndpointId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a disposition must be selected before an activity using
    /// this subject can be completed. This is the single decision-control policy that applies to both
    /// inbound and outbound activities, enforced by the activity disposition service.
    /// </summary>
    public bool RequireDisposition { get; set; }

    /// <summary>
    /// When the interaction is automated, this will be the initial message to start the conversation with the customer.
    /// </summary>
    public string InitialOutboundPromptPattern { get; set; }

    /// <summary>
    /// A clear description of what success looks like for this automated subject.
    /// Used by the AI to determine when the interaction can be terminated.
    /// </summary>
    public string SubjectGoal { get; set; }

    /// <summary>
    /// Gets or sets the AI profile identifier used for automated interactions.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the optional speech-to-text deployment name used for automated phone calls.
    /// When empty, the site default speech-to-text deployment is used.
    /// </summary>
    public string SpeechToTextDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the optional text-to-speech deployment name used for automated phone calls.
    /// When empty, the site default text-to-speech deployment is used.
    /// </summary>
    public string TextToSpeechDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the optional text-to-speech voice identifier used for automated phone calls.
    /// When empty, the site default voice is used.
    /// </summary>
    public string TextToSpeechVoiceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to allow AI to update contact.
    /// </summary>
    public bool AllowAIToUpdateContact { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to allow AI to update subject.
    /// </summary>
    public bool AllowAIToUpdateSubject { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of minutes to wait before an automated SMS activity is marked as failed
    /// when the contact stops responding.
    /// </summary>
    public int? NoResponseTimeoutInMinutes { get; set; }

    /// <summary>
    /// Gets or sets the number of seconds to wait before sending each automated SMS response.
    /// </summary>
    public int? SmsResponseDelayInSeconds { get; set; }

    /// <summary>
    /// Gets or sets SMS opt-out keywords that stop automated SMS conversations and set the contact's
    /// do-not-SMS preference.
    /// </summary>
    public string[] SmsOptOutKeywords { get; set; }

    /// <summary>
    /// Gets or sets the date and time the settings were created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the date and time the settings were last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the author.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the owner identifier.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Creates a copy of the current subject flow settings.
    /// </summary>
    public SubjectFlowSettings Clone()
    {
        return new SubjectFlowSettings
        {
            ItemId = ItemId,
            DisplayText = DisplayText,
            SubjectContentType = SubjectContentType,
            CampaignId = CampaignId,
            InteractionType = InteractionType,
            Channel = Channel,
            ChannelEndpointId = ChannelEndpointId,
            RequireDisposition = RequireDisposition,
            InitialOutboundPromptPattern = InitialOutboundPromptPattern,
            SubjectGoal = SubjectGoal,
            ProfileId = ProfileId,
            SpeechToTextDeploymentName = SpeechToTextDeploymentName,
            TextToSpeechDeploymentName = TextToSpeechDeploymentName,
            TextToSpeechVoiceId = TextToSpeechVoiceId,
            AllowAIToUpdateContact = AllowAIToUpdateContact,
            AllowAIToUpdateSubject = AllowAIToUpdateSubject,
            NoResponseTimeoutInMinutes = NoResponseTimeoutInMinutes,
            SmsResponseDelayInSeconds = SmsResponseDelayInSeconds,
            SmsOptOutKeywords = SmsOptOutKeywords?.ToArray(),
            CreatedUtc = CreatedUtc,
            ModifiedUtc = ModifiedUtc,
            Author = Author,
            OwnerId = OwnerId,
        };
    }
}
