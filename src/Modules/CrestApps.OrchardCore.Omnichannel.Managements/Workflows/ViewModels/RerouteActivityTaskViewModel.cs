using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.ViewModels;

/// <summary>
/// Represents the editor of the Reroute Activity workflow task.
/// </summary>
public class RerouteActivityTaskViewModel
{
    /// <summary>
    /// Gets or sets the Liquid expression that resolves the activity to reroute.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets who works the activity next.
    /// </summary>
    public ActivityRerouteTarget Target { get; set; }

    /// <summary>
    /// Gets or sets the campaign the activity moves to; empty keeps its campaign.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the dialer profile that dials the activity.
    /// </summary>
    public string DialerProfileId { get; set; }

    /// <summary>
    /// Gets or sets the source of an activity an agent works; empty is Manual.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the user name of the agent who works the activity.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets whether a finished activity with no open follow-up gets its next attempt created.
    /// </summary>
    public bool CreateNextAttempt { get; set; }

    /// <summary>
    /// Gets or sets the Liquid expression that resolves how many minutes from now a created next attempt is due.
    /// </summary>
    public string DelayMinutes { get; set; }

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
    /// Gets or sets the sources an agent's activity may take.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Sources { get; set; } = [];
}
