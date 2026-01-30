using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Defines a template for creating AI profiles with predefined configurations.
/// </summary>
public interface IAIProfileTemplate
{
    /// <summary>
    /// Gets the unique name of the template.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the localized display name of the template.
    /// </summary>
    LocalizedString DisplayName { get; }

    /// <summary>
    /// Gets the localized description of the template.
    /// </summary>
    LocalizedString Description { get; }

    /// <summary>
    /// Gets the profile source that this template is compatible with.
    /// Returns null if the template is compatible with all sources.
    /// </summary>
    string ProfileSource { get; }

    /// <summary>
    /// Applies the template's configuration to the given AI profile.
    /// </summary>
    /// <param name="profile">The AI profile to configure.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ApplyAsync(Models.AIProfile profile);
}
