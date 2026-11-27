using System.ComponentModel.DataAnnotations;
using CrestApps.Core.AI.Indexing;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.AI.FileTransfer.Core.Services;

/// <summary>
/// Holds a remote-file ingestion connector of CrestApps.Core (FTP, SFTP) to the <see cref="FileTransferEgressPolicy"/>:
/// a file source is refused when it is saved through the catalog (the editor, the API, a recipe), and every discovery
/// and fetch checks the server and proxy before the package connects, and hands the package a copy of the source that
/// names the addresses that were checked (review 3).
/// </summary>
internal sealed class EgressGuardedIngestionConnector : IIngestionConnector
{
    private readonly IIngestionConnector _inner;
    private readonly FileTransferEgressPolicy _policy;
    private readonly Func<IngestionSource, IEnumerable<(string Field, string Host)>> _hosts;
    private readonly Func<IngestionSource, FileTransferEgressPolicy, string> _refusal;
    private readonly Action<IngestionSource, FileTransferEgressPolicy> _prepare;

    public EgressGuardedIngestionConnector(
        IIngestionConnector inner,
        FileTransferEgressPolicy policy,
        Func<IngestionSource, IEnumerable<(string Field, string Host)>> hosts,
        Func<IngestionSource, FileTransferEgressPolicy, string> refusal = null,
        Action<IngestionSource, FileTransferEgressPolicy> prepare = null)
    {
        _inner = inner;
        _policy = policy;
        _hosts = hosts;
        _refusal = refusal;
        _prepare = prepare;
    }

    public string Name => _inner.Name;

    public async ValueTask ValidateAsync(IngestionSource source, ValidationResultDetails result, CancellationToken cancellationToken = default)
    {
        await _inner.ValidateAsync(source, result, cancellationToken);

        foreach (var (field, host) in _hosts(source))
        {
            if (!_policy.IsAllowed(host))
            {
                result.Fail(new ValidationResult(FileTransferEgress.RefusedMessage, [field]));
            }
        }

        if (_refusal?.Invoke(source, _policy) is { } refusedField)
        {
            result.Fail(new ValidationResult(FileTransferEgress.DataConnectionRefusedMessage, [refusedField]));
        }
    }

    public Task<IngestionDiscoveryResult> DiscoverAsync(IngestionSource source, string continuationToken = null, CancellationToken cancellationToken = default)
        => _inner.DiscoverAsync(PrepareConnection(source), continuationToken, cancellationToken);

    public Task<IngestionItemContent> FetchAsync(IngestionSource source, string itemId, CancellationToken cancellationToken = default)
        => _inner.FetchAsync(PrepareConnection(source), itemId, cancellationToken);

    // The source the package connects with: a copy, so the checked addresses never replace the names the source stores
    // (the record a catalog returns may be the one it keeps).
    private IngestionSource PrepareConnection(IngestionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (var (_, host) in _hosts(source))
        {
            _policy.EnsureAllowed(host);
        }

        if (_prepare is null)
        {
            return source;
        }

        var copy = (IngestionSource)Activator.CreateInstance(source.GetType());
        source.CopyTo(copy);
        _prepare(copy, _policy);

        return copy;
    }
}
