using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the "Next call" section of a "Try again" or "New activity" subject action.
/// </summary>
public class FollowUpRoutingSubjectActionViewModel
{
    /// <summary>
    /// Gets or sets who makes the follow-up call.
    /// </summary>
    public FollowUpRoutingTarget Target { get; set; }

    /// <summary>
    /// Gets or sets the campaign of the follow-up; empty keeps its campaign.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the dialer profile that dials the follow-up.
    /// </summary>
    public string DialerProfileId { get; set; }

    /// <summary>
    /// Gets or sets the source of a follow-up an agent works.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the campaigns to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Campaigns { get; set; } = [];

    /// <summary>
    /// Gets or sets the dialer profiles to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> DialerProfiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the sources an agent's call may have.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Sources { get; set; } = [];
}
