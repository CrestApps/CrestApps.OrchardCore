namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Provides a unified view of AI profile templates from all sources
/// (database-stored and file-based).
/// </summary>
public interface IAIProfileTemplateService
{
    /// <summary>
    /// Gets all available profile templates from all sources.
    /// </summary>
    Task<IReadOnlyList<AIProfileTemplate>> GetAllAsync();

    /// <summary>
    /// Gets all listable profile templates from all sources.
    /// </summary>
    Task<IReadOnlyList<AIProfileTemplate>> GetListableAsync();

    /// <summary>
    /// Finds a profile template by its identifier, searching all sources.
    /// </summary>
    Task<AIProfileTemplate> FindByIdAsync(string id);
}
