namespace CrestApps.OrchardCore.Disbursements.Services;

/// <summary>
/// Keeps the files of released batches encrypted in the tenant file store: they hold account numbers.
/// </summary>
public interface IDisbursementFileStore
{
    /// <summary>
    /// Stores a batch's file.
    /// </summary>
    /// <param name="batchId">The batch's id.</param>
    /// <param name="content">The file.</param>
    /// <returns>A task that completes when the file is stored.</returns>
    Task WriteAsync(string batchId, byte[] content);

    /// <summary>
    /// Reads a batch's file.
    /// </summary>
    /// <param name="batchId">The batch's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The file, or <see langword="null"/>.</returns>
    Task<byte[]> ReadAsync(string batchId, CancellationToken cancellationToken = default);
}
