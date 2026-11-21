namespace CrestApps.OrchardCore.Surveys.Core.Controls.MultipleText;

/// <summary>
/// The settings of a multiple text question. Its items are the question's rows.
/// </summary>
public sealed class MultipleTextSettings
{
    /// <summary>
    /// Gets or sets the most characters of each item, or <see langword="null"/> for 500.
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Gets or sets whether every item must be filled when the question is required.
    /// </summary>
    public bool AllItemsRequired { get; set; }
}
