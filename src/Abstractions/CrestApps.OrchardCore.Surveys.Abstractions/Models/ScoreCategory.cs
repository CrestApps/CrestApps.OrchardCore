namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A category that question points add to, such as a skill or a section.
/// </summary>
public sealed class ScoreCategory
{
    /// <summary>
    /// Gets or sets the stable id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the variable code.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }
}
