using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents one profile template offered as a starting point in the "New AI profile" gallery.
/// </summary>
public class ProfileScenarioCardViewModel
{
    /// <summary>
    /// Gets or sets the identifier of the template the scenario creates a profile from.
    /// </summary>
    public string TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the scenario title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the text that explains what the scenario is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the category the scenario is grouped under.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the Font Awesome class of the scenario's icon.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the template is a featured scenario rather than one of the
    /// other starting points.
    /// </summary>
    public bool IsFeatured { get; set; }

    /// <summary>
    /// Gets or sets the type of profile the scenario creates, when the template sets one.
    /// </summary>
    public AIProfileType? ProfileType { get; set; }

    /// <summary>
    /// Gets or sets the names of the features the scenario needs that are not enabled.
    /// </summary>
    /// <remarks>
    /// The scenario can be used only when this is empty.
    /// </remarks>
    public IList<string> MissingFeatureNames { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether every feature the scenario needs is enabled.
    /// </summary>
    public bool IsAvailable => MissingFeatureNames.Count == 0;
}
