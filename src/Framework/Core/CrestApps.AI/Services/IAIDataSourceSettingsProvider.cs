using CrestApps.AI.Models;

namespace CrestApps.AI.Services;

/// <summary>
/// Resolves the active data source retrieval settings for the current host.
/// </summary>
public interface IAIDataSourceSettingsProvider
{
    /// <summary>
    /// Gets the current data source settings.
    /// </summary>
    Task<AIDataSourceSettings> GetAsync();
}
