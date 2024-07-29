using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.CrestApps.Subscriptions.Core;
using OrchardCore.CrestApps.Subscriptions.Core.Extensions;
using OrchardCore.CrestApps.Subscriptions.Core.Indexes;
using OrchardCore.CrestApps.Subscriptions.ViewModels;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Navigation;
using YesSql;
using YesSql.Services;

namespace OrchardCore.CrestApps.Subscriptions.Controllers;

public sealed class SubscriptionsController : Controller
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ISession _session;

    public SubscriptionsController(
        IContentDefinitionManager contentDefinitionManager,
        IContentItemDisplayManager contentItemDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        ISession session)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentItemDisplayManager = contentItemDisplayManager;
        _updateModelAccessor = updateModelAccessor;
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

            if (definition == null || !definition.StereotypeEquals(SubscriptionsConstants.Stereotype))
            {
                return NotFound();
            }

            contentTypes.Add(definition.Name);
        }

        if (contentTypes.Count == 0)
        {
            contentTypes.AddRange((await _contentDefinitionManager.GetSubscriptionsTypeDefinitionsAsync()).Select(x => x.Name));
        }

        if (contentTypes.Count == 0)
        {
            return NotFound();
        }

        var query = _session.Query<ContentItem, SubscriptionsContentItemIndex>(item => item.Published && item.ContentType.IsIn(contentTypes))
            .OrderBy(index => index.Order)
            .ThenByDescending(index => index.CreatedUtc);

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var total = await query.CountAsync();

        var pagerShape = await shapeFactory.PagerAsync(pager, total);

        var startIndex = (pager.Page - 1) * pager.PageSize;

        var contentItems = await query.Skip(startIndex).Take(pager.PageSize).ListAsync();

        var model = new ListSubscriptionsViewModel()
        {
            Pager = pagerShape,
            Subscriptions = []
        };

        foreach (var contentItem in contentItems)
        {
            var shape = await _contentItemDisplayManager.BuildDisplayAsync(contentItem, _updateModelAccessor.ModelUpdater, "Summary");

            model.Subscriptions.Add(shape);
        }

        return View(model);
    }

    public IActionResult Signup(string id)
    {
        return View();
    }
}
