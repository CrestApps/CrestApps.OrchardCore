using CrestApps.OrchardCore.Conditions;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A jump taken when a page is left and its rule holds.
/// </summary>
public sealed class SurveyBranch
{
    /// <summary>
    /// Gets or sets the stable id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the rule.
    /// </summary>
    public ConditionRule Rule { get; set; }

    /// <summary>
    /// Gets or sets an optional formula that must also hold.
    /// </summary>
    public string Formula { get; set; }

    /// <summary>
    /// Gets or sets whether the branch goes to a page or an ending.
    /// </summary>
    public BranchTargetKind TargetKind { get; set; }

    /// <summary>
    /// Gets or sets the id of a later page or of an ending.
    /// </summary>
    public string TargetId { get; set; }
}
