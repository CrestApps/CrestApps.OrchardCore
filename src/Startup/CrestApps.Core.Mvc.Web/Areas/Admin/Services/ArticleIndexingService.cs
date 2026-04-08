using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Mvc.Web.Areas.Admin.Models;
using CrestApps.Core.Services;
using CrestApps.Core.Support;

namespace CrestApps.Core.Mvc.Web.Areas.Admin.Services;

/// <summary>
/// Indexes articles into a configured search index (Elasticsearch or Azure AI Search).
/// </summary>
public sealed class ArticleIndexingService
{
    private readonly ICatalog<Article> _articleCatalog;
    private readonly ISearchIndexProfileStore _indexProfileStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ArticleIndexingService> _logger;

    public ArticleIndexingService(
        ICatalog<Article> articleCatalog,
        ISearchIndexProfileStore indexProfileStore,
        IServiceProvider serviceProvider,
        ILogger<ArticleIndexingService> logger)
    {
        _articleCatalog = articleCatalog;
        _indexProfileStore = indexProfileStore;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task IndexAsync(Article article, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(article);

        var indexProfiles = await GetConfiguredIndexProfilesAsync(cancellationToken);

        if (indexProfiles.Count == 0)
        {
            return;
        }

        foreach (var indexProfile in indexProfiles)
        {
            await IndexIntoProfileAsync(indexProfile, article, cancellationToken);
        }
    }

    public async Task SyncByIndexProfileAsync(SearchIndexProfile indexProfile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);

        if (!string.Equals(indexProfile.Type, IndexProfileTypes.Articles, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!TryResolveSearchServices(indexProfile.ProviderName, out var indexManager, out var documentManager))
        {
            return;
        }

        if (!await indexManager.ExistsAsync(indexProfile, cancellationToken))
        {
            await indexManager.CreateAsync(indexProfile, BuildFields(), cancellationToken);
        }

        var articles = await _articleCatalog.GetAllAsync();

        foreach (var article in articles)
        {
            await IndexIntoProfileAsync(indexProfile, article, cancellationToken);
        }
    }

    public async Task DeleteAsync(string articleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(articleId);

        var indexProfiles = await GetConfiguredIndexProfilesAsync(cancellationToken);

        if (indexProfiles.Count == 0)
        {
            return;
        }

        foreach (var indexProfile in indexProfiles)
        {
            if (!TryResolveSearchServices(indexProfile.ProviderName, out _, out var documentManager))
            {
                continue;
            }

            await documentManager.DeleteAsync(indexProfile, [articleId], cancellationToken);
        }
    }

    private async Task<IReadOnlyCollection<SearchIndexProfile>> GetConfiguredIndexProfilesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var indexProfiles = await _indexProfileStore.GetByTypeAsync(IndexProfileTypes.Articles);

        if (indexProfiles.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Article indexing is disabled because no '{Type}' index profile is configured.", IndexProfileTypes.Articles);
            }
        }

        return indexProfiles;
    }

    private async Task IndexIntoProfileAsync(SearchIndexProfile indexProfile, Article article, CancellationToken cancellationToken)
    {
        if (!TryResolveSearchServices(indexProfile.ProviderName, out var indexManager, out var documentManager))
        {
            return;
        }

        if (!await indexManager.ExistsAsync(indexProfile, cancellationToken))
        {
            await indexManager.CreateAsync(indexProfile, BuildFields(), cancellationToken);
        }

        var indexed = await documentManager.AddOrUpdateAsync(indexProfile,
        [
            new IndexDocument
            {
            Id = article.ItemId,
            Fields = new Dictionary<string, object>
            {
            [ColumnNames.ArticleId] = article.ItemId,
            [ColumnNames.Title] = article.Title ?? string.Empty,
            [ColumnNames.Description] = article.Description ?? string.Empty,
            [ColumnNames.CreatedUtc] = article.CreatedUtc,
            },
            },
            ], cancellationToken);

        if (!indexed)
        {
            _logger.LogWarning(
                "Article indexing reported failure for article '{ArticleId}' into index '{IndexName}'.",
                article.ItemId.SanitizeLogValue(),
                indexProfile.IndexFullName.SanitizeLogValue());
        }
    }

    private bool TryResolveSearchServices(
    string providerName,
    out ISearchIndexManager indexManager,
    out ISearchDocumentManager documentManager)
    {
        try
        {
            indexManager = _serviceProvider.GetKeyedService<ISearchIndexManager>(providerName);
            documentManager = _serviceProvider.GetKeyedService<ISearchDocumentManager>(providerName);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Skipping article indexing because provider '{ProviderName}' is not fully configured for search indexing.", providerName.SanitizeLogValue());
            indexManager = null;
            documentManager = null;

            return false;
        }

        if (indexManager == null || documentManager == null)
        {
            _logger.LogWarning("Skipping article indexing because provider '{ProviderName}' is not configured for search indexing.", providerName.SanitizeLogValue());

            return false;
        }

        return true;
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
        public const string CreatedUtc = "created_utc";
    }
}
