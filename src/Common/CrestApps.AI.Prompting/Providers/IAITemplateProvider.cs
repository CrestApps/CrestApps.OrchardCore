using CrestApps.AI.Prompting.Models;

namespace CrestApps.AI.Prompting.Providers;

/// <summary>
/// Provides prompt templates from a specific source.
/// Implement this interface to add custom prompt template discovery.
/// </summary>
public interface IAITemplateProvider
{
    /// <summary>
    /// Gets all prompt templates from this provider.
    /// </summary>
    /// <returns>The discovered prompt templates.</returns>
    Task<IReadOnlyList<AITemplate>> GetTemplatesAsync();
}
