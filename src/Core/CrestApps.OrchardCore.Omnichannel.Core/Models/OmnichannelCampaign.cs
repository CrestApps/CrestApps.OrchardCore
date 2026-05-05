using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the omnichannel campaign.
/// </summary>
public sealed class OmnichannelCampaign : CatalogItem, IDisplayTextAwareModel, ICloneable<OmnichannelCampaign>
{
    /// <summary>
    /// Gets or sets the display text.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the interaction type.
    /// </summary>
    public ActivityInteractionType InteractionType { get; set; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the channel endpoint id.
    /// </summary>
    public string ChannelEndpointId { get; set; }

    /// <summary>
    /// When the campaign in automated, this will be the initial message to start the converation with the customer.
    /// </summary>
    public string InitialOutboundPromptPattern { get; set; }

    /// <summary>
    /// A clear description of what success looks like for this automated campaign.
    /// Used by the AI to determine when the chat can be terminated.
    /// </summary>
    public string CampaignGoal { get; set; }

    /// <summary>
    /// Gets or sets the provider name.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the connection name.
    /// </summary>
    public string ConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the deployment name.
    /// </summary>
    public string DeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the system message.
    /// </summary>
    public string SystemMessage { get; set; }

    /// <summary>
    /// Gets or sets the temperature.
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the top p.
    /// </summary>
    public float? TopP { get; set; }

    /// <summary>
    /// Gets or sets the frequency penalty.
    /// </summary>
    public float? FrequencyPenalty { get; set; }

    /// <summary>
    /// Gets or sets the presence penalty.
    /// </summary>
    public float? PresencePenalty { get; set; }

    /// <summary>
    /// Gets or sets the max tokens.
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the tool names.
    /// </summary>
    public string[] ToolNames { get; set; }

    /// <summary>
    /// Gets or sets the created utc.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the author.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets the owner id.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether allow AI to update contact.
    /// </summary>
    public bool AllowAIToUpdateContact { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether allow AI to update subject.
    /// </summary>
    public bool AllowAIToUpdateSubject { get; set; } = true;

    /// <summary>
    /// Creates a copy of the current campaign.
    /// </summary>
    public OmnichannelCampaign Clone()
    {
        return new OmnichannelCampaign
        {
            ItemId = ItemId,
            DisplayText = DisplayText,
            Description = Description,
            InteractionType = InteractionType,
            Channel = Channel,
            ChannelEndpointId = ChannelEndpointId,
            InitialOutboundPromptPattern = InitialOutboundPromptPattern,
            CampaignGoal = CampaignGoal,
            ProviderName = ProviderName,
            ConnectionName = ConnectionName,
            DeploymentName = DeploymentName,
            SystemMessage = SystemMessage,
            Temperature = Temperature,
            TopP = TopP,
            FrequencyPenalty = FrequencyPenalty,
            PresencePenalty = PresencePenalty,
            MaxTokens = MaxTokens,
            ToolNames = ToolNames?.ToArray(),
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            AllowAIToUpdateContact = AllowAIToUpdateContact,
            AllowAIToUpdateSubject = AllowAIToUpdateSubject,
        };
    }
}
