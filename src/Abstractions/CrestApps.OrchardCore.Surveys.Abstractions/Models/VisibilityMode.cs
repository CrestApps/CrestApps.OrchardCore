namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// How a visibility rule applies.
/// </summary>
public enum VisibilityMode
{
    /// <summary>
    /// Always shows.
    /// </summary>
    Always,

    /// <summary>
    /// Shows when the rule holds.
    /// </summary>
    ShowWhen,

    /// <summary>
    /// Hides when the rule holds.
    /// </summary>
    HideWhen,
}
