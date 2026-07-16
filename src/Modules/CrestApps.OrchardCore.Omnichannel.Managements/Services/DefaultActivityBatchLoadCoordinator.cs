using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Default implementation of <see cref="IActivityBatchLoadCoordinator"/> that selects a source-specific
/// <see cref="IActivityBatchLoader"/> when one is registered, and otherwise falls back to the
/// <see cref="DefaultContactActivityBatchLoader"/>.
/// </summary>
public sealed class DefaultActivityBatchLoadCoordinator : IActivityBatchLoadCoordinator
{
    private readonly ICatalog<OmnichannelActivityBatch> _catalog;
    private readonly DefaultContactActivityBatchLoader _defaultLoader;
    private readonly ILogger _logger;
    private readonly Dictionary<string, IActivityBatchLoader> _loaders;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultActivityBatchLoadCoordinator"/> class.
    /// </summary>
    /// <param name="catalog">The activity batch catalog.</param>
    /// <param name="loaders">The registered source-specific activity batch loaders.</param>
    /// <param name="defaultLoader">The default contact-based activity batch loader used as a fallback.</param>
    /// <param name="logger">The logger.</param>
    public DefaultActivityBatchLoadCoordinator(
        ICatalog<OmnichannelActivityBatch> catalog,
        IEnumerable<IActivityBatchLoader> loaders,
        DefaultContactActivityBatchLoader defaultLoader,
        ILogger<DefaultActivityBatchLoadCoordinator> logger)
    {
        _catalog = catalog;
        _defaultLoader = defaultLoader;
        _logger = logger;
        _loaders = loaders
            .Where(loader => !string.IsNullOrEmpty(loader.Source))
            .GroupBy(loader => loader.Source, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task LoadAsync(
        string batchId,
        string loaderId,
        string loaderUserName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(batchId);

        var batch = await _catalog.FindByIdAsync(batchId, cancellationToken);

        if (batch is null)
        {
            _logger.LogError("Unable to load activities. No activity batch was found with the ID '{BatchId}'.", batchId);

            return;
        }

        if (batch.Status != OmnichannelActivityBatchStatus.Started)
        {
            throw new InvalidOperationException($"Unable to load activities for batch with the ID '{batch.ItemId}' since its status is not '{nameof(OmnichannelActivityBatchStatus.Started)}'.");
        }

        var loader = ResolveLoader(batch.Source);

        batch.Status = OmnichannelActivityBatchStatus.Loading;
        batch.TotalLoaded = 0;

        await _catalog.UpdateAsync(batch, cancellationToken);

        var context = new ActivityBatchLoadContext(batch, loaderId, loaderUserName);

        try
        {
            await loader.LoadAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while loading activities for the batch with ID '{BatchId}'.", batch.ItemId);

            batch.Status = OmnichannelActivityBatchStatus.New;

            await _catalog.UpdateAsync(batch, cancellationToken);
        }
    }

    private IActivityBatchLoader ResolveLoader(string source)
    {
        if (!string.IsNullOrEmpty(source) && _loaders.TryGetValue(source, out var loader))
        {
            return loader;
        }

        return _defaultLoader;
    }
}
