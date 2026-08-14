namespace CrestApps.OrchardCore.ContentTransfer;

/// <summary>
/// Manages content transfer entry lifecycle operations that must be coordinated with background work.
/// </summary>
public interface IContentTransferEntryManager
{
    /// <summary>
    /// Pauses an in-progress import entry so it can be resumed later.
    /// </summary>
    /// <param name="entryId">The entry identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task PauseImportAsync(string entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an import entry as processing so background work can resume it.
    /// </summary>
    /// <param name="entryId">The entry identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task ResumeImportAsync(string entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an entry as deleting before background cleanup begins.
    /// </summary>
    /// <param name="entryId">The entry identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task MarkAsDeletingAsync(string entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entry and its stored file while coordinating with any active background processing lock.
    /// </summary>
    /// <param name="entryId">The entry identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task DeleteAsync(string entryId, CancellationToken cancellationToken = default);
}
