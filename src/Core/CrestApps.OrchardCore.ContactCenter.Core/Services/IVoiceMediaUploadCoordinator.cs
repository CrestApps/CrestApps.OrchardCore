namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Makes sure a clip is uploaded to a provider once when several calls need it at the same moment. Right after a
/// change of provider every concurrent call finds the provider without a copy; without this each would upload the
/// clip, one would be recorded and the rest would be left at the provider unreferenced.
/// </summary>
public interface IVoiceMediaUploadCoordinator
{
    /// <summary>
    /// Runs the upload for the clip and provider unless one is already running or has just completed, in which case
    /// the caller waits for, or reuses, that upload's reference instead.
    /// </summary>
    /// <param name="providerName">The technical name of the provider being given the copy.</param>
    /// <param name="masterStorageReference">The master copy the provider copy is made from.</param>
    /// <param name="upload">The upload to run when none is running; returns the provider's reference, or <see langword="null"/> when it failed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider's reference, or <see langword="null"/> when the upload failed.</returns>
    Task<string> UploadOnceAsync(string providerName, string masterStorageReference, Func<CancellationToken, Task<string>> upload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets the reference a recent upload produced for the clip and provider, because the provider no longer
    /// holds that copy or it was deleted, so the next caller uploads again instead of reusing it.
    /// </summary>
    /// <param name="providerName">The technical name of the provider.</param>
    /// <param name="masterStorageReference">The master copy the provider copy was made from.</param>
    void Forget(string providerName, string masterStorageReference);
}
