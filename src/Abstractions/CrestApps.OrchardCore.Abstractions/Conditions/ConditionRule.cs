namespace CrestApps.OrchardCore.Conditions;

/// <summary>
/// When something applies, such as when a question shows: all or any of a list of conditions. A rule without conditions
/// always applies.
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
    /// Gets whether a rule has no condition with a source, and so always applies.
    /// </summary>
    /// <param name="rule">The rule, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it always applies.</returns>
    public static bool IsEmpty(ConditionRule rule)
        => rule?.Conditions is null || !rule.Conditions.Any(condition => !string.IsNullOrWhiteSpace(condition?.Source));
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
