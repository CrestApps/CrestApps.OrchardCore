using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Services;

public interface IAIDataSourceIndexingService
{
    Task SyncAllAsync(CancellationToken cancellationToken = default);

    Task SyncDataSourceAsync(AIDataSource dataSource, CancellationToken cancellationToken = default);

    Task SyncSourceDocumentsAsync(IEnumerable<string> documentIds, CancellationToken cancellationToken = default);

    Task RemoveSourceDocumentsAsync(IEnumerable<string> documentIds, CancellationToken cancellationToken = default);

    Task DeleteDataSourceDocumentsAsync(AIDataSource dataSource, CancellationToken cancellationToken = default);
}
