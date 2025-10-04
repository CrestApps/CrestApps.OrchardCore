using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class OmnichannelCampaign : CatalogItem, IDisplayTextAwareModel, ICloneable<OmnichannelCampaign>
{
    public string DisplayText { get; set; }

    public string Description { get; set; }

    public ActivityInteractionType InteractionType { get; set; }

    public string AIProfileName { get; set; }

    public string Channel { get; set; }

    public string ChannelEndpoint { get; set; }

    /// <summary>
    /// When the campaign in automated, this will be the initial message to start the converation with the customer.
    /// </summary>
    public string InitialOutboundPromptPattern { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public IList<string> DispositionIds { get; set; }

    public OmnichannelCampaign Clone()
    {
        return new OmnichannelCampaign()
        {
            ItemId = ItemId,
            DisplayText = DisplayText,
            Description = Description,
            InteractionType = InteractionType,
            AIProfileName = AIProfileName,
            Channel = Channel,
            ChannelEndpoint = ChannelEndpoint,
            InitialOutboundPromptPattern = InitialOutboundPromptPattern,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            DispositionIds = DispositionIds?.ToArray(),
        };
    }
}

