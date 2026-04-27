using System.Text.RegularExpressions;
using CrestApps.Core.AI.Documents;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.AI.Documents.Services;

/// <summary>
/// Represents the default document file store.
/// </summary>
public sealed class DefaultDocumentFileStore : IDocumentFileStore
{
    private static readonly Regex _safePathSegmentExpression = new("^[a-zA-Z0-9._-]+$", RegexOptions.Compiled);

    private readonly IFileStore _fileStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDocumentFileStore"/> class.
    /// </summary>
    /// <param name="fileStore">The file store.</param>
    public DefaultDocumentFileStore(IFileStore fileStore)
    {
        _fileStore = fileStore;
    }

    /// <summary>
    /// Saves the file async.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="content">The content.</param>
    public async Task<string> SaveFileAsync(string fileName, Stream content)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(content);

        var relativePath = NormalizeRelativePath(fileName);

        return await _fileStore.CreateFileFromStreamAsync(relativePath, content, overwrite: true);
    }

    /// <summary>
    /// Retrieves the file async.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    public async Task<Stream> GetFileAsync(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        var relativePath = NormalizeRelativePath(fileName);
        var entry = await _fileStore.GetFileInfoAsync(relativePath);

        if (entry is null)
        {
            return null;
        }

        return await _fileStore.GetFileStreamAsync(relativePath);
    }

    /// <summary>
    /// Removes the file async.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    public async Task<bool> DeleteFileAsync(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        var relativePath = NormalizeRelativePath(fileName);

        return await _fileStore.TryDeleteFileAsync(relativePath);
    }

    private static string NormalizeRelativePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.IsPathRooted(fileName))
        {
            throw new ArgumentException("The file name contains an invalid path.");
        }

        var segments = fileName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            throw new ArgumentException("The file name contains an invalid path.");
        }

        foreach (var segment in segments)
        {
            if (segment is "." or ".." || !_safePathSegmentExpression.IsMatch(segment))
            {
                throw new ArgumentException("The file name contains an invalid path.");
            }
        }

        return Path.Combine(segments);
    }
}
