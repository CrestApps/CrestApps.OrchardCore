using System.ComponentModel.DataAnnotations;
using CrestApps.Core.AI.Indexing;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.AI.FileTransfer.Core.Services;

/// <summary>
/// Holds a remote-file ingestion connector of CrestApps.Core (FTP, SFTP) to the <see cref="FileTransferEgressPolicy"/>:
/// a file source is refused when it is saved through the catalog (the editor, the API, a recipe), and every discovery
/// and fetch checks the server and proxy before the package connects.
/// </summary>
internal sealed class EgressGuardedIngestionConnector : IIngestionConnector
{
    private readonly IIngestionConnector _inner;
    private readonly FileTransferEgressPolicy _policy;
    private readonly Func<IngestionSource, IEnumerable<(string Field, string Host)>> _hosts;

    public EgressGuardedIngestionConnector(IIngestionConnector inner, FileTransferEgressPolicy policy, Func<IngestionSource, IEnumerable<(string Field, string Host)>> hosts)
    {
        _inner = inner;
        _policy = policy;
        _hosts = hosts;
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
    }

    public Task<IngestionDiscoveryResult> DiscoverAsync(IngestionSource source, string continuationToken = null, CancellationToken cancellationToken = default)
    {
        EnsureAllowed(source);

        return _inner.DiscoverAsync(source, continuationToken, cancellationToken);
    }

    public Task<IngestionItemContent> FetchAsync(IngestionSource source, string itemId, CancellationToken cancellationToken = default)
    {
        EnsureAllowed(source);

        return _inner.FetchAsync(source, itemId, cancellationToken);
    }

    private void EnsureAllowed(IngestionSource source)
    {
        foreach (var (_, host) in _hosts(source))
        {
            _policy.EnsureAllowed(host);
        }
    }
}
