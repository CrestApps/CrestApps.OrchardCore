using CrestApps.AI.Models;

namespace CrestApps.AI.Profiles;

/// <summary>
/// Provides AI profile templates from a specific source (e.g., module files).
/// </summary>
public interface IAIProfileTemplateProvider
{
    /// <summary>
    /// Gets all profile templates from this provider.
    /// </summary>
    Task<IReadOnlyList<AIProfileTemplate>> GetTemplatesAsync();
}
