namespace CrestApps.OrchardCore.Conditions;

/// <summary>
/// When something applies, such as when a question shows: all or any of a list of conditions and nested groups of
/// conditions. A rule without conditions always applies.
/// </summary>
public sealed class ConditionRule
{
    /// <summary>
    /// Gets or sets whether all the conditions must be met, or any one of them.
    /// </summary>
    public ConditionMatch Match { get; set; }

    /// <summary>
    /// Gets or sets the conditions.
    /// </summary>
    public List<Condition> Conditions { get; set; } = [];

    /// <summary>
    /// Gets or sets nested groups, such as "(A or B)" in "C and (A or B)". <see cref="Match"/> applies over the
    /// conditions and the groups together: each group counts as one item, met when its own rule is met.
    /// </summary>
    public List<ConditionRule> Groups { get; set; } = [];

    /// <summary>
    /// Gets whether a rule has no condition with a source, in itself or in a nested group, and so always applies.
    /// </summary>
    /// <param name="rule">The rule, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it always applies.</returns>
    public static bool IsEmpty(ConditionRule rule)
    {
        if (rule is null)
        {
            return true;
        }

        if (rule.Conditions is not null && rule.Conditions.Any(condition => !string.IsNullOrWhiteSpace(condition?.Source)))
        {
            return false;
        }

        return rule.Groups is null || rule.Groups.All(IsEmpty);
    }
}

/// <summary>
/// Whether a <see cref="ConditionRule"/> needs all its conditions or any one of them.
/// </summary>
public enum ConditionMatch
{
    /// <summary>
    /// Every condition must be met.
    /// </summary>
    All,

    /// <summary>
    /// One condition is enough.
    /// </summary>
    Any,
}
