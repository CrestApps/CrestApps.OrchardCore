using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A broken rule that someone allowed went ahead with, and their reason.
/// </summary>
public sealed class LaborRuleOverride : CatalogItem
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the rule set.
    /// </summary>
    public string RuleSetId { get; set; }

    /// <summary>
    /// Gets or sets the rule.
    /// </summary>
    public string RuleKey { get; set; }

    /// <summary>
    /// Gets or sets what was broken.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets why it went ahead.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets what the change was, such as Publish.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the schedule, when the change was a publish.
    /// </summary>
    public string PeriodId { get; set; }

    /// <summary>
    /// Gets or sets the shifts.
    /// </summary>
    public IList<string> ShiftKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets who overrode it.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was recorded.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
