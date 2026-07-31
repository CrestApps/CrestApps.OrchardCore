using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for editing the base <see cref="OmnichannelSubjectPart"/> flow settings.
/// </summary>
public class OmnichannelSubjectPartSettingsViewModel
{
    /// <summary>
    /// Gets or sets the primary communication direction for the subject.
    /// </summary>
    public SubjectDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the interaction type used for inbound activities of this subject.
    /// </summary>
    public ActivityInteractionType InteractionType { get; set; }

    /// <summary>
    /// Gets or sets the channel used for inbound activities of this subject.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the channel endpoint used for inbound activities of this subject.
    /// </summary>
    public string ChannelEndpointId { get; set; }

    /// <summary>
    /// Gets or sets the optional default campaign for the subject.
    /// </summary>
    public string DefaultCampaignId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a disposition must be selected before an activity using this subject can be completed.
    /// </summary>
    public bool RequireDisposition { get; set; }

    /// <summary>
    /// Gets or sets the available communication directions.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Directions { get; set; }

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
    /// Gets or sets the available campaigns.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> Campaigns { get; set; }
}
