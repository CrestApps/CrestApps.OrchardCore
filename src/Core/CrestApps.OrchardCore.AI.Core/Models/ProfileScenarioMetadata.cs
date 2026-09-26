namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Describes how a profile template is presented as a scenario in the "New AI profile" gallery.
/// </summary>
/// <remarks>
/// Read from a template file's front matter (<c>Featured</c>, <c>Icon</c>, <c>Order</c> and
/// <c>RequiresFeatures</c>). It belongs to the template alone and is never copied onto a profile built from it.
/// </remarks>
public sealed class ProfileScenarioMetadata
{
    /// <summary>
    /// Gets or sets a value indicating whether the template is shown as a featured scenario card rather than
    /// in the list of other starting points.
    /// </summary>
    public bool Featured { get; set; }

    /// <summary>
    /// Gets or sets the Font Awesome class of the scenario's icon, for example <c>fa-solid fa-comments</c>.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Gets or sets the position of the scenario within its category. Lower numbers come first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the identifiers of the features that must be enabled before the scenario can be used.
    /// </summary>
    public string[] RequiresFeatures { get; set; } = [];
}
