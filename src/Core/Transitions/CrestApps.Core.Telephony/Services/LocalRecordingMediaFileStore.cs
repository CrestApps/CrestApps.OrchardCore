using System.Runtime.CompilerServices;

namespace CrestApps.Core.Telephony.Services;

/// <summary>
/// Keeps recording bytes as files in one directory. This is the default backend for
/// <see cref="LocalEncryptedRecordingMediaStore"/>, and the one a host gets without configuring anything.
/// </summary>
/// <remarks>
/// A host that serves more than one tenant gives each of them its own directory, which is what keeps one
/// tenant's recordings from being addressable by another.
/// </remarks>
public sealed class LocalRecordingMediaFileStore : IRecordingMediaFileStore
{
    private readonly string _rootPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalRecordingMediaFileStore"/> class.
    /// </summary>
    /// <param name="rootPath">The directory the recordings are written to.</param>
    public LocalRecordingMediaFileStore(string rootPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootPath);

        _rootPath = rootPath;
    }

    /// <inheritdoc/>
    public async Task WriteAsync(string name, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(content);

        Directory.CreateDirectory(_rootPath);

        await using var file = new FileStream(
            ResolvePath(name),
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await content.CopyToAsync(file, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Stream> OpenReadAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var path = ResolvePath(name);

        if (!File.Exists(path))
        {
            return Task.FromResult<Stream>(null);
        }

        return Task.FromResult<Stream>(new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true));
    }

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var path = ResolvePath(name);

        if (!File.Exists(path))
        {
            return Task.FromResult(true);
        }

        try
        {
            File.Delete(path);

            return Task.FromResult(true);
        }
        catch (IOException)
        {
            return Task.FromResult(false);
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_rootPath))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(_rootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return Path.GetFileName(path);
        }

        await Task.CompletedTask;
    }

    private string ResolvePath(string name)
        => Path.Combine(_rootPath, name);
}
