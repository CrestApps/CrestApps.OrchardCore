using CrestApps.OrchardCore.Core.FileStorage;
using CrestApps.OrchardCore.Disbursements.Services;

namespace CrestApps.OrchardCore.Disbursements.Core.Services;

/// <summary>
/// Keeps batch files encrypted with the tenant's Data Protection keys in a tenant file store: a bank file holds every
/// payee's account number in clear text.
/// </summary>
public sealed class DisbursementFileStore : IDisbursementFileStore
{
    private readonly EncryptedFileStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementFileStore"/> class.
    /// </summary>
    /// <param name="store">The encrypted file store.</param>
    public DisbursementFileStore(EncryptedFileStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task WriteAsync(string batchId, byte[] content)
    {
        ArgumentException.ThrowIfNullOrEmpty(batchId);
        ArgumentNullException.ThrowIfNull(content);

        return _store.WriteAsync(batchId, content);
    }

    /// <inheritdoc />
    public Task<byte[]> ReadAsync(string batchId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(batchId);

        return _store.ReadAsync(batchId, cancellationToken);
    }
}
