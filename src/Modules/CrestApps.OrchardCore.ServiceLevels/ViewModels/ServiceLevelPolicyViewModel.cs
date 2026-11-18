using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ServiceLevels.ViewModels;

/// <summary>
/// The general card of the policy editor: the name, what the policy is for, the hours it counts on and its warning
/// point.
/// </summary>
public class ServiceLevelPolicyViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the Business Hours schedule; empty counts around the clock.
    /// </summary>
    public string HoursScheduleId { get; set; }

    /// <summary>
    /// Gets or sets the warning point, in percent of a target.
    /// </summary>
    public int WarningPercent { get; set; }

    /// <summary>
    /// Gets or sets the name of the scope shown to users.
    /// </summary>
    [BindNever]
    public string ScopeDisplayName { get; set; }
}
