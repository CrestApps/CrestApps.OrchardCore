using System.Threading.Channels;
using CrestApps.AI.Models;

namespace CrestApps.Mvc.Web.Areas.DataSources.Services;

public sealed class MvcAIDataSourceIndexingQueue : IMvcAIDataSourceIndexingQueue
{
    private readonly Channel<MvcAIDataSourceIndexingWorkItem> _channel = Channel.CreateUnbounded<MvcAIDataSourceIndexingWorkItem>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    public ValueTask QueueSyncDataSourceAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        return _channel.Writer.WriteAsync(
            MvcAIDataSourceIndexingWorkItem.ForSyncDataSource(dataSource.Clone()),
            cancellationToken);
    }

    public ValueTask QueueDeleteDataSourceAsync(AIDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        return _channel.Writer.WriteAsync(
            MvcAIDataSourceIndexingWorkItem.ForDeleteDataSource(dataSource.Clone()),
            cancellationToken);
    }

    public ValueTask QueueSyncSourceDocumentsAsync(IReadOnlyCollection<string> documentIds, CancellationToken cancellationToken = default)
        => QueueDocumentIdsAsync(documentIds, MvcAIDataSourceIndexingWorkItem.ForSyncSourceDocuments, cancellationToken);

    public ValueTask QueueRemoveSourceDocumentsAsync(IReadOnlyCollection<string> documentIds, CancellationToken cancellationToken = default)
        => QueueDocumentIdsAsync(documentIds, MvcAIDataSourceIndexingWorkItem.ForRemoveSourceDocuments, cancellationToken);

    internal IAsyncEnumerable<MvcAIDataSourceIndexingWorkItem> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    private ValueTask QueueDocumentIdsAsync(
        IReadOnlyCollection<string> documentIds,
        Func<IReadOnlyCollection<string>, MvcAIDataSourceIndexingWorkItem> factory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentIds);

        var ids = documentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ids.Length == 0)
        {
            return ValueTask.CompletedTask;
        }

        return _channel.Writer.WriteAsync(factory(ids), cancellationToken);
    }
}

internal sealed class MvcAIDataSourceIndexingWorkItem
{
    public AIDataSource DataSource { get; private init; }

    public IReadOnlyCollection<string> DocumentIds { get; private init; } = [];

    public MvcAIDataSourceIndexingWorkItemType Type { get; private init; }

    public static MvcAIDataSourceIndexingWorkItem ForSyncDataSource(AIDataSource dataSource) =>
        new()
        {
            DataSource = dataSource,
            Type = MvcAIDataSourceIndexingWorkItemType.SyncDataSource,
        };

    public static MvcAIDataSourceIndexingWorkItem ForDeleteDataSource(AIDataSource dataSource) =>
        new()
        {
            DataSource = dataSource,
            Type = MvcAIDataSourceIndexingWorkItemType.DeleteDataSource,
        };

    public static MvcAIDataSourceIndexingWorkItem ForSyncSourceDocuments(IReadOnlyCollection<string> documentIds) =>
        new()
        {
            DocumentIds = documentIds,
            Type = MvcAIDataSourceIndexingWorkItemType.SyncSourceDocuments,
        };

    public static MvcAIDataSourceIndexingWorkItem ForRemoveSourceDocuments(IReadOnlyCollection<string> documentIds) =>
        new()
        {
            DocumentIds = documentIds,
            Type = MvcAIDataSourceIndexingWorkItemType.RemoveSourceDocuments,
        };
}

internal enum MvcAIDataSourceIndexingWorkItemType
{
    SyncDataSource,
    DeleteDataSource,
    SyncSourceDocuments,
    RemoveSourceDocuments,
}
