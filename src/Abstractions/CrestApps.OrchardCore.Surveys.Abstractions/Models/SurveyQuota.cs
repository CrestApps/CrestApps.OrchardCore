using CrestApps.OrchardCore.Conditions;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A count of completed responses that match a rule, with an action when the limit is reached.
/// </summary>
public sealed class SurveyQuota
{
    /// <summary>
    /// Gets or sets the stable id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the variable code.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the label staff see.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets which responses count; all when empty.
    /// </summary>
    public ConditionRule Rule { get; set; }

    /// <summary>
    /// Gets or sets the limit.
    /// </summary>
    public int Limit { get; set; }

    /// <summary>
    /// Gets or sets what happens to new matching responses once the limit is reached.
    /// </summary>
    public QuotaAction Action { get; set; }

    /// <summary>
    /// Gets or sets the ending used when the action screens out.
    /// </summary>
    public string EndingId { get; set; }
}
