using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The team editor.
/// </summary>
public class TeamEditViewModel
{
    /// <summary>
    /// Gets or sets the team id, or empty for a new team.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the parent team.
    /// </summary>
    public string ParentId { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the linked role.
    /// </summary>
    public string LinkedRoleName { get; set; }

    /// <summary>
    /// Gets or sets how the team's open shifts are given; empty to use the parent team's.
    /// </summary>
    public string OpenShiftClaimPolicy { get; set; }

    /// <summary>
    /// Gets or sets whether a manager approves the team's shift trades: <c>true</c>, <c>false</c>, or empty to use the
    /// parent team's.
    /// </summary>
    public string ShiftTradesNeedApproval { get; set; }

    /// <summary>
    /// Gets or sets what happens to a worker's published shifts when their time off is approved; empty to use the
    /// parent team's.
    /// </summary>
    public string TimeOffShiftHandling { get; set; }

    /// <summary>
    /// Gets or sets whether time off is on too, so its shift setting is shown.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool ShowTimeOff { get; set; }

    /// <summary>
    /// Gets or sets whether shift planning is on, so its settings are shown.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool ShowShiftPlanning { get; set; }

    /// <summary>
    /// Gets or sets the time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the address.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets whether the team is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the kinds to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> KindOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams to choose the parent from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ParentOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the time zones to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TimeZoneOptions { get; set; } = [];
}
