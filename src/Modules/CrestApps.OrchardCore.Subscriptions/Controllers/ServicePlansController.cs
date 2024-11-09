using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Extensions;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Navigation;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

public sealed class ServicePlansController : Controller
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentItemDisplayManager _contentItemDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ISession _session;

    internal readonly IHtmlLocalizer H;

    public ServicePlansController(
        IContentDefinitionManager contentDefinitionManager,
        IContentItemDisplayManager contentItemDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        ISession session,
        IHtmlLocalizer<SubscriptionsController> htmlLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentItemDisplayManager = contentItemDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _session = session;
        H = htmlLocalizer;
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

            if (definition == null || !definition.StereotypeEquals(SubscriptionConstants.Stereotype))
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

        var model = new ListServicePlansViewModel()
        {
            Pager = pagerShape,
            ServicePlans = []
        };

        foreach (var contentItem in contentItems)
        {
            var shape = await _contentItemDisplayManager.BuildDisplayAsync(contentItem, _updateModelAccessor.ModelUpdater, "Summary");

            model.ServicePlans.Add(shape);
        }

        return View(model);
    }
}
