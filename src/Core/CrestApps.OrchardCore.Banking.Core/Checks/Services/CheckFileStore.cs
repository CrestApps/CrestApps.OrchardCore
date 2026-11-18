using CrestApps.OrchardCore.Banking.Checks.Services;
using CrestApps.OrchardCore.Core.FileStorage;

namespace CrestApps.OrchardCore.Banking.Checks.Core.Services;

/// <summary>
/// Keeps the files of check layouts encrypted with the tenant's Data Protection keys in a tenant file store.
/// </summary>
public sealed class CheckFileStore : ICheckFileStore
{
    private readonly EncryptedFileStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckFileStore"/> class.
    /// </summary>
    /// <param name="store">The encrypted file store.</param>
    public CheckFileStore(EncryptedFileStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task WriteAsync(string layoutId, string kind, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return _store.WriteAsync(Key(layoutId, kind), content);
    }

    /// <inheritdoc />
    public Task<byte[]> ReadAsync(string layoutId, string kind, CancellationToken cancellationToken = default)
        => _store.ReadAsync(Key(layoutId, kind), cancellationToken);

    /// <inheritdoc />
    public Task DeleteAsync(string layoutId, string kind)
        => _store.DeleteAsync(Key(layoutId, kind));

    private static string Key(string layoutId, string kind)
    {
        ArgumentException.ThrowIfNullOrEmpty(layoutId);
        ArgumentException.ThrowIfNullOrEmpty(kind);

        return layoutId + "/" + kind;
    }
}
