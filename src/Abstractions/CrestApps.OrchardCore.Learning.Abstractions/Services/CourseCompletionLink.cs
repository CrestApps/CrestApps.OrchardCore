namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// A link on the course completion panel.
/// </summary>
public sealed class CourseCompletionLink
{
    /// <summary>
    /// Gets or sets the text of the link, localized for the learner.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the address, absolute or starting with <c>~/</c> for the site's root.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the Font Awesome classes of the icon, such as <c>fa-solid fa-certificate</c>.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Gets or sets the order of the link on the panel; lower comes first.
    /// </summary>
    public int Position { get; set; }
}
