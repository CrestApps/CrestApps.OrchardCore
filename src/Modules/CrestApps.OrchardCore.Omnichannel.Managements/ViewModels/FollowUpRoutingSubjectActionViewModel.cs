using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the follow-up section of a "Try again" or "New activity" subject action: its campaign, how it is dialed,
/// and who owns it.
/// </summary>
public class FollowUpRoutingSubjectActionViewModel
{
    /// <summary>
    /// The <see cref="DialWith"/> value for a follow-up dialed under the profile the activity it follows had, if any.
    /// </summary>
    public const string DialWithInherited = "";

    /// <summary>
    /// The <see cref="DialWith"/> value for a follow-up no dialer calls: an agent works it.
    /// </summary>
    public const string DialWithNone = "__none__";

    /// <summary>
    /// Gets or sets the campaign of the follow-up; empty keeps its campaign.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets how the follow-up is dialed: <see cref="DialWithInherited"/>, <see cref="DialWithNone"/>, or a
    /// dialer profile id.
    /// </summary>
    public string DialWith { get; set; }

    /// <summary>
    /// Gets or sets who owns the follow-up.
    /// </summary>
    public SubjectActionOwnerAssignmentType AssignTo { get; set; }

    /// <summary>
    /// Gets or sets the normalized user name of the owner, for <see cref="SubjectActionOwnerAssignmentType.SpecificOwner"/>.
    /// </summary>
    public string NormalizedUserName { get; set; }

    /// <summary>
    /// Gets or sets where the pool's users come from, for <see cref="SubjectActionOwnerAssignmentType.Pool"/>.
    /// </summary>
    public FollowUpOwnerPoolSource PoolSource { get; set; }

    /// <summary>
    /// Gets or sets the pool's team.
    /// </summary>
    public string PoolTeamId { get; set; }

    /// <summary>
    /// Gets or sets the pool's roles.
    /// </summary>
    public string[] PoolRoleNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the pool's user ids.
    /// </summary>
    public string[] PoolUserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how one user of the pool is picked.
    /// </summary>
    public FollowUpOwnerPoolStrategy PoolStrategy { get; set; }

    /// <summary>
    /// Gets or sets whether a pool can be picked from, which needs the Teams feature.
    /// </summary>
    [BindNever]
    public bool PoolAvailable { get; set; }

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
    /// Gets or sets the selected owner, shown by the user picker.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> SelectedUsers { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Teams { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Roles { get; set; } = [];
}
