namespace CrestApps.OrchardCore.Surveys.Core.Controls.ChoiceBlanks;

/// <summary>
/// How the words of a choice blanks question are picked.
/// </summary>
public enum ChoiceBlanksDisplay
{
    /// <summary>
    /// A drop-down in each blank.
    /// </summary>
    GapSelect,

    /// <summary>
    /// Words dragged into the blanks (with a drop-down for keyboards and screen readers).
    /// </summary>
    DragIntoText,
}
