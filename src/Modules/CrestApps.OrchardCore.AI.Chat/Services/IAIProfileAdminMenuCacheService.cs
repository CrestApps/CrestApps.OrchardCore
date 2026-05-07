using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// Caches the chat AI profiles that should be surfaced on admin-menu chat entry points.
/// </summary>
public interface IAIProfileAdminMenuCacheService
{
    /// <summary>
    /// Gets the cached AI chat profiles that should be shown on the admin menu.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A value task that resolves to the chat AI profiles configured for admin-menu display.
    /// </returns>
    ValueTask<IReadOnlyList<AIProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached admin-menu AI profiles.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask InvalidateAsync(CancellationToken cancellationToken = default);
}
