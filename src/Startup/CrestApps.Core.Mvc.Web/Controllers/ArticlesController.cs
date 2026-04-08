using CrestApps.Core.Mvc.Web.Areas.Admin.Models;
using CrestApps.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.Core.Mvc.Web.Controllers;

[AllowAnonymous]
public sealed class ArticlesController : Controller
{
    public const string DisplayRouteName = "PublicArticleDisplay";

    private readonly ICatalogManager<Article> _articleManager;

    public ArticlesController(ICatalogManager<Article> articleManager)
    {
        _articleManager = articleManager;
    }

    [HttpGet("articles/{id}", Name = DisplayRouteName)]
    public async Task<IActionResult> Display(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var article = await _articleManager.FindByIdAsync(id);

        if (article is null)
        {
            return NotFound();
        }

        return View(article);
    }
}
