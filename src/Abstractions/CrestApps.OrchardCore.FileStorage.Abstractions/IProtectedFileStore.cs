namespace CrestApps.OrchardCore.FileStorage;

/// <summary>
/// The private files of one area, kept outside the media library under the tenant's App_Data where no URL serves them.
/// Every upload goes through Orchard Core's file creation pipeline first (an antivirus scanner, for one), which can
/// refuse it. Register an area with <c>services.AddProtectedFileArea&lt;TArea&gt;(folder)</c>.
/// </summary>
/// <typeparam name="TArea">The marker type that names the area.</typeparam>
public interface IProtectedFileStore<TArea>
{
    /// <summary>
    /// Keeps a file under a new id.
    /// </summary>
    /// <param name="fileName">The file name, made safe for every file system.</param>
    /// <param name="content">The content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The file kept.</returns>
    Task<ProtectedFile> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a file.
    /// </summary>
    Task<ProtectedFile> FindAsync(string fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a file to read.
    /// </summary>
    Task<Stream> OpenAsync(string fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file.
    /// </summary>
    Task<bool> DeleteAsync(string fileId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A protected file.
/// </summary>
/// <param name="FileId">The id.</param>
/// <param name="FileName">The file name.</param>
/// <param name="Size">The size, in bytes.</param>
public sealed record ProtectedFile(string FileId, string FileName, long Size);
