using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;

namespace CrestApps.Core.AI.Profiles;

/// <summary>
/// Manages AI profile templates with unified access to both
/// database-stored and file-based template sources.
/// </summary>
public interface IAIProfileTemplateManager : INamedSourceCatalogManager<AIProfileTemplate>
{
    /// <summary>
    /// Gets all listable profile templates from all sources
    /// (database and file-based providers).
    /// </summary>
    ValueTask<IEnumerable<AIProfileTemplate>> GetListableAsync();
}
