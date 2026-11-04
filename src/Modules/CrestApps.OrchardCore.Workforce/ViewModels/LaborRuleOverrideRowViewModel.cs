using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// A row of the rule override list.
/// </summary>
public class LaborRuleOverrideRowViewModel
{
    /// <summary>
    /// Gets or sets the override.
    /// </summary>
    public LaborRuleOverride Override { get; set; }

    /// <summary>
    /// Gets or sets the worker's name.
    /// </summary>
    public string WorkerName { get; set; }

    /// <summary>
    /// Gets or sets the team's name.
    /// </summary>
    public string TeamName { get; set; }

    /// <summary>
    /// Gets or sets the rule's name.
    /// </summary>
    public string RuleName { get; set; }

    /// <summary>
    /// Gets or sets the rule set's name.
    /// </summary>
    public string RuleSetName { get; set; }

    /// <summary>
    /// Gets or sets the site's time zone, to show when it was recorded.
    /// </summary>
    public string TimeZoneId { get; set; }
}
