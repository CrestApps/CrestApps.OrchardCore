using CrestApps.AI.Models;

namespace CrestApps.AI.Services;

/// <summary>
/// Resolves the active document retrieval settings for the current host.
/// </summary>
public interface IInteractionDocumentSettingsProvider
{
    /// <summary>
    /// Gets the current interaction document settings.
    /// </summary>
    Task<InteractionDocumentSettings> GetAsync();
}
