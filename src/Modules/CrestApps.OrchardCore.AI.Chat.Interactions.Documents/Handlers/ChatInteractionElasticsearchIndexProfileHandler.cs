using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using Elastic.Clients.Elasticsearch.Mapping;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;
using OrchardCore.Search.Elasticsearch;
using OrchardCore.Search.Elasticsearch.Core.Models;
using OrchardCore.Search.Elasticsearch.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Handlers;


public sealed class ChatInteractionElasticsearchIndexProfileHandler : ChatInteractionsIndexProfileHandlerBase
{
    public ChatInteractionElasticsearchIndexProfileHandler(IAIClientFactory aiClientFactory)
        : base(ElasticsearchConstants.ProviderName, aiClientFactory)
    {
    }

    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    private async Task SetMappingAsync(IndexProfile indexProfile)
    {
        if (!CanHandle(indexProfile))
        {
            return;
        }

        var metadata = indexProfile.As<ElasticsearchIndexMetadata>();

        metadata.IndexMappings ??= new ElasticsearchIndexMap();
        metadata.IndexMappings.Mapping ??= new TypeMapping();
        metadata.IndexMappings.Mapping.Properties ??= [];

        // Get embedding connection from index profile metadata
        var interactionMetadata = indexProfile.As<ChatInteractionIndexProfileMetadata>();

        // Dynamically determine embedding dimensions by generating a sample embedding using the configured connection
        var embeddingDimensions = await GetEmbeddingDimensionsAsync(interactionMetadata);

        metadata.IndexMappings.KeyFieldName = ChatInteractionsConstants.ColumnNames.DocumentId;
        metadata.IndexMappings.Mapping.Properties[ChatInteractionsConstants.ColumnNames.DocumentId] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[ChatInteractionsConstants.ColumnNames.Text] = new TextProperty();
        metadata.IndexMappings.Mapping.Properties[ChatInteractionsConstants.ColumnNames.InteractionId] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[ChatInteractionsConstants.ColumnNames.FileName] = new KeywordProperty();
        metadata.IndexMappings.Mapping.Properties[ChatInteractionsConstants.ColumnNames.Chunks] = new NestedProperty()
        {
            Properties = new Properties()
            {
                { ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Text, new TextProperty() },
                { ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Embedding, new DenseVectorProperty
                    {
                        Dims = embeddingDimensions,
                        Index = true,
                    }
                },
                { ChatInteractionsConstants.ColumnNames.ChunksColumnNames.Index, new IntegerNumberProperty() },
            },
        };

        indexProfile.Put(metadata);
    }
}
