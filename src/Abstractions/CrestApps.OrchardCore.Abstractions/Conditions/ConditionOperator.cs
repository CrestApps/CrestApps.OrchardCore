namespace CrestApps.OrchardCore.Conditions;

/// <summary>
/// How a <see cref="Condition"/> compares the values of its source with the values it names.
/// </summary>
public enum ConditionOperator
{
    /// <summary>
    /// One of the source's values is the condition's value.
    /// </summary>
    Is,

    /// <summary>
    /// None of the source's values is the condition's value.
    /// </summary>
    IsNot,

    /// <summary>
    /// One of the source's values is one of the condition's values.
    /// </summary>
    IsOneOf,

    /// <summary>
    /// None of the source's values is one of the condition's values.
    /// </summary>
    IsNoneOf,

    /// <summary>
    /// The source has no value, or only blank ones.
    /// </summary>
    IsEmpty,

    /// <summary>
    /// The source has a value that isn't blank.
    /// </summary>
    IsNotEmpty,

    /// <summary>
    /// One of the source's values contains the condition's value.
    /// </summary>
    Contains,

    /// <summary>
    /// One of the source's values is a number or an ISO 8601 date greater than the condition's value.
    /// </summary>
    GreaterThan,

    /// <summary>
    /// One of the source's values is a number or an ISO 8601 date less than the condition's value.
    /// </summary>
    LessThan,

    /// <summary>
    /// One of the source's values is the condition's value or sits under it in a tree, such as a sub-category under its
    /// category. Needs a source that gives the ancestors of its values (<see cref="ConditionValue.Ancestors"/>).
    /// </summary>
    IsOrUnder,

    /// <summary>
    /// None of the source's values contains the condition's value.
    /// </summary>
    NotContains,

    /// <summary>
    /// One of the source's values starts with the condition's value.
    /// </summary>
    StartsWith,

    /// <summary>
    /// One of the source's values ends with the condition's value.
    /// </summary>
    EndsWith,

    /// <summary>
    /// One of the source's values is a number or an ISO 8601 date greater than or equal to the condition's value.
    /// </summary>
    GreaterThanOrEqual,

    /// <summary>
    /// One of the source's values is a number or an ISO 8601 date less than or equal to the condition's value.
    /// </summary>
    LessThanOrEqual,

    /// <summary>
    /// One of the source's values is a number or an ISO 8601 date between the condition's two values, both included.
    /// The two values may be given in either order.
    /// </summary>
    Between,

    /// <summary>
    /// The source has at least as many values that aren't blank as the condition's value, such as "at least 2 choices
    /// selected".
    /// </summary>
    CountAtLeast,

    /// <summary>
    /// The source has at most as many values that aren't blank as the condition's value.
    /// </summary>
    CountAtMost,
}
