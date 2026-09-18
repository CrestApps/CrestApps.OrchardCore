using CrestApps.Core;
using CrestApps.Core.AI.FileSources.Connectors;
using CrestApps.Core.AI.Indexing;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.DataSources.FileSources.Services;

/// <summary>
/// The framework's local-folder connector, confined to this tenant's own file-source folder.
/// </summary>
/// <remarks>
/// <para>
/// The reading is entirely the framework's: this resolves the configured folder against the tenant's own
/// boundary and hands the framework a record whose path is the resolved one, or refuses outright.
/// </para>
/// <para>
/// It runs at ingestion time and not only when a file source is saved, because a stored record is not
/// evidence of anything: it may predate the rule, or have been written straight into the database, or name
/// a folder that has since been replaced by a link pointing somewhere else. A check that only happens on
/// the way in is a check an attacker walks around.
/// </para>
/// </remarks>
internal sealed class TenantLocalFolderIngestionConnector : IIngestionConnector
{
    private readonly LocalFolderIngestionConnector _inner;
    private readonly ITenantFileSourceRoot _tenantRoot;
    private readonly ILogger<TenantLocalFolderIngestionConnector> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantLocalFolderIngestionConnector"/> class.
    /// </summary>
    /// <param name="inner">The framework connector that does the reading.</param>
    /// <param name="tenantRoot">This tenant's file-source folder.</param>
    /// <param name="logger">The logger.</param>
    public TenantLocalFolderIngestionConnector(
        LocalFolderIngestionConnector inner,
        ITenantFileSourceRoot tenantRoot,
        ILogger<TenantLocalFolderIngestionConnector> logger)
    {
        _inner = inner;
        _tenantRoot = tenantRoot;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => LocalFolderIngestionConnector.ConnectorName;

    /// <inheritdoc />
    public async ValueTask ValidateAsync(WebCrawler settings, ValidationResultDetails result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(result);

        if (!TryConfine(settings, out var confined, out var reason))
        {
            result.Fail(new System.ComponentModel.DataAnnotations.ValidationResult(
                reason,
                [nameof(LocalFolderIndexerMetadata.RootPath)]));

            return;
        }

        await _inner.ValidateAsync(confined, result, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IngestionDiscoveryResult> DiscoverAsync(WebCrawler settings, string continuationToken = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!TryConfine(settings, out var confined, out var reason))
        {
            _logger.LogWarning(
                "File source '{FileSourceId}' was not listed: {Reason}",
                settings.ItemId,
                reason);

            return Task.FromResult(new IngestionDiscoveryResult([], IsComplete: false, reason));
        }

        return _inner.DiscoverAsync(confined, continuationToken, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IngestionItemContent> FetchAsync(WebCrawler settings, string itemId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!TryConfine(settings, out var confined, out var reason))
        {
            _logger.LogWarning(
                "File source '{FileSourceId}' was not read: {Reason}",
                settings.ItemId,
                reason);

            return Task.FromResult<IngestionItemContent>(null);
        }

        return _inner.FetchAsync(confined, itemId, cancellationToken);
    }

    /// <summary>
    /// Copies the record with its folder replaced by the resolved path inside this tenant's folder.
    /// </summary>
    /// <param name="settings">The stored file source.</param>
    /// <param name="confined">A copy whose folder is the resolved one.</param>
    /// <param name="reason">Why the folder was refused, when it was.</param>
    /// <returns><see langword="true"/> when the folder is inside this tenant's folder.</returns>
    /// <remarks>
    /// A copy rather than a mutation, so nothing writes a host-absolute path back onto the stored record.
    /// </remarks>
    private bool TryConfine(WebCrawler settings, out WebCrawler confined, out string reason)
    {
        confined = null;

        var metadata = settings.GetOrCreate<LocalFolderIndexerMetadata>();

        if (!_tenantRoot.TryResolve(metadata.RootPath, out var resolved, out reason))
        {
            return false;
        }

        confined = settings.Clone();

        confined.Put(new LocalFolderIndexerMetadata
        {
            RootPath = resolved,
            SearchPattern = metadata.SearchPattern,
            Recursive = metadata.Recursive,
            MaxItems = metadata.MaxItems,
        });

        return true;
    }
}
