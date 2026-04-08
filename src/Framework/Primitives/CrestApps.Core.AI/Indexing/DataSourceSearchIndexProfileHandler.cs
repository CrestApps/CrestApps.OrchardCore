using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.Indexing;

public sealed class DataSourceSearchIndexProfileHandler : EmbeddingSearchIndexProfileHandlerBase
{
    public DataSourceSearchIndexProfileHandler(
        ICatalog<AIDeployment> deploymentCatalog,
        IAIClientFactory aiClientFactory,
        ILogger<DataSourceSearchIndexProfileHandler> logger)
        : base(IndexProfileTypes.DataSource, deploymentCatalog, aiClientFactory, logger)
    {
    }

    protected override IReadOnlyCollection<SearchIndexField> BuildFields(int vectorDimensions)
        =>
        [
            new SearchIndexField
            {
                Name = DataSourceConstants.ColumnNames.ChunkId,
                FieldType = SearchFieldType.Keyword,
                IsKey = true,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = DataSourceConstants.ColumnNames.ReferenceId,
                FieldType = SearchFieldType.Keyword,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = DataSourceConstants.ColumnNames.DataSourceId,
                FieldType = SearchFieldType.Keyword,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = DataSourceConstants.ColumnNames.ReferenceType,
                FieldType = SearchFieldType.Keyword,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = DataSourceConstants.ColumnNames.ChunkIndex,
                FieldType = SearchFieldType.Integer,
            },
            new SearchIndexField
            {
                Name = DataSourceConstants.ColumnNames.Title,
                FieldType = SearchFieldType.Text,
                IsSearchable = true,
            },
            new SearchIndexField
            {
                Name = DataSourceConstants.ColumnNames.Content,
                FieldType = SearchFieldType.Text,
                IsSearchable = true,
            },
            new SearchIndexField
            {
                Name = DataSourceConstants.ColumnNames.Timestamp,
                FieldType = SearchFieldType.DateTime,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = DataSourceConstants.ColumnNames.Embedding,
                FieldType = SearchFieldType.Vector,
                VectorDimensions = vectorDimensions,
            },
        ];
}
