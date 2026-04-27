using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.DataSources.Services;

internal sealed class OrchardAIDataSourceIndexingWorkItem
{
    /// <summary>
    /// Gets or sets the data source.
    /// </summary>
    public AIDataSource DataSource { get; private init; }

    /// <summary>
    /// Gets or sets the document ids.
    /// </summary>
    public IReadOnlyCollection<string> DocumentIds { get; private init; } = [];

    /// <summary>
    /// Gets or sets the source index profile name.
    /// </summary>
    public string SourceIndexProfileName { get; private init; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public OrchardAIDataSourceIndexingWorkItemType Type { get; private init; }

    /// <summary>
    /// Performs the for sync data source operation.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    public static OrchardAIDataSourceIndexingWorkItem ForSyncDataSource(AIDataSource dataSource)
    {
        return new()
        {
            DataSource = dataSource,
            Type = OrchardAIDataSourceIndexingWorkItemType.SyncDataSource,
        };
    }

    /// <summary>
    /// Performs the for delete data source operation.
    /// </summary>
    /// <param name="dataSource">The data source.</param>
    public static OrchardAIDataSourceIndexingWorkItem ForDeleteDataSource(AIDataSource dataSource)
    {
        return new()
        {
            DataSource = dataSource,
            Type = OrchardAIDataSourceIndexingWorkItemType.DeleteDataSource,
        };
    }

    /// <summary>
    /// Performs the for sync source documents operation.
    /// </summary>
    /// <param name="sourceIndexProfileName">The source index profile name.</param>
    /// <param name="documentIds">The document ids.</param>
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

    /// <summary>
    /// Performs the for remove source documents operation.
    /// </summary>
    /// <param name="sourceIndexProfileName">The source index profile name.</param>
    /// <param name="documentIds">The document ids.</param>
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
