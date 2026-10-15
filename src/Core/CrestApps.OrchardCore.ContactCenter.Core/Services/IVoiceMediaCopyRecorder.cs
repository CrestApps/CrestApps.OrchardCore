using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Does the bookkeeping that follows a change to a clip's copies once the current unit of work has committed, in a
/// scope of its own: records the copy a provider was given while a call needed it, and removes the copies an upload
/// replaced. Running afterwards keeps a write that collides with a concurrent one (an agent's live presence, an
/// operator's edit of the entry) from failing the call or the save, and lets each task reload the record and act
/// only on what was actually stored.
/// </summary>
public interface IVoiceMediaCopyRecorder
{
    /// <summary>
    /// Schedules the copy a provider was given of a voice media library entry to be recorded on that entry. The
    /// task reloads the entry and records the copy only while the entry's master copy is still the one the copy was
    /// made from; otherwise the audio changed meanwhile and the copy, which plays the old audio, is deleted. A copy
    /// another path recorded for the provider meanwhile is kept, and the copy made here is deleted as a duplicate,
    /// unless it is the copy this one was made to replace.
    /// </summary>
    /// <param name="itemId">The library entry identifier.</param>
    /// <param name="masterStorageReference">The master copy the provider copy was made from.</param>
    /// <param name="providerName">The technical name of the provider that holds the copy.</param>
    /// <param name="mediaReference">The reference the provider plays the clip by.</param>
    /// <param name="replacedReference">The reference of the copy the provider reported missing, which the new copy replaces; <see langword="null"/> when the provider had no copy recorded.</param>
    void RecordLibraryCopy(string itemId, string masterStorageReference, string providerName, string mediaReference, string replacedReference);

    /// <summary>
    /// Schedules the copy a provider was given of an agent's voicemail greeting to be recorded on the agent's
    /// profile, under the same rules as <see cref="RecordLibraryCopy"/>.
    /// </summary>
    /// <param name="agentId">The identifier of the agent whose greeting was copied.</param>
    /// <param name="masterStorageReference">The master copy the provider copy was made from.</param>
    /// <param name="providerName">The technical name of the provider that holds the copy.</param>
    /// <param name="mediaReference">The reference the provider plays the greeting by.</param>
    /// <param name="replacedReference">The reference of the copy the provider reported missing, which the new copy replaces; <see langword="null"/> when the provider had no copy recorded.</param>
    void RecordGreetingCopy(string agentId, string masterStorageReference, string providerName, string mediaReference, string replacedReference);

    /// <summary>
    /// Schedules the copies an agent's greeting was held as before it was saved to be deleted, once the save has
    /// committed. The task reloads the profile: when it holds the saved greeting (or, when <paramref name="saved"/>
    /// is <see langword="null"/>, no greeting at all) the replaced copies are deleted; when the save did not land,
    /// the copies made for it are deleted instead, so the profile never references audio that is gone.
    /// </summary>
    /// <param name="agentId">The identifier of the agent.</param>
    /// <param name="saved">The copies the saved greeting is held as, or <see langword="null"/> when the greeting was removed.</param>
    /// <param name="replaced">The copies the previous greeting was held as, or <see langword="null"/> when there were none.</param>
    void ReleaseReplacedGreetingCopies(string agentId, VoiceMediaAsset saved, VoiceMediaAsset replaced);
}
