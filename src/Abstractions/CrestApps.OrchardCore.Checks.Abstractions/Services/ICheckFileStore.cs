namespace CrestApps.OrchardCore.Checks.Services;

/// <summary>
/// Keeps the files of check layouts (signature images, the check line font) encrypted in the tenant file store, which
/// is local or Azure Blob storage, never the media library.
/// </summary>
public interface ICheckFileStore
{
    /// <summary>
    /// Stores a file of a layout, replacing the one it had.
    /// </summary>
    /// <param name="layoutId">The layout's id.</param>
    /// <param name="kind">The kind of file, from <see cref="ChecksConstants.LayoutFiles"/>.</param>
    /// <param name="content">The file.</param>
    /// <returns>A task that completes when the file is stored.</returns>
    Task WriteAsync(string layoutId, string kind, byte[] content);

    /// <summary>
    /// Reads a file of a layout.
    /// </summary>
    /// <param name="layoutId">The layout's id.</param>
    /// <param name="kind">The kind of file.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The file, or <see langword="null"/> when none is stored or it can't be read with the tenant's keys.</returns>
    Task<byte[]> ReadAsync(string layoutId, string kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a file of a layout.
    /// </summary>
    /// <param name="layoutId">The layout's id.</param>
    /// <param name="kind">The kind of file.</param>
    /// <returns>A task that completes when the file is removed.</returns>
    Task DeleteAsync(string layoutId, string kind);
}
