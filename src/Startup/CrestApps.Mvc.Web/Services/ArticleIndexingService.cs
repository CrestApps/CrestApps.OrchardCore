using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Models;

namespace CrestApps.Mvc.Web.Services;

/// <summary>
/// Indexes articles into a configured search index (Elasticsearch or Azure AI Search).
/// </summary>
public sealed class ArticleIndexingService
{
    public const string ArticlesIndexType = "Articles";

    private readonly ISearchIndexProfileStore _indexProfileStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ArticleIndexingService> _logger;

    public ArticleIndexingService(
        ISearchIndexProfileStore indexProfileStore,
        IServiceProvider serviceProvider,
        ILogger<ArticleIndexingService> logger)
    {
        _indexProfileStore = indexProfileStore;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task IndexAsync(Article article, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(article);

        var indexProfile = await GetConfiguredIndexProfileAsync(cancellationToken);
        if (indexProfile == null)
        {
            return;
        }

        var indexManager = _serviceProvider.GetKeyedService<ISearchIndexManager>(indexProfile.ProviderName);
        var documentManager = _serviceProvider.GetKeyedService<ISearchDocumentManager>(indexProfile.ProviderName);

        if (indexManager == null || documentManager == null)
        {
            _logger.LogWarning("Skipping article indexing because provider '{ProviderName}' is not configured for search indexing.", indexProfile.ProviderName);
            return;
        }

        if (!await indexManager.ExistsAsync(indexProfile.IndexFullName, cancellationToken))
        {
            await indexManager.CreateAsync(indexProfile, BuildFields(), cancellationToken);
        }

        var documents = new[]
        {
            new IndexDocument
            {
                Id = article.ItemId,
                Fields = new Dictionary<string, object>
                {
                    [ColumnNames.ArticleId] = article.ItemId,
                    [ColumnNames.Title] = article.Title ?? string.Empty,
                    [ColumnNames.Description] = article.Description ?? string.Empty,
                    [ColumnNames.Author] = article.Author ?? string.Empty,
                    [ColumnNames.CreatedUtc] = article.CreatedUtc,
                },
            },
        };

        var indexed = await documentManager.AddOrUpdateAsync(indexProfile, documents, cancellationToken);

        if (!indexed)
        {
            _logger.LogWarning("Article indexing reported failure for article '{ArticleId}' into index '{IndexName}'.", article.ItemId, indexProfile.IndexFullName);
        }
    }

    public async Task DeleteAsync(string articleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(articleId);

        var indexProfile = await GetConfiguredIndexProfileAsync(cancellationToken);
        if (indexProfile == null)
        {
            return;
        }

        var documentManager = _serviceProvider.GetKeyedService<ISearchDocumentManager>(indexProfile.ProviderName);
        if (documentManager == null)
        {
            _logger.LogWarning("Skipping article index cleanup because provider '{ProviderName}' is not configured.", indexProfile.ProviderName);
            return;
        }

        await documentManager.DeleteAsync(indexProfile, [articleId], cancellationToken);
    }

    private async Task<SearchIndexProfile> GetConfiguredIndexProfileAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var indexProfiles = await _indexProfileStore.GetAllAsync();

        var indexProfile = indexProfiles.FirstOrDefault(p =>
            string.Equals(p.Type, ArticlesIndexType, StringComparison.OrdinalIgnoreCase));

        if (indexProfile == null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Article indexing is disabled because no '{Type}' index profile is configured.", ArticlesIndexType);
            }
        }

        return indexProfile;
    }

    private static IReadOnlyCollection<SearchIndexField> BuildFields()
    {
        return
        [
            new SearchIndexField
            {
                Name = ColumnNames.ArticleId,
                FieldType = SearchFieldType.Keyword,
                IsKey = true,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = ColumnNames.Title,
                FieldType = SearchFieldType.Text,
                IsSearchable = true,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = ColumnNames.Description,
                FieldType = SearchFieldType.Text,
                IsSearchable = true,
            },
            new SearchIndexField
            {
                Name = ColumnNames.Author,
                FieldType = SearchFieldType.Keyword,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = ColumnNames.CreatedUtc,
                FieldType = SearchFieldType.DateTime,
                IsFilterable = true,
            },
        ];
    }

    public static class ColumnNames
    {
        public const string ArticleId = "article_id";
        public const string Title = "title";
        public const string Description = "description";
        public const string Author = "author";
        public const string CreatedUtc = "created_utc";
    }
}
