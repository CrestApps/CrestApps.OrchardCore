using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The Workforce section of the team editor.
/// </summary>
public class WorkforceTeamEditViewModel
{
    /// <summary>
    /// Gets or sets the site address.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets the site's latitude, for the time clock's geofence.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Gets or sets the site's longitude, for the time clock's geofence.
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Gets or sets the time clock policy; empty to use the parent team's.
    /// </summary>
    public string TimeClockPolicyId { get; set; }

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
    /// Gets or sets whether the time clock is on, so its settings are shown.
    /// </summary>
    [BindNever]
    public bool ShowTimeClock { get; set; }

    /// <summary>
    /// Gets or sets whether shift planning is on, so its settings are shown.
    /// </summary>
    [BindNever]
    public bool ShowShiftPlanning { get; set; }

    /// <summary>
    /// Gets or sets whether time off is on too, so its shift setting is shown.
    /// </summary>
    [BindNever]
    public bool ShowTimeOff { get; set; }

    /// <summary>
    /// Gets or sets the time clock policies to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TimeClockPolicyOptions { get; set; } = [];
}
