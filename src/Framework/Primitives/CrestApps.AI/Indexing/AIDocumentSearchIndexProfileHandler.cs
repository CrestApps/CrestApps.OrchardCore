using CrestApps.AI.Clients;
using CrestApps.AI.Models;
using CrestApps.Infrastructure;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.Models;
using CrestApps.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.AI.Indexing;

public sealed class AIDocumentSearchIndexProfileHandler : EmbeddingSearchIndexProfileHandlerBase
{
    public AIDocumentSearchIndexProfileHandler(
        ICatalog<AIDeployment> deploymentCatalog,
        IAIClientFactory aiClientFactory,
        ILogger<AIDocumentSearchIndexProfileHandler> logger)
        : base(IndexProfileTypes.AIDocuments, deploymentCatalog, aiClientFactory, logger)
    {
    }

    protected override IReadOnlyCollection<SearchIndexField> BuildFields(int vectorDimensions)
        =>
        [
            new SearchIndexField
            {
                Name = DocumentIndexConstants.ColumnNames.ChunkId,
                FieldType = SearchFieldType.Keyword,
                IsKey = true,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = DocumentIndexConstants.ColumnNames.DocumentId,
                FieldType = SearchFieldType.Keyword,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = DocumentIndexConstants.ColumnNames.Content,
                FieldType = SearchFieldType.Text,
                IsSearchable = true,
            },
            new SearchIndexField
            {
                Name = DocumentIndexConstants.ColumnNames.FileName,
                FieldType = SearchFieldType.Keyword,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = DocumentIndexConstants.ColumnNames.ReferenceId,
                FieldType = SearchFieldType.Keyword,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = DocumentIndexConstants.ColumnNames.ReferenceType,
                FieldType = SearchFieldType.Keyword,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = DocumentIndexConstants.ColumnNames.ChunkIndex,
                FieldType = SearchFieldType.Integer,
            },
            new SearchIndexField
            {
                Name = DocumentIndexConstants.ColumnNames.Embedding,
                FieldType = SearchFieldType.Vector,
                VectorDimensions = vectorDimensions,
            },
        ];
}
