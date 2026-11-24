using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Contents;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Controllers;

/// <summary>
/// The course catalog: the published courses, browsed by category (with the categories under it) and searched by
/// title, for everyone who may view content, as the course pages themselves are.
/// </summary>
[Feature(LearningConstants.Features.LearnerPages)]
public sealed class CatalogController : Controller
{
    private const int PageSize = 24;

    private readonly IAuthorizationService _authorizationService;
    private readonly CourseCatalogService _catalog;
    private readonly IShapeFactory _shapeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="catalog">The catalog builder.</param>
    /// <param name="shapeFactory">The shape factory, for the pager.</param>
    public CatalogController(IAuthorizationService authorizationService, CourseCatalogService catalog, IShapeFactory shapeFactory)
    {
        _authorizationService = authorizationService;
        _catalog = catalog;
        _shapeFactory = shapeFactory;
    }

    /// <summary>
    /// Shows a page of the catalog.
    /// </summary>
    /// <param name="category">The category to browse, or empty for every course.</param>
    /// <param name="q">Words to find in the course titles.</param>
    /// <param name="pagerParameters">The page.</param>
    /// <returns>The page.</returns>
    [HttpGet("learning/catalog", Name = "LearningCatalog")]
    public async Task<IActionResult> Index(string category, string q, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CommonPermissions.ViewContent))
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Challenge();
        }

        var pager = new Pager(pagerParameters, PageSize);
        var model = await _catalog.BuildAsync(category, q, pager);
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(model.Category))
        {
            routeData.Values["category"] = model.Category;
        }

        if (!string.IsNullOrEmpty(model.Search))
        {
            routeData.Values["q"] = model.Search;
        }

        model.Pager = await _shapeFactory.PagerAsync(pager, model.Total, routeData);

        return View(model);
    }
}
