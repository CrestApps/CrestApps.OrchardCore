using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Calendars.Controllers;

/// <summary>
/// The categories people put events in to color-code and filter their calendar: their own, and (for calendar managers) the
/// ones everyone can use.
/// </summary>
public sealed class CalendarCategoriesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ICalendarCategoryService _categoryService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarCategoriesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="categoryService">The category service.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarCategoriesController(
        IAuthorizationService authorizationService,
        ICalendarCategoryService categoryService,
        INotifier notifier,
        IHtmlLocalizer<CalendarCategoriesController> htmlLocalizer,
        IStringLocalizer<CalendarCategoriesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _categoryService = categoryService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the user's categories and the shared ones.
    /// </summary>
    /// <param name="search">A part of a name to find.</param>
    /// <returns>The page.</returns>
    [Admin("calendar-categories", "CalendarCategories")]
    public async Task<IActionResult> Index(string search)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CalendarPermissions.AccessCalendars))
        {
            return Forbid();
        }

        var categories = (await _categoryService.ListAsync(CalendarUser.GetId(User), HttpContext.RequestAborted))
            .Where(category => string.IsNullOrWhiteSpace(search) || category.Name?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        return View(new CalendarCategoryListViewModel
        {
            Search = search,
            List = new AdminListContext<CalendarCategory> { Source = categories },
        });
    }

    /// <summary>
    /// Shows the editor of a new or existing category.
    /// </summary>
    /// <param name="id">The category's id, or empty for a new one.</param>
    /// <returns>The editor.</returns>
    [Admin("calendar-categories/edit/{id?}", "CalendarCategoriesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CalendarPermissions.AccessCalendars))
        {
            return Forbid();
        }

        var category = string.IsNullOrEmpty(id) ? new CalendarCategory { Color = "#1971c2" } : await _categoryService.FindByIdAsync(id, HttpContext.RequestAborted);

        if (category is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(id) && !await CanChangeAsync(category))
        {
            return Forbid();
        }

        return View(new CalendarCategoryEditViewModel
        {
            Id = category.ItemId,
            Name = category.Name,
            Color = category.Color,
            Shared = !string.IsNullOrEmpty(id) && category.IsShared,
            CanShare = await CanManageSharedAsync(),
        });
    }

    /// <summary>
    /// Saves a category.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("calendar-categories/edit/{id?}", "CalendarCategoriesEdit")]
    public async Task<IActionResult> EditPost(CalendarCategoryEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, CalendarPermissions.AccessCalendars))
        {
            return Forbid();
        }

        var category = string.IsNullOrEmpty(model.Id) ? new CalendarCategory() : await _categoryService.FindByIdAsync(model.Id, HttpContext.RequestAborted);

        if (category is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(model.Id) && !await CanChangeAsync(category))
        {
            return Forbid();
        }

        var canShare = await CanManageSharedAsync();

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), S["The name is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.Color) || !System.Text.RegularExpressions.Regex.IsMatch(model.Color.Trim(), "^#[0-9a-fA-F]{6}$"))
        {
            ModelState.AddModelError(nameof(model.Color), S["Choose a color."]);
        }

        if (!ModelState.IsValid)
        {
            model.CanShare = canShare;

            return View(model);
        }

        category.Name = model.Name.Trim();
        category.Color = model.Color.Trim().ToLowerInvariant();

        // Only a calendar manager decides whether a category is everyone's; anyone else's categories are their own. A new
        // category has no owner yet, so it must not be mistaken for a shared one.
        category.OwnerId = canShare && model.Shared ? null : CalendarUser.GetId(User);

        await _categoryService.SaveAsync(category, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The category has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a category. Its events stay, without a category.
    /// </summary>
    /// <param name="id">The category's id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("calendar-categories/delete/{id}", "CalendarCategoriesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        var category = await _categoryService.FindByIdAsync(id, HttpContext.RequestAborted);

        if (category is null)
        {
            return NotFound();
        }

        if (!await CanChangeAsync(category))
        {
            return Forbid();
        }

        await _categoryService.DeleteAsync(category, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["The category has been deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> CanChangeAsync(CalendarCategory category)
    {
        return category.IsShared
            ? await CanManageSharedAsync()
            : string.Equals(category.OwnerId, CalendarUser.GetId(User), StringComparison.Ordinal);
    }

    private Task<bool> CanManageSharedAsync()
    {
        return _authorizationService.AuthorizeAsync(User, CalendarPermissions.ManageAllCalendars);
    }
}
