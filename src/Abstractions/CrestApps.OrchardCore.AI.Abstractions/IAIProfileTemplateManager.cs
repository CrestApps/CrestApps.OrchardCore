namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Manages AI profile templates.
/// </summary>
public interface IAIProfileTemplateManager
{
    /// <summary>
    /// Gets all available AI profile templates.
    /// </summary>
    /// <returns>An enumerable of all registered templates.</returns>
    IEnumerable<IAIProfileTemplate> GetAllTemplates();

    /// <summary>
    /// Gets all templates compatible with the specified profile source.
    /// </summary>
    /// <param name="profileSource">The profile source to filter by.</param>
    /// <returns>An enumerable of compatible templates.</returns>
    IEnumerable<IAIProfileTemplate> GetTemplatesForSource(string profileSource);

    /// <summary>
    /// Gets a template by its name.
    /// </summary>
    /// <param name="name">The name of the template.</param>
    /// <returns>The template, or null if not found.</returns>
    IAIProfileTemplate GetTemplate(string name);
}
