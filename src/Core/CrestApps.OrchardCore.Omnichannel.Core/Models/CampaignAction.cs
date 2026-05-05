using System.Text.Json;

using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class CampaignAction : SourceCatalogEntry, IDisplayTextAwareModel, ICloneable<CampaignAction>
{
    /// <summary>
    /// Gets or sets the display text for this action.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the campaign identifier this action belongs to.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the disposition identifier that triggers this action.
    /// </summary>
    public string DispositionId { get; set; }

    /// <summary>
    /// Gets or sets whether to set the contact's "Do Not Call" preference when this action executes.
    /// </summary>
    public bool? SetDoNotCall { get; set; }

    /// <summary>
    /// Gets or sets whether to set the contact's "Do Not SMS" preference when this action executes.
    /// </summary>
    public bool? SetDoNotSms { get; set; }

    /// <summary>
    /// Gets or sets whether to set the contact's "Do Not Email" preference when this action executes.
    /// </summary>
    public bool? SetDoNotEmail { get; set; }

    /// <summary>
    /// Gets or sets whether to set the contact's "Do Not Chat" preference when this action executes.
    /// </summary>
    public bool? SetDoNotChat { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string Author { get; set; }

    public string OwnerId { get; set; }

    public CampaignAction Clone()
    {
        return new CampaignAction
        {
            ItemId = ItemId,
            Source = Source,
            DisplayText = DisplayText,
            CampaignId = CampaignId,
            DispositionId = DispositionId,
            SetDoNotCall = SetDoNotCall,
            SetDoNotSms = SetDoNotSms,
            SetDoNotEmail = SetDoNotEmail,
            SetDoNotChat = SetDoNotChat,
            CreatedUtc = CreatedUtc,
            Author = Author,
            OwnerId = OwnerId,
            Properties = Properties is null
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(Properties)),
        };
    }
}
