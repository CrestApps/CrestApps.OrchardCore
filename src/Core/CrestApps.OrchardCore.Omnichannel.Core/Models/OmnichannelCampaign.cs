using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class OmnichannelCampaign : CatalogItem, IDisplayTextAwareModel, ICloneable<OmnichannelCampaign>
{
    public string DisplayText { get; set; }

    public string Description { get; set; }

    public ActivityInteractionType InteractionType { get; set; }

    public string Channel { get; set; }

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

    public string ProviderName { get; set; }

    public string ConnectionName { get; set; }

    public string DeploymentName { get; set; }

    public string SystemMessage { get; set; }

    public float? Temperature { get; set; }

    public float? TopP { get; set; }

    public float? FrequencyPenalty { get; set; }

    public float? PresencePenalty { get; set; }

    public int? MaxTokens { get; set; }

    public string[] ToolNames { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public IList<string> DispositionIds { get; set; }

    public bool AllowAIToUpdateContact { get; set; }

    public bool AllowAIToUpdateSubject { get; set; } = true;

    public OmnichannelCampaign Clone()
    {
        return new OmnichannelCampaign()
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
            DispositionIds = DispositionIds?.ToArray(),
            AllowAIToUpdateContact = AllowAIToUpdateContact,
            AllowAIToUpdateSubject = AllowAIToUpdateSubject,
        };
    }
}

