using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core.Services;

namespace CrestApps.OrchardCore.AI.DataSources.Services;

internal sealed class OrchardAIDataSourceIndexingServiceAdapter : IAIDataSourceIndexingService
{
    private readonly DataSourceIndexingService _indexingService;

    public OrchardAIDataSourceIndexingServiceAdapter(DataSourceIndexingService indexingService)
    {
        _indexingService = indexingService;
    }

    public Task SyncAllAsync(CancellationToken cancellationToken = default)
        => _indexingService.SyncAllAsync(cancellationToken);

    public Task SyncDataSourceAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
        => _indexingService.SyncDataSourceAsync(dataSource, cancellationToken);

    public Task DeleteDataSourceDocumentsAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
        => _indexingService.DeleteDataSourceDocumentsAsync(dataSource, cancellationToken);

    public Task SyncSourceDocumentsAsync(
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
        => _indexingService.IndexDocumentsAsync(documentIds, cancellationToken);

    public Task SyncSourceDocumentsAsync(
        string sourceIndexProfileName,
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
        => _indexingService.IndexDocumentsAsync(sourceIndexProfileName, documentIds, cancellationToken);

    public Task RemoveSourceDocumentsAsync(
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
        => _indexingService.RemoveDocumentsAsync(documentIds, cancellationToken);

    public Task RemoveSourceDocumentsAsync(
        string sourceIndexProfileName,
        IEnumerable<string> documentIds,
        CancellationToken cancellationToken = default)
        => _indexingService.RemoveDocumentsAsync(sourceIndexProfileName, documentIds, cancellationToken);
}
