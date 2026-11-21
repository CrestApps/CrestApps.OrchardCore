namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// Which choices of an earlier question are carried forward.
/// </summary>
public enum ChoicesFromMode
{
    /// <summary>
    /// All of them.
    /// </summary>
    All,

    /// <summary>
    /// Those the respondent selected.
    /// </summary>
    Selected,

    /// <summary>
    /// Those the respondent did not select.
    /// </summary>
    Unselected,
}
