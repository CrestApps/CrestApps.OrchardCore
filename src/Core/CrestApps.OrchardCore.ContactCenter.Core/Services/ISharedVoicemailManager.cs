using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Manages the messages in queue shared voicemail boxes.
/// </summary>
public interface ISharedVoicemailManager : ICatalogManager<SharedVoicemail>
{
    /// <summary>
    /// Finds the shared voicemail left on an interaction.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The shared voicemail, or <see langword="null"/> when the interaction left none.</returns>
    Task<SharedVoicemail> FindByInteractionIdAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a page of shared voicemails, newest first.
    /// </summary>
    /// <param name="query">What to read.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The page.</returns>
    Task<SharedVoicemailPage> QueryAsync(SharedVoicemailQuery query, CancellationToken cancellationToken = default);
}
