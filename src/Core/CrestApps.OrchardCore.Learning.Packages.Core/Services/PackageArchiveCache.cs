using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// Keeps a local copy of package archives that come from a store without seeking, such as Azure Blob Storage, so a
/// launch reads each file out of the archive without downloading the whole package for every request.
/// </summary>
/// <remarks>
/// A package file id never changes content: replacing a package stores a new file. A copy is therefore valid for as
/// long as it exists, and copies nobody opened for <see cref="UnusedLifetime"/> are removed. Archives from a store that
/// seeks, such as the local file system, are never copied.
/// </remarks>
public sealed class PackageArchiveCache
{
    /// <summary>
    /// How long a copy nobody opened is kept.
    /// </summary>
    public static readonly TimeSpan UnusedLifetime = TimeSpan.FromDays(14);

    private static readonly TimeSpan _touchInterval = TimeSpan.FromHours(6);

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);
    private readonly string _folder;
    private readonly ILogger _logger;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageArchiveCache"/> class.
    /// </summary>
    /// <param name="shellOptions">The shell options.</param>
    /// <param name="shellSettings">The tenant's settings.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public PackageArchiveCache(IOptions<ShellOptions> shellOptions, ShellSettings shellSettings, IClock clock, ILogger<PackageArchiveCache> logger)
        : this(Path.Combine(shellOptions.Value.ShellsApplicationDataPath, shellOptions.Value.ShellsContainerName, shellSettings.Name, PackagesConstants.FilesFolder + "Cache"), clock, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageArchiveCache"/> class over a folder.
    /// </summary>
    /// <param name="folder">The folder the copies are kept in.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    internal PackageArchiveCache(string folder, IClock clock, ILogger logger)
    {
        _folder = folder;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Opens a package archive as a stream that seeks.
    /// </summary>
    /// <param name="fileId">The package file id.</param>
    /// <param name="open">Opens the archive from its store; returns <see langword="null"/> when there is no such file.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The archive, or <see langword="null"/> when the store has no such file.</returns>
    public async Task<Stream> OpenAsync(string fileId, Func<CancellationToken, Task<Stream>> open, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(open);

        if (!IsValidId(fileId))
        {
            return await open(cancellationToken);
        }

        var path = Path.Combine(_folder, fileId + ".zip");

        if (TryOpenCopy(path) is { } cached)
        {
            return cached;
        }

        var gate = _gates.GetOrAdd(fileId, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);

        try
        {
            // Another request may have made the copy while this one waited.
            if (TryOpenCopy(path) is { } copied)
            {
                return copied;
            }

            var stream = await open(cancellationToken);

            if (stream is null || stream.CanSeek)
            {
                return stream;
            }

            Directory.CreateDirectory(_folder);

            var partial = path + "." + Guid.NewGuid().ToString("N") + ".part";

            try
            {
                await using (stream)
                await using (var target = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await stream.CopyToAsync(target, cancellationToken);
                }

                File.Move(partial, path, overwrite: true);
            }
            catch
            {
                TryDelete(partial);

                throw;
            }

            RemoveUnused();

            return TryOpenCopy(path);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Gets whether an id is one the protected store makes, so a crafted id can't climb out of the folder.
    /// </summary>
    /// <param name="fileId">The id.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal static bool IsValidId(string fileId)
        => !string.IsNullOrEmpty(fileId) && fileId.Length <= 64 && fileId.All(char.IsAsciiLetterOrDigit);

    private FileStream TryOpenCopy(string path)
    {
        try
        {
            var info = new FileInfo(path);

            if (!info.Exists)
            {
                return null;
            }

            var now = _clock.UtcNow;

            if (now - info.LastWriteTimeUtc > _touchInterval)
            {
                info.LastWriteTimeUtc = now;
            }

            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, useAsync: true);
        }
        catch (IOException exception)
        {
            _logger.LogWarning(exception, "The local copy of a package could not be opened; it is downloaded again.");

            return null;
        }
    }

    private void RemoveUnused()
    {
        try
        {
            var cutoff = _clock.UtcNow - UnusedLifetime;

            foreach (var file in new DirectoryInfo(_folder).EnumerateFiles())
            {
                if (file.LastWriteTimeUtc < cutoff)
                {
                    TryDelete(file.FullName);
                }
            }
        }
        catch (IOException exception)
        {
            _logger.LogWarning(exception, "Unused local copies of packages could not be removed.");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A copy that is open or already gone is left alone: the next clean-up removes it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above: removing a copy is best effort, and a copy left behind is only disk space.
        }
    }
}
