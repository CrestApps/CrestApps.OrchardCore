using System.Text.RegularExpressions;
using CrestApps.Core.AI.Documents;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.AI.Documents.Services;

public sealed class DefaultDocumentFileStore : IDocumentFileStore
{
    private static readonly Regex _safePathSegmentExpression = new("^[a-zA-Z0-9._-]+$", RegexOptions.Compiled);

    private readonly IFileStore _fileStore;

    public DefaultDocumentFileStore(IFileStore fileStore)
    {
        _fileStore = fileStore;
    }

    public async Task<string> SaveFileAsync(string fileName, Stream content)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(content);

        var relativePath = NormalizeRelativePath(fileName);

        return await _fileStore.CreateFileFromStreamAsync(relativePath, content, overwrite: true);
    }

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
