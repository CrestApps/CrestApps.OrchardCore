namespace CrestApps.OrchardCore.Surveys.Core.Controls.LongText;

/// <summary>
/// The settings of a long text question.
/// </summary>
public sealed class LongTextSettings
{
    /// <summary>
    /// Gets or sets the number of visible lines.
    /// </summary>
    public int Rows { get; set; } = 5;

    /// <summary>
    /// Gets or sets the fewest words, or <see langword="null"/>.
    /// </summary>
    public int? MinWords { get; set; }

    /// <summary>
    /// Gets or sets the most words, or <see langword="null"/>.
    /// </summary>
    public int? MaxWords { get; set; }
}
