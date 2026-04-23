using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Services;

internal sealed class OrchardAIDataSourceIndexingWorkItem
{
    public AIDataSource DataSource { get; private init; }

    public IReadOnlyCollection<string> DocumentIds { get; private init; } = [];

    public string SourceIndexProfileName { get; private init; }

    public OrchardAIDataSourceIndexingWorkItemType Type { get; private init; }

    public static OrchardAIDataSourceIndexingWorkItem ForSyncDataSource(AIDataSource dataSource)
    {
        return new()
        {
            DataSource = dataSource,
            Type = OrchardAIDataSourceIndexingWorkItemType.SyncDataSource,
        };
    }

    public static OrchardAIDataSourceIndexingWorkItem ForDeleteDataSource(AIDataSource dataSource)
    {
        return new()
        {
            DataSource = dataSource,
            Type = OrchardAIDataSourceIndexingWorkItemType.DeleteDataSource,
        };
    }

    public static OrchardAIDataSourceIndexingWorkItem ForSyncSourceDocuments(
        string sourceIndexProfileName,
        IReadOnlyCollection<string> documentIds)
    {
        return new()
        {
            DocumentIds = documentIds,
            SourceIndexProfileName = sourceIndexProfileName,
            Type = OrchardAIDataSourceIndexingWorkItemType.SyncSourceDocuments,
        };
    }

    public static OrchardAIDataSourceIndexingWorkItem ForRemoveSourceDocuments(
        string sourceIndexProfileName,
        IReadOnlyCollection<string> documentIds)
    {
        return new()
        {
            DocumentIds = documentIds,
            SourceIndexProfileName = sourceIndexProfileName,
            Type = OrchardAIDataSourceIndexingWorkItemType.RemoveSourceDocuments,
        };
    }
}
