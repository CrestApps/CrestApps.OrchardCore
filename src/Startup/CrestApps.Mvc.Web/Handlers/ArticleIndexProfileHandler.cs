using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.Mvc.Web.Services;

namespace CrestApps.Mvc.Web.Handlers;

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
}
