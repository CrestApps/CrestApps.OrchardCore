using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;

/// <summary>
/// Edits the Create AI Activity workflow task.
/// </summary>
public class CreateAIActivityTaskViewModel
{
    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the AI profile that holds the conversation.
    /// </summary>
    public string AIProfileId { get; set; }

    /// <summary>
    /// Gets or sets the number the call or text comes from.
    /// </summary>
    public string ChannelEndpointId { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression of the customer's phone number.
    /// </summary>
    public string Destination { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression of the contact's content item id.
    /// </summary>
    public string ContactContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the subject content type.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the campaign identifier.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the variables the conversation starts with.
    /// </summary>
    public string Variables { get; set; }

    /// <summary>
    /// Gets or sets the AI profiles to choose from.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Profiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the numbers to choose from.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Endpoints { get; set; } = [];
}
