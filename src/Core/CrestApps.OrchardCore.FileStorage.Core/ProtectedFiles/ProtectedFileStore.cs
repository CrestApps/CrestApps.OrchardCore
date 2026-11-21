using Microsoft.AspNetCore.StaticFiles;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.FileStorage.ProtectedFiles;

/// <summary>
/// Keeps the files of an area in a folder of their own: one folder per file id holding the file under its name.
/// </summary>
/// <typeparam name="TArea">The marker type of the area.</typeparam>
public sealed class ProtectedFileStore<TArea> : IProtectedFileStore<TArea>
{
    private static readonly char[] _invalidFileNameChars = [.. "\"<>|:*?", .. Enumerable.Range(0, 32).Select(code => (char)code)];
    private static readonly FileExtensionContentTypeProvider _contentTypes = new();

    private readonly IFileStore _fileStore;
    private readonly FileCreationService _fileCreationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtectedFileStore{TArea}"/> class.
    /// </summary>
    /// <param name="fileStore">The folder of the area.</param>
    /// <param name="fileCreationService">The file creation pipeline.</param>
    public ProtectedFileStore(IFileStore fileStore, FileCreationService fileCreationService)
    {
        _fileStore = fileStore;
        _fileCreationService = fileCreationService;
    }

    /// <inheritdoc/>
    public async Task<ProtectedFile> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
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

        return new ProtectedFile(fileId, name, info?.Length ?? 0);
    }

    /// <inheritdoc/>
    public async Task<ProtectedFile> FindAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (!IsValidId(fileId) || await _fileStore.GetDirectoryInfoAsync(fileId) is null)
        {
            return null;
        }

        await foreach (var entry in _fileStore.GetDirectoryContentAsync(fileId))
        {
            if (!entry.IsDirectory)
            {
                return new ProtectedFile(fileId, entry.Name, entry.Length);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenAsync(string fileId, CancellationToken cancellationToken = default)
    {
        var file = await FindAsync(fileId, cancellationToken);

        return file is null ? null : await _fileStore.GetFileStreamAsync(_fileStore.Combine(fileId, file.FileName));
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (!IsValidId(fileId) || await _fileStore.GetDirectoryInfoAsync(fileId) is null)
        {
            return false;
        }

        return await _fileStore.TryDeleteDirectoryAsync(fileId);
    }

    /// <summary>
    /// Gets whether an id is one this store makes, so a crafted id can't climb out of the folder.
    /// </summary>
    /// <param name="fileId">The id.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public static bool IsValidId(string fileId)
        => !string.IsNullOrEmpty(fileId) && fileId.Length <= 64 && fileId.All(char.IsAsciiLetterOrDigit);

    /// <summary>
    /// Makes a file name safe on every file system.
    /// </summary>
    /// <param name="fileName">The name.</param>
    /// <returns>The safe name.</returns>
    public static string Sanitize(string fileName)
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

        return name.Length == 0 ? "file" : name;
    }
}
