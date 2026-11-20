namespace CrestApps.OrchardCore.Conditions;

/// <summary>
/// What a source reads as for a condition: its values (a field can hold several, such as chosen terms) and, for a value
/// that sits in a tree, the values above it, so <see cref="ConditionOperator.IsOrUnder"/> can match a parent.
/// </summary>
public sealed class ConditionValue
{
    /// <summary>
    /// A source that holds nothing.
    /// </summary>
    public static readonly ConditionValue Empty = new([]);

    /// <summary>
    /// A source that can't answer, such as a hidden question: every condition that reads it fails, whatever its operator
    /// (<see cref="ConditionOperator.IsEmpty"/> and <see cref="ConditionOperator.IsNot"/> included).
    /// </summary>
    public static readonly ConditionValue Withheld = new([], null, withheld: true);

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionValue"/> class.
    /// </summary>
    /// <param name="values">The values, in the invariant culture.</param>
    /// <param name="ancestors">The values above them in a tree, if any.</param>
    public ConditionValue(IEnumerable<string> values, IEnumerable<string> ancestors = null)
        : this(values, ancestors, withheld: false)
    {
    }

    private ConditionValue(IEnumerable<string> values, IEnumerable<string> ancestors, bool withheld)
    {
        Values = values?.Where(value => value is not null).ToArray() ?? [];
        Ancestors = ancestors?.Where(value => value is not null).ToArray() ?? [];
        IsWithheld = withheld;
    }

    /// <summary>
    /// Gets whether the source can't answer (<see cref="Withheld"/>).
    /// </summary>
    public bool IsWithheld { get; }

    /// <summary>
    /// Gets the values.
    /// </summary>
    public IReadOnlyList<string> Values { get; }

    /// <summary>
    /// Gets the values above <see cref="Values"/> in a tree, such as the categories above a sub-category.
    /// </summary>
    public IReadOnlyList<string> Ancestors { get; }

    /// <summary>
    /// Makes a value from one string, or an empty one.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The condition value.</returns>
    public static ConditionValue Of(string value)
        => value is null ? Empty : new ConditionValue([value]);
}
