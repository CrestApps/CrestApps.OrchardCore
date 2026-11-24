using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the pool a subject action picks the owner of its follow-up from.
/// </summary>
public class FollowUpOwnerPoolSubjectActionViewModel
{
    /// <summary>
    /// Gets or sets where the pool's users come from.
    /// </summary>
    public FollowUpOwnerPoolSource Source { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the roles.
    /// </summary>
    public string[] RoleNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the user ids.
    /// </summary>
    public string[] UserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how one user is picked.
    /// </summary>
    public FollowUpOwnerPoolStrategy Strategy { get; set; }

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
