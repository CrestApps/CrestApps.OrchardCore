using CrestApps.AI.Indexing;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Infrastructure.Indexing.Models;
using CrestApps.Models;
using CrestApps.Mvc.Web.Areas.Admin.Services;

namespace CrestApps.Mvc.Web.Areas.Admin.Handlers;

public sealed class ArticleIndexProfileHandler : IndexProfileHandlerBase
{
    private readonly ArticleIndexingService _indexingService;

    public ArticleIndexProfileHandler(ArticleIndexingService indexingService)
    {
        _indexingService = indexingService;
    }

    public override async Task SynchronizedAsync(SearchIndexProfile indexProfile, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(indexProfile.Type, IndexProfileTypes.Articles, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await _indexingService.SyncByIndexProfileAsync(indexProfile, cancellationToken);
    }

    public override Task ResetAsync(SearchIndexProfile indexProfile, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public override ValueTask ValidateAsync(
        SearchIndexProfile indexProfile,
        ValidationResultDetails result,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(indexProfile.Type, IndexProfileTypes.Articles, StringComparison.OrdinalIgnoreCase))
        {
            indexProfile.EmbeddingDeploymentId = null;
        }

        return ValueTask.CompletedTask;
    }

    public override ValueTask<IReadOnlyCollection<SearchIndexField>> GetFieldsAsync(
        SearchIndexProfile indexProfile,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(indexProfile.Type, IndexProfileTypes.Articles, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult<IReadOnlyCollection<SearchIndexField>>(null);
        }

        IReadOnlyCollection<SearchIndexField> fields =
        [
            new SearchIndexField
            {
                Name = ArticleIndexingService.ColumnNames.ArticleId,
                FieldType = SearchFieldType.Keyword,
                IsKey = true,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = ArticleIndexingService.ColumnNames.Title,
                FieldType = SearchFieldType.Text,
                IsSearchable = true,
                IsFilterable = true,
            },
            new SearchIndexField
            {
                Name = ArticleIndexingService.ColumnNames.Description,
                FieldType = SearchFieldType.Text,
                IsSearchable = true,
            },
            new SearchIndexField
            {
                Name = ArticleIndexingService.ColumnNames.CreatedUtc,
                FieldType = SearchFieldType.DateTime,
                IsFilterable = true,
            },
        ];

        return ValueTask.FromResult(fields);
    }
}
