namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// A choice or row of a question imported again that kept the id of the earlier item at its place only: its text
/// changed and neither side has a key. It now holds the answers given to the earlier item, so an import reports it.
/// </summary>
public sealed class SurveyPositionMatch
{
    /// <summary>
    /// Gets or sets the item's place in its list, from 1.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the text of the earlier item.
    /// </summary>
    public string EarlierText { get; set; }

    /// <summary>
    /// Gets or sets the text of the new item.
    /// </summary>
    public string Text { get; set; }
}
