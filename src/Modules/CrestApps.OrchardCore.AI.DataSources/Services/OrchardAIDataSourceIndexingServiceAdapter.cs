using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core.Services;

namespace CrestApps.OrchardCore.AI.DataSources.Services;

internal sealed class OrchardAIDataSourceIndexingServiceAdapter : IAIDataSourceIndexingService
{
    private readonly DataSourceIndexingService _indexingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardAIDataSourceIndexingServiceAdapter"/> class.
    /// </summary>
    /// <param name="indexingService">The indexing service.</param>
    public OrchardAIDataSourceIndexingServiceAdapter(DataSourceIndexingService indexingService)
    {
        _indexingService = indexingService;
    }

    /// <summary>
    /// Asynchronously performs the sync all operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task SyncAllAsync(CancellationToken cancellationToken = default)
        => _indexingService.SyncAllAsync(cancellationToken);

    /// <summary>
    /// Asynchronously performs the sync data source operation.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task SyncDataSourceAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
        => _indexingService.SyncDataSourceAsync(dataSource, cancellationToken);

    /// <summary>
    /// Removes the data source documents async.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task DeleteDataSourceDocumentsAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
        => _indexingService.DeleteDataSourceDocumentsAsync(dataSource, cancellationToken);

    /// <summary>
    /// Asynchronously performs the sync source documents operation.
    /// </summary>
    /// <param name="documentIds">The document ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task SyncSourceDocumentsAsync(
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
        => _indexingService.IndexDocumentsAsync(documentIds, cancellationToken);

    /// <summary>
    /// Asynchronously performs the sync source documents operation.
    /// </summary>
    /// <param name="sourceIndexProfileName">The source index profile name.</param>
    /// <param name="documentIds">The document ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task SyncSourceDocumentsAsync(
        string sourceIndexProfileName,
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
        => _indexingService.IndexDocumentsAsync(sourceIndexProfileName, documentIds, cancellationToken);

    /// <summary>
    /// Removes the source documents async.
    /// </summary>
    /// <param name="documentIds">The document ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task RemoveSourceDocumentsAsync(
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
        => _indexingService.RemoveDocumentsAsync(documentIds, cancellationToken);

    /// <summary>
    /// Removes the source documents async.
    /// </summary>
    /// <param name="sourceIndexProfileName">The source index profile name.</param>
    /// <param name="documentIds">The document ids.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task RemoveSourceDocumentsAsync(
        string sourceIndexProfileName,
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
        => _indexingService.RemoveDocumentsAsync(sourceIndexProfileName, documentIds, cancellationToken);
}
