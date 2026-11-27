using System.Collections;
using Microsoft.Extensions.FileProviders;

namespace CrestApps.OrchardCore.Core.FileStorage;

/// <summary>
/// The entries of a folder inside a <see cref="TenantFolder"/> that the folder's rule resolves.
/// </summary>
internal sealed class TenantFolderContents : IDirectoryContents
{
    private readonly IReadOnlyList<IFileInfo> _entries;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantFolderContents"/> class.
    /// </summary>
    /// <param name="entries">The entries to list.</param>
    public TenantFolderContents(IReadOnlyList<IFileInfo> entries)
    {
        _entries = entries;
    }

    /// <inheritdoc/>
    public bool Exists => true;

    /// <inheritdoc/>
    public IEnumerator<IFileInfo> GetEnumerator()
        => _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
