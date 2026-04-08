using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure;

using CrestApps.Core.Infrastructure.Indexing;

using CrestApps.Core.Infrastructure.Indexing.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.Mvc.Web.Areas.Indexing.Services;

/// <summary>
/// Indexes MVC-uploaded AI document chunks into the configured AI Documents search index.
/// </summary>
public sealed class MvcAIDocumentIndexingService
{
    private readonly InteractionDocumentOptions _options;
    private readonly ISearchIndexProfileStore _indexProfileStore;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MvcAIDocumentIndexingService> _logger;

    public MvcAIDocumentIndexingService(
        IOptions<InteractionDocumentOptions> options,
        ISearchIndexProfileStore indexProfileStore,
        IServiceProvider serviceProvider,
        ILogger<MvcAIDocumentIndexingService> logger)
    {
        _options = options.Value;
        _indexProfileStore = indexProfileStore;
        _serviceProvider = serviceProvider;

        _logger = logger;
    }

    public async Task IndexAsync(AIDocument document, IReadOnlyCollection<AIDocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(chunks);

        var indexedChunks = chunks
            .Where(chunk => chunk.Embedding is { Length: > 0 } && !string.IsNullOrWhiteSpace(chunk.Content))
            .ToList();

        if (indexedChunks.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Skipping AI document indexing for '{FileName}' because no embedded chunks were created.", document.FileName);
            }

            return;
        }

        try
        {
            var indexProfile = await GetConfiguredIndexProfileAsync(cancellationToken);

            if (indexProfile == null)
            {
                return;
            }

            var indexManager = _serviceProvider.GetKeyedService<ISearchIndexManager>(indexProfile.ProviderName);
            var documentManager = _serviceProvider.GetKeyedService<ISearchDocumentManager>(indexProfile.ProviderName);

            if (indexManager == null || documentManager == null)
            {
                _logger.LogWarning("Skipping AI document indexing because provider '{ProviderName}' is not configured for search indexing.", indexProfile.ProviderName);

                return;
            }

            if (!await indexManager.ExistsAsync(indexProfile, cancellationToken))
            {
                var dimensions = indexedChunks[0].Embedding!.Length;

                await indexManager.CreateAsync(indexProfile, BuildFields(dimensions), cancellationToken);
            }

            var documents = indexedChunks
                .Select(chunk => new IndexDocument
                {
                    Id = chunk.ItemId,
                    Fields = new Dictionary<string, object>
                    {
                        [DocumentIndexConstants.ColumnNames.ChunkId] = chunk.ItemId,
                        [DocumentIndexConstants.ColumnNames.DocumentId] = document.ItemId,
                        [DocumentIndexConstants.ColumnNames.Content] = chunk.Content,
                        [DocumentIndexConstants.ColumnNames.FileName] = document.FileName,
                        [DocumentIndexConstants.ColumnNames.ReferenceId] = chunk.ReferenceId,
                        [DocumentIndexConstants.ColumnNames.ReferenceType] = chunk.ReferenceType,
                        [DocumentIndexConstants.ColumnNames.Embedding] = chunk.Embedding,
                        [DocumentIndexConstants.ColumnNames.ChunkIndex] = chunk.Index,
                    },
                })
                .ToArray();

            var indexed = await documentManager.AddOrUpdateAsync(indexProfile, documents, cancellationToken);

            if (!indexed)
            {
                _logger.LogWarning("AI document indexing reported failure for file '{FileName}' into index '{IndexName}'.", document.FileName, indexProfile.IndexFullName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index uploaded AI document '{FileName}'. The document will remain attached, but search indexing was skipped.", document.FileName);
        }
    }

    public async Task DeleteAsync(string documentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        try
        {
            var indexProfile = await GetConfiguredIndexProfileAsync(cancellationToken);

            if (indexProfile == null)
            {
                return;
            }

            var documentManager = _serviceProvider.GetKeyedService<ISearchDocumentManager>(indexProfile.ProviderName);

            if (documentManager == null)
            {
                _logger.LogWarning("Skipping AI document index cleanup because provider '{ProviderName}' is not configured for search indexing.", indexProfile.ProviderName);

                return;
            }

            await documentManager.DeleteAsync(indexProfile, [documentId], cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove AI document '{DocumentId}' from the configured search index.", documentId);
        }
    }

    public async Task DeleteChunksAsync(IEnumerable<string> chunkIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunkIds);

        var ids = chunkIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (ids.Length == 0)
        {

            return;
        }

        try
        {
            var indexProfile = await GetConfiguredIndexProfileAsync(cancellationToken);

            if (indexProfile == null)
            {
                return;
            }

            var documentManager = _serviceProvider.GetKeyedService<ISearchDocumentManager>(indexProfile.ProviderName);

            if (documentManager == null)
            {
                _logger.LogWarning("Skipping AI document chunk cleanup because provider '{ProviderName}' is not configured for search indexing.", indexProfile.ProviderName);

                return;
            }

            await documentManager.DeleteAsync(indexProfile, ids, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove {ChunkCount} AI document chunk(s) from the configured search index.", ids.Length);
        }
    }

    private async Task<SearchIndexProfile> GetConfiguredIndexProfileAsync(CancellationToken cancellationToken)
    {
        var settings = _options;

        if (string.IsNullOrWhiteSpace(settings.IndexProfileName))
        {
            _logger.LogDebug("AI document indexing is disabled because no default AI Documents index is configured.");

            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var indexProfile = await _indexProfileStore.FindByNameAsync(settings.IndexProfileName);

        if (indexProfile == null)
        {
            _logger.LogWarning("AI document indexing is configured to use '{IndexProfileName}', but that index profile was not found.", settings.IndexProfileName);

            return null;
        }

        if (!string.Equals(indexProfile.Type, IndexProfileTypes.AIDocuments, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("AI document indexing requires an '{ExpectedType}' index profile, but '{IndexProfileName}' is '{ActualType}'.", IndexProfileTypes.AIDocuments, settings.IndexProfileName, indexProfile.Type);

            return null;
        }

        return indexProfile;
    }

    private static IReadOnlyCollection<SearchIndexField> BuildFields(int vectorDimensions)
    {
        return
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
            FieldType = SearchFieldType.Text,
            IsFilterable = true,
            IsSearchable = true,
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
            IsFilterable = true,
            },
            new SearchIndexField
            {
            Name = DocumentIndexConstants.ColumnNames.Embedding,
            FieldType = SearchFieldType.Vector,
            VectorDimensions = vectorDimensions,
            },
        ];
    }
}
