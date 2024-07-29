using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.CrestApps.Subscriptions.Core;
using OrchardCore.CrestApps.Subscriptions.Core.Extensions;
using OrchardCore.DisplayManagement;
using OrchardCore.Navigation;
using YesSql;
using YesSql.Services;

namespace OrchardCore.CrestApps.Subscriptions.Controllers;

public sealed class SubscriptionsController : Controller
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISession _session;

    public SubscriptionsController(
        IContentDefinitionManager contentDefinitionManager,
        ISession session)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
    }

    public async Task<IActionResult> Index(
        string contentType,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        var contentTypes = new List<string>();

        if (!string.IsNullOrEmpty(contentType))
        {
            var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

            if (definition != null && definition.StereotypeEquals(SubscriptionsConstants.Stereotype))
            {
                contentTypes.Add(definition.Name);
            }
        }

        if (contentTypes.Count == 0)
        {
            contentTypes.AddRange((await _contentDefinitionManager.GetSubscriptionsTypeDefinitionsAsync()).Select(x => x.Name));
        }

        if (contentTypes.Count == 0)
        {
            return NotFound();
        }

        var query = _session.Query<ContentItem, ContentItemIndex>(item => item.Published && item.ContentType.IsIn(contentTypes));

        var total = await query.CountAsync();

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var pagerShape = await shapeFactory.PagerAsync(pager, total);

        var startIndex = (pager.Page - 1) * pager.PageSize + 1;

        var contentItems = await query.Skip(startIndex).Take(pager.PageSize).ListAsync();
    }
}
