using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The fields of a time-off allotment.
/// </summary>
public class TimeOffAllotmentEditViewModel
{
    /// <summary>
    /// Gets or sets the allotment id, or empty for a new one.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the types it counts.
    /// </summary>
    public string[] TypeIds { get; set; }

    /// <summary>
    /// Gets or sets how many people may be off on each weekday, Monday first.
    /// </summary>
    public int[] Slots { get; set; } = [0, 0, 0, 0, 0, 0, 0];

    /// <summary>
    /// Gets or sets dates with their own number, one per line.
    /// </summary>
    public string Dates { get; set; }

    /// <summary>
    /// Gets or sets whether requests that fit are approved with no manager.
    /// </summary>
    public bool AutoApprove { get; set; }

    /// <summary>
    /// Gets or sets what happens to a request that doesn't fit.
    /// </summary>
    public string WhenFull { get; set; }

    /// <summary>
    /// Gets or sets whether it is in use.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the teams.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TeamOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the time-off types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];
}
