using CrestApps.Core;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Deployments;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Handlers;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Logging;
using OrchardCore.AzureAI;
using OrchardCore.AzureAI.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Infrastructure.Entities;

namespace CrestApps.OrchardCore.AI.Documents.AzureAI.Handlers;

/// <summary>
/// Handles events for AI document azure AI search index profile.
/// </summary>
public sealed class AIDocumentAzureAISearchIndexProfileHandler : AIDocumentIndexProfileHandlerBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIDocumentAzureAISearchIndexProfileHandler"/> class.
    /// </summary>
    /// <param name="deploymentManager">The deployment manager.</param>
    /// <param name="aiClientFactory">The ai client factory.</param>
    /// <param name="logger">The logger.</param>
    public AIDocumentAzureAISearchIndexProfileHandler(
        IAIDeploymentManager deploymentManager,
        IAIClientFactory aiClientFactory,
        ILogger<AIDocumentAzureAISearchIndexProfileHandler> logger)
        : base(AzureAISearchConstants.ProviderName, deploymentManager, aiClientFactory, logger)
    {
    }

    public override Task InitializingAsync(InitializingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task UpdatingAsync(UpdatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task CreatingAsync(CreatingContext<IndexProfile> context)
        => SetMappingAsync(context.Model);

    public override Task LoadedAsync(LoadedContext<IndexProfile> context)
    {
        if (!CanHandle(context.Model))
        {
            return Task.CompletedTask;
        }

        var metadata = context.Model.GetOrCreate<AzureAISearchIndexMetadata>();
        NormalizeIndexMappings(metadata);
        context.Model.Put(metadata);

        return Task.CompletedTask;
    }

    private async Task SetMappingAsync(IndexProfile indexProfile)
    {
        if (!CanHandle(indexProfile))
        {
            return;
        }

        var metadata = indexProfile.GetOrCreate<AzureAISearchIndexMetadata>();
        var embeddingDimensions = await GetEmbeddingDimensionsAsync(indexProfile);
        NormalizeIndexMappings(metadata, embeddingDimensions);

        metadata.VectorSearchMappings = new VectorSearchMappings
        {
            Profiles =
            [
                new VectorSearchProfileMap
                {
                    Name = "default",
                    AlgorithmConfigurationName = "default-hnsw",
                },
            ],
            Algorithms =
            [
                new VectorSearchAlgorithmMap
                {
                    Name = "default-hnsw",
                    Kind = VectorSearchAlgorithmMap.HnswKind,
                },
            ],
        };

        indexProfile.Put(metadata);
    }

    private static void NormalizeIndexMappings(
        AzureAISearchIndexMetadata metadata,
        int? embeddingDimensions = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var existingManagedMappings = new Dictionary<string, AzureAISearchIndexMap>(StringComparer.OrdinalIgnoreCase);
        var customMappings = new List<AzureAISearchIndexMap>();

        foreach (var mapping in metadata.IndexMappings)
        {
            mapping.IndexingKey ??= mapping.AzureFieldKey;

            var fieldKey = mapping.AzureFieldKey ?? mapping.IndexingKey;

            if (!IsManagedField(fieldKey))
            {
                customMappings.Add(mapping);
                continue;
            }

            if (!existingManagedMappings.ContainsKey(fieldKey))
            {
                existingManagedMappings[fieldKey] = mapping;
            }
        }

        metadata.IndexMappings.Clear();

        metadata.IndexMappings.Add(CreateOrUpdateIndexMap(existingManagedMappings, AIConstants.ColumnNames.ChunkId, DocumentIndex.Types.Text, map =>
        {
            map.IsKey = true;
            map.IsFilterable = true;
        }));
        metadata.IndexMappings.Add(CreateOrUpdateIndexMap(existingManagedMappings, AIConstants.ColumnNames.DocumentId, DocumentIndex.Types.Text, map => map.IsFilterable = true));
        metadata.IndexMappings.Add(CreateOrUpdateIndexMap(existingManagedMappings, AIConstants.ColumnNames.Content, DocumentIndex.Types.Text, map => map.IsSearchable = true));
        metadata.IndexMappings.Add(CreateOrUpdateIndexMap(existingManagedMappings, AIConstants.ColumnNames.FileName, DocumentIndex.Types.Text, map => map.IsFilterable = true));
        metadata.IndexMappings.Add(CreateOrUpdateIndexMap(existingManagedMappings, AIConstants.ColumnNames.ReferenceId, DocumentIndex.Types.Text, map => map.IsFilterable = true));
        metadata.IndexMappings.Add(CreateOrUpdateIndexMap(existingManagedMappings, AIConstants.ColumnNames.ReferenceType, DocumentIndex.Types.Text, map => map.IsFilterable = true));
        metadata.IndexMappings.Add(CreateOrUpdateIndexMap(existingManagedMappings, AIConstants.ColumnNames.ChunkIndex, DocumentIndex.Types.Integer));
        metadata.IndexMappings.Add(CreateOrUpdateIndexMap(existingManagedMappings, AIConstants.ColumnNames.Embedding, DocumentIndex.Types.Vector, map =>
        {
            map.IsSearchable = true;
            map.VectorInfo = new AzureAISearchIndexMapVectorInfo
            {
                Dimensions = embeddingDimensions ?? map.VectorInfo?.Dimensions ?? 1536,
                VectorSearchConfiguration = "default",
            };
        }));

        foreach (var customMapping in customMappings)
        {
            metadata.IndexMappings.Add(customMapping);
        }
    }

    private static AzureAISearchIndexMap CreateOrUpdateIndexMap(
        Dictionary<string, AzureAISearchIndexMap> existingMappings,
        string fieldKey,
        DocumentIndex.Types type,
        Action<AzureAISearchIndexMap> configure = null)
    {
        if (!existingMappings.TryGetValue(fieldKey, out var indexMap))
        {
            indexMap = new AzureAISearchIndexMap();
        }

        indexMap.IndexingKey = fieldKey;
        indexMap.AzureFieldKey = fieldKey;
        indexMap.Type = type;
        indexMap.IsKey = false;
        indexMap.IsCollection = false;
        indexMap.IsSuggester = false;
        indexMap.IsFilterable = false;
        indexMap.IsSortable = false;
        indexMap.IsHidden = false;
        indexMap.IsFacetable = false;
        indexMap.IsSearchable = false;
        indexMap.VectorInfo = null;

        configure?.Invoke(indexMap);

        return indexMap;
    }

    private static bool IsManagedField(string fieldKey)
    {
        return string.Equals(fieldKey, AIConstants.ColumnNames.ChunkId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fieldKey, AIConstants.ColumnNames.DocumentId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fieldKey, AIConstants.ColumnNames.Content, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fieldKey, AIConstants.ColumnNames.FileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fieldKey, AIConstants.ColumnNames.ReferenceId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fieldKey, AIConstants.ColumnNames.ReferenceType, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fieldKey, AIConstants.ColumnNames.ChunkIndex, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fieldKey, AIConstants.ColumnNames.Embedding, StringComparison.OrdinalIgnoreCase);
    }
}
