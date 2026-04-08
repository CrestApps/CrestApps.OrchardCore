using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.Indexing;

public sealed class AIMemorySearchIndexProfileHandler : EmbeddingSearchIndexProfileHandlerBase
{
    private const string _memoryIdFieldName = "memoryId";
    private const string _userIdFieldName = "userId";
    private const string _nameFieldName = "name";
    private const string _descriptionFieldName = "description";
    private const string _contentFieldName = "content";
    private const string _updatedUtcFieldName = "updatedUtc";
    private const string _embeddingFieldName = "embedding";

    public AIMemorySearchIndexProfileHandler(
        ICatalog<AIDeployment> deploymentCatalog,
        IAIClientFactory aiClientFactory,
        ILogger<AIMemorySearchIndexProfileHandler> logger)
        : base(IndexProfileTypes.AIMemory, deploymentCatalog, aiClientFactory, logger)
    {
    }

    protected override IReadOnlyCollection<SearchIndexField> BuildFields(int vectorDimensions)
        =>
        [
            new SearchIndexField
            {
                Name = _memoryIdFieldName,
                FieldType = SearchFieldType.Keyword,
                IsKey = true,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = _userIdFieldName,
                FieldType = SearchFieldType.Keyword,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = _nameFieldName,
                FieldType = SearchFieldType.Text,
                IsSearchable = true,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = _descriptionFieldName,
                FieldType = SearchFieldType.Text,
                IsSearchable = true,
            },
            new SearchIndexField
            {
                Name = _contentFieldName,
                FieldType = SearchFieldType.Text,
                IsSearchable = true,
            },
            new SearchIndexField
            {
                Name = _updatedUtcFieldName,
                FieldType = SearchFieldType.DateTime,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = _embeddingFieldName,
                FieldType = SearchFieldType.Vector,
                VectorDimensions = vectorDimensions,
            },
        ];
}
