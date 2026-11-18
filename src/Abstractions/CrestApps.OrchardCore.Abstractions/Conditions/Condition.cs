namespace CrestApps.OrchardCore.Conditions;

/// <summary>
/// One test of a <see cref="ConditionRule"/>, such as "the answer to Is this an outage? is Yes".
/// </summary>
public sealed class Condition
{
    /// <summary>
    /// Gets or sets what the condition reads: another question or field by its key, or a value a module provides by its
    /// name. The consumer says what keys mean.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets how the values are compared.
    /// </summary>
    public ConditionOperator Operator { get; set; }

    /// <summary>
    /// Gets or sets the values compared, written with the invariant culture. Most operators use the first;
    /// <see cref="ConditionOperator.IsOneOf"/> and <see cref="ConditionOperator.IsNoneOf"/> use them all;
    /// <see cref="ConditionOperator.IsEmpty"/> and <see cref="ConditionOperator.IsNotEmpty"/> use none.
    /// </summary>
    public List<string> Values { get; set; } = [];
}
