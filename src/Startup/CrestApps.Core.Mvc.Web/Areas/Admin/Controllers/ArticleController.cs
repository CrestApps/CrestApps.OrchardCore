using CrestApps.Core.Models;
using CrestApps.Core.Mvc.Web.Areas.Admin.Models;
using CrestApps.Core.Mvc.Web.Areas.Admin.ViewModels;
using CrestApps.Core.Mvc.Web.Models;
using CrestApps.Core.Mvc.Web.Services;
using CrestApps.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.Core.Mvc.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Admin")]
public sealed class ArticleController : Controller
{
    private readonly ICatalogManager<Article> _manager;
    private readonly AppDataSettingsService<PaginationSettings> _paginationSettingsService;

    public ArticleController(
        ICatalogManager<Article> manager,
        AppDataSettingsService<PaginationSettings> paginationSettingsService)
    {
        _manager = manager;
        _paginationSettingsService = paginationSettingsService;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var pagination = await _paginationSettingsService.GetAsync();
        var pageSize = pagination.AdminPageSize;

        if (page < 1)
        {
            page = 1;
        }

        var result = await _manager.PageAsync(page, pageSize, new QueryContext());

        var model = new ArticleIndexViewModel
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = result.Count,
            Articles = result.Entries
                .Select(a => new ArticleListEntry
                {
                    ItemId = a.ItemId,
                    Title = a.Title,
                    CreatedUtc = a.CreatedUtc,
                })
            .ToList(),
        };

        return View(model);
    }

    public IActionResult Create()
    {
        return View(new ArticleViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ArticleViewModel model)
    {
        Validate(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var article = await _manager.NewAsync();
        article.Title = model.Title?.Trim();
        article.Description = model.Description?.Trim();
        article.CreatedUtc = DateTime.UtcNow;

        await _manager.CreateAsync(article);

        TempData["SuccessMessage"] = "Article created successfully.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var article = await _manager.FindByIdAsync(id);

        if (article == null)
        {
            return NotFound();
        }

        var model = new ArticleViewModel
        {
            ItemId = article.ItemId,
            Title = article.Title,
            Description = article.Description,
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ArticleViewModel model)
    {
        var article = await _manager.FindByIdAsync(model.ItemId);

        if (article == null)
        {
            return NotFound();
        }

        Validate(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        article.Title = model.Title?.Trim();
        article.Description = model.Description?.Trim();

        await _manager.UpdateAsync(article);

        TempData["SuccessMessage"] = "Article updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var article = await _manager.FindByIdAsync(id);

        if (article != null)
        {
            await _manager.DeleteAsync(article);
        }

        TempData["SuccessMessage"] = "Article deleted successfully.";

        return RedirectToAction(nameof(Index));
    }

    private void Validate(ArticleViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Title), "Title is required.");
        }
    }
}
