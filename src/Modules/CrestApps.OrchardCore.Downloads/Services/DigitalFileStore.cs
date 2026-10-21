using Microsoft.AspNetCore.StaticFiles;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.Downloads.Services;

/// <summary>
/// The private store digital files are kept in: a folder under the tenant's App_Data, served by no URL. Each file is
/// kept at <c>{fileId}/{fileName}</c>, so its name travels with it.
/// </summary>
public interface IDigitalFileStore
{
    /// <summary>
    /// Runs a new file through the tenant's file creation pipeline (such as an antivirus scanner), saves it when no
    /// handler refuses it, and returns what was kept.
    /// </summary>
    /// <param name="fileName">The name the file was uploaded with.</param>
    /// <param name="content">The file's content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="FileStoreException">Thrown when a file event handler refuses the file; nothing is kept.</exception>
    Task<StoredDigitalFile> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a file by its id, or returns <see langword="null"/>.
    /// </summary>
    /// <param name="fileId">The file's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<StoredDigitalFile> FindAsync(string fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a file to read, or returns <see langword="null"/> when it is gone.
    /// </summary>
    /// <param name="fileId">The file's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<Stream> OpenAsync(string fileId, CancellationToken cancellationToken = default);
}

public sealed record StoredDigitalFile(string FileId, string FileName, long Size);

internal sealed class DigitalFileStore : IDigitalFileStore
{
    private static readonly char[] _invalidFileNameChars = [.. "\"<>|:*?", .. Enumerable.Range(0, 32).Select(code => (char)code)];
    private static readonly FileExtensionContentTypeProvider _contentTypes = new();

    private readonly IFileStore _fileStore;
    private readonly FileCreationService _fileCreationService;

    public DigitalFileStore(
        IFileStore fileStore,
        FileCreationService fileCreationService)
    {
        _fileStore = fileStore;
        _fileCreationService = fileCreationService;
    }

    public async Task<StoredDigitalFile> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var fileId = global::OrchardCore.IdGenerator.GenerateId();
        var name = Sanitize(fileName);
        var path = _fileStore.Combine(fileId, name);

        if (!_contentTypes.TryGetContentType(name, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        // Every handler (an antivirus scanner, for one) sees the file before it is written, and any of them can
        // refuse it.
        await using var result = await _fileCreationService.CreateAsync(
            new FileCreatingContext(path, content.CanSeek ? content.Length : null, contentType),
            content,
            leaveOpen: true,
            cancellationToken);

        if (!result.Succeeded)
        {
            throw new FileStoreException(result.ErrorMessage ?? $"The file '{name}' was refused before it could be kept.");
        }

        await _fileStore.CreateFileFromStreamAsync(path, result.Stream, overwrite: false);

        var info = await _fileStore.GetFileInfoAsync(path);

        if (info is not null)
        {
            await _fileCreationService.CreatedAsync(info, cancellationToken);
        }

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
