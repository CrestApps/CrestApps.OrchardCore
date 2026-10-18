using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.Downloads.Services;

/// <summary>
/// The private store digital files are kept in: a folder under the tenant's App_Data, served by no URL. Each file is
/// kept at <c>{fileId}/{fileName}</c>, so its name travels with it.
/// </summary>
public interface IDigitalFileStore
{
    /// <summary>
    /// Saves a new file and returns what was kept.
    /// </summary>
    Task<StoredDigitalFile> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a file by its id, or returns <see langword="null"/>.
    /// </summary>
    Task<StoredDigitalFile> FindAsync(string fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a file to read, or returns <see langword="null"/> when it is gone.
    /// </summary>
    Task<Stream> OpenAsync(string fileId, CancellationToken cancellationToken = default);
}

public sealed record StoredDigitalFile(string FileId, string FileName, long Size);

internal sealed class DigitalFileStore : IDigitalFileStore
{
    private static readonly char[] _invalidFileNameChars = [.. "\"<>|:*?", .. Enumerable.Range(0, 32).Select(code => (char)code)];

    private readonly IFileStore _fileStore;

    public DigitalFileStore(IFileStore fileStore)
    {
        _fileStore = fileStore;
    }

    public async Task<StoredDigitalFile> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var fileId = global::OrchardCore.IdGenerator.GenerateId();
        var name = Sanitize(fileName);

        await _fileStore.CreateFileFromStreamAsync(_fileStore.Combine(fileId, name), content, overwrite: false);

        var info = await _fileStore.GetFileInfoAsync(_fileStore.Combine(fileId, name));

        return new StoredDigitalFile(fileId, name, info?.Length ?? 0);
    }

    public async Task<StoredDigitalFile> FindAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (!IsValidId(fileId) || await _fileStore.GetDirectoryInfoAsync(fileId) is null)
        {
            return null;
        }

        await foreach (var entry in _fileStore.GetDirectoryContentAsync(fileId))
        {
            if (!entry.IsDirectory)
            {
                return new StoredDigitalFile(fileId, entry.Name, entry.Length);
            }
        }

        return null;
    }

    public async Task<Stream> OpenAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var file = await FindAsync(fileId, cancellationToken);

        return file is null ? null : await _fileStore.GetFileStreamAsync(_fileStore.Combine(fileId, file.FileName));
    }

    // Only ids this store made reach the file system, so a crafted id can't climb out of the folder.
    internal static bool IsValidId(string fileId)
        => !string.IsNullOrEmpty(fileId) && fileId.Length <= 64 && fileId.All(char.IsAsciiLetterOrDigit);

    internal static string Sanitize(string fileName)
    {
        // A name can come from any system, so both separators end a folder and the characters Windows refuses are
        // replaced on every server: the same upload gets the same name wherever the site runs.
        var name = fileName ?? string.Empty;
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..].Trim();

        foreach (var invalid in _invalidFileNameChars)
        {
            name = name.Replace(invalid, '_');
        }

        name = name.Trim('.', ' ');

        if (name.Length > 200)
        {
            var extension = Path.GetExtension(name);

            name = name[..(200 - extension.Length)] + extension;
        }

        return string.IsNullOrEmpty(name) ? "file" : name;
    }
}
