namespace CrestApps.OrchardCore.Surveys.Core.Controls.TextBlock;

/// <summary>
/// The settings of a text block.
/// </summary>
public sealed class TextBlockSettings
{
    /// <summary>
    /// Gets or sets the sanitized HTML shown.
    /// </summary>
    public string Html { get; set; }

    /// <summary>
    /// Gets or sets an image shown, or <see langword="null"/>.
    /// </summary>
    public string ImageUrl { get; set; }
}
