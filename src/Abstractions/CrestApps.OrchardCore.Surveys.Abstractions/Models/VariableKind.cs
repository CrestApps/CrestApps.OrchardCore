namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// Where a variable gets its value.
/// </summary>
public enum VariableKind
{
    /// <summary>
    /// From the consumer, the link, the collector or the default value.
    /// </summary>
    Hidden,

    /// <summary>
    /// From a formula.
    /// </summary>
    Calculated,
}
