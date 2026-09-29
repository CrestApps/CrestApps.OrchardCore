namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Profile values a profile template file carries that the shared template model has no field for.
/// </summary>
/// <remarks>
/// Read from a template file's front matter (<c>InitialPrompt</c>). Unlike <see cref="ProfileScenarioMetadata"/>,
/// these values are meant for the profile: they are applied to a profile built from the template, and the
/// metadata itself is never copied across.
/// </remarks>
public sealed class ProfileTemplateDefaultsMetadata
{
    /// <summary>
    /// Gets or sets the initial prompt given to a profile built from the template, which turns on
    /// <c>Add initial prompt</c>.
    /// </summary>
    /// <remarks>
    /// It is the assistant's opening message: an automated SMS conversation sends it as the first text and an
    /// automated call speaks it as the greeting. It may use Liquid. Automated conversations only offer profiles
    /// that have one.
    /// </remarks>
    public string InitialPrompt { get; set; }
}
