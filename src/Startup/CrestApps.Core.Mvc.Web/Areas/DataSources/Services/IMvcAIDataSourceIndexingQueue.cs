using CrestApps.Core.AI.Models;

namespace CrestApps.Core.Mvc.Web.Areas.DataSources.Services;

public interface IMvcAIDataSourceIndexingQueue
{
    ValueTask QueueSyncDataSourceAsync(AIDataSource dataSource, CancellationToken cancellationToken = default);

    ValueTask QueueDeleteDataSourceAsync(AIDataSource dataSource, CancellationToken cancellationToken = default);

    ValueTask QueueSyncSourceDocumentsAsync(IReadOnlyCollection<string> documentIds, CancellationToken cancellationToken = default);

    ValueTask QueueRemoveSourceDocumentsAsync(IReadOnlyCollection<string> documentIds, CancellationToken cancellationToken = default);
}
