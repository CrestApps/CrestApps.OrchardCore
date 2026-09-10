using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for voice media library entries.
/// </summary>
public interface IVoiceMediaItemStore : ICatalog<VoiceMediaItem>
{
    /// <summary>
    /// Finds the media clip with the specified unique name.
    /// </summary>
    /// <param name="name">The clip name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching clip, or <see langword="null"/> when none exists.</returns>
    Task<VoiceMediaItem> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every media clip ordered by name for display.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>All media clips.</returns>
    new Task<IReadOnlyCollection<VoiceMediaItem>> GetAllAsync(CancellationToken cancellationToken = default);
}
