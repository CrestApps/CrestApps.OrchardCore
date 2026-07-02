namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the routing priority of an interaction. Higher values are handled before lower values.
/// </summary>
public enum InteractionPriority
{
    /// <summary>
    /// The lowest routing priority.
    /// </summary>
    Lowest = 0,

    /// <summary>
    /// A low routing priority.
    /// </summary>
    Low = 1,

    /// <summary>
    /// The default routing priority.
    /// </summary>
    Normal = 2,

    /// <summary>
    /// A high routing priority.
    /// </summary>
    High = 3,

    /// <summary>
    /// The highest routing priority.
    /// </summary>
    Highest = 4,
}
