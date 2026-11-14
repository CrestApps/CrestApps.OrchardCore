using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Primitives;

namespace CrestApps.OrchardCore.Core.FileStorage;

/// <summary>
/// A read-only file provider over a <see cref="TenantFolder"/>. Every path is resolved by the folder's own rule, so an
/// absolute path, a path that climbs out with <c>..</c> and a link that leaves the folder all answer "not found".
/// </summary>
public sealed class TenantFolderFileProvider : IFileProvider
{
    private readonly TenantFolder _folder;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantFolderFileProvider"/> class.
    /// </summary>
    /// <param name="folder">The tenant folder the provider reads.</param>
    public TenantFolderFileProvider(TenantFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        _folder = folder;
    }

    /// <inheritdoc/>
    public IFileInfo GetFileInfo(string subpath)
    {
        if (string.IsNullOrWhiteSpace(subpath) ||
            !_folder.TryResolve(subpath.TrimStart('/', '\\'), out var resolved, out _, allowAbsolute: false) ||
            !File.Exists(resolved))
        {
            return new NotFoundFileInfo(subpath ?? string.Empty);
        }

        return new PhysicalFileInfo(new FileInfo(resolved));
    }

    /// <inheritdoc/>
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        if (!_folder.TryResolve((subpath ?? string.Empty).TrimStart('/', '\\'), out var resolved, out _, allowAbsolute: false) ||
            !Directory.Exists(resolved))
        {
            return NotFoundDirectoryContents.Singleton;
        }

        using var provider = new PhysicalFileProvider(resolved);

        return provider.GetDirectoryContents(string.Empty);
    }

    /// <inheritdoc/>
    public IChangeToken Watch(string filter)
        => NullChangeToken.Singleton;
}
