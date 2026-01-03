using CrestApps.OrchardCore.AI.Core;
using Elastic.Clients.Elasticsearch.Mapping;
using OrchardCore.Entities;
using OrchardCore.Indexing.Core.Handlers;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Search.Elasticsearch;
using OrchardCore.Search.Elasticsearch.Core.Models;
using OrchardCore.Search.Elasticsearch.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;


public sealed class ChatInteractionElasticsearchIndexProfileHandler : IndexProfileHandlerBase
{
    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    private static Task SetMappingAsync(IndexProfile indexProfile)
    {
        if (!CanHandle(indexProfile))
        {
            return Task.CompletedTask;
        }

        var metadata = indexProfile.As<ElasticsearchIndexMetadata>();

        metadata.IndexMappings ??= new ElasticsearchIndexMap();
        metadata.IndexMappings.Mapping ??= new TypeMapping();
        metadata.IndexMappings.Mapping.Properties ??= [];
        metadata.IndexMappings.Mapping.Meta ??= new Dictionary<string, object>();

        metadata.IndexMappings.KeyFieldName = "documentId";
        metadata.IndexMappings.Mapping.Properties["documentId"] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties["interactionId"] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties["fileName"] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties["chunks"] = new NestedProperty()
        {
            Properties = new Properties()
            {
                { "chunkId", new KeywordProperty() },
                { "text", new TextProperty() },
                { "embedding", new DenseVectorProperty
                    {
                        Dims = 1536,
                        Index = true,
                    }
                },
                { "chunkIndex", new IntegerNumberProperty() },
            },
        };


        indexProfile.Put(metadata);

        return Task.CompletedTask;
    }

    private static bool CanHandle(IndexProfile indexProfile)
    {
        return string.Equals(ElasticsearchConstants.ProviderName, indexProfile.ProviderName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(ChatInteractionsConstants.IndexingTaskType, indexProfile.Type, StringComparison.OrdinalIgnoreCase);
    }
}
