using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.PointOfSale.ViewModels;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.PointOfSale.Controllers;

/// <summary>
/// Commerce › Point of sale › Departments: what registers ring amounts up in, without an item to track.
/// </summary>
/// <remarks>
/// Its routes start with their own prefix: the registers controller's <c>point-of-sale/{action}</c> would take
/// <c>point-of-sale/departments</c> as one of its actions.
/// </remarks>
[Admin("pos-departments/{action}/{itemId?}", "PosDepartments{action}")]
public sealed class PosDepartmentsAdminController : Controller
{
    private readonly IPosDepartmentStore _departments;
    private readonly IEnumerable<INamedCatalog<TaxCategory>> _taxCategories;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public PosDepartmentsAdminController(
        IPosDepartmentStore departments,
        IEnumerable<INamedCatalog<TaxCategory>> taxCategories,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<PosDepartmentsAdminController> htmlLocalizer,
        IStringLocalizer<PosDepartmentsAdminController> stringLocalizer)
    {
        _departments = departments;
        _taxCategories = taxCategories;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("pos-departments", "PosDepartmentsIndex")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PosPermissions.ManageRegisters))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, departments) = await _departments.PageAsync(pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var pagerShape = await shapeFactory.PagerAsync(pager, count, new RouteData());

        return View(new PosDepartmentsIndexViewModel
        {
            List = new AdminListContext<PosDepartment>
            {
                Source = departments,
                Pager = (IShape)pagerShape,
            },
        });
    }

    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, PosPermissions.ManageRegisters))
        {
            return Forbid();
        }

        var position = (await _departments.GetAllAsync(HttpContext.RequestAborted)).Select(department => department.Position).DefaultIfEmpty(0).Max() + 1;

        return View("Edit", await PopulateAsync(new PosDepartmentEditViewModel { Position = position }));
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(PosDepartmentEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PosPermissions.ManageRegisters))
        {
            return Forbid();
        }

        var department = new PosDepartment { ItemId = IdGenerator.GenerateId(), CreatedUtc = _clock.UtcNow };

        if (!await ApplyAsync(model, department))
        {
            return View("Edit", await PopulateAsync(model));
        }

        await _departments.CreateAsync(department);
        await _notifier.SuccessAsync(H["The department was added. Registers show a key for it unless their keys are programmed."]);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PosPermissions.ManageRegisters))
        {
            return Forbid();
        }

        if (await _departments.FindByIdAsync(itemId) is not { } department)
        {
            return NotFound();
        }

        return View(await PopulateAsync(new PosDepartmentEditViewModel
        {
            Name = department.Name,
            Number = department.Number,
            TaxCategoryCode = department.TaxCategoryCode,
            Color = department.Color,
            MinimumAmount = department.MinimumAmount?.ToString(CultureInfo.InvariantCulture),
            MaximumAmount = department.MaximumAmount?.ToString(CultureInfo.InvariantCulture),
            Position = department.Position,
            IsEnabled = department.IsEnabled,
        }, department.ItemId));
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, PosDepartmentEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PosPermissions.ManageRegisters))
        {
            return Forbid();
        }

        if (await _departments.FindByIdAsync(itemId) is not { } department)
        {
            return NotFound();
        }

        if (!await ApplyAsync(model, department))
        {
            return View(await PopulateAsync(model, department.ItemId));
        }

        await _departments.UpdateAsync(department);
        await _notifier.SuccessAsync(H["The department was saved."]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, PosPermissions.ManageRegisters))
        {
            return Forbid();
        }

        if (await _departments.FindByIdAsync(itemId) is not { } department)
        {
            return NotFound();
        }

        // Sales keep the department's name on their lines, so removing it changes no sale; registers drop its key.
        await _departments.DeleteAsync(department);
        await _notifier.SuccessAsync(H["The department was removed. Its past sales keep its name."]);

        return RedirectToAction(nameof(Index));
    }

    // Applies the form onto the department and checks it; false when it isn't valid, with the problems in ModelState.
    private async Task<bool> ApplyAsync(PosDepartmentEditViewModel model, PosDepartment department)
    {
        var minimum = ParseAmount(model.MinimumAmount, nameof(model.MinimumAmount));
        var maximum = ParseAmount(model.MaximumAmount, nameof(model.MaximumAmount));
        var candidate = new PosDepartment
        {
            ItemId = department.ItemId,
            Name = model.Name,
            Number = model.Number,
            TaxCategoryCode = model.TaxCategoryCode,
            Color = model.Color,
            MinimumAmount = minimum,
            MaximumAmount = maximum,
            Position = model.Position,
            IsEnabled = model.IsEnabled,
        };

        PosDepartments.Normalize(candidate);

        foreach (var problem in PosDepartments.Validate(candidate, await _departments.GetAllAsync(HttpContext.RequestAborted), S))
        {
            ModelState.AddModelError(string.Empty, problem);
        }

        if (!ModelState.IsValid)
        {
            return false;
        }

        department.Name = candidate.Name;
        department.Number = candidate.Number;
        department.TaxCategoryCode = candidate.TaxCategoryCode;
        department.Color = candidate.Color;
        department.MinimumAmount = candidate.MinimumAmount;
        department.MaximumAmount = candidate.MaximumAmount;
        department.Position = candidate.Position;
        department.IsEnabled = candidate.IsEnabled;
        department.ModifiedUtc = _clock.UtcNow;

        return true;
    }

    // Amounts are typed in the invariant culture, as the number field sends them, so "2,50" is never read as 250.
    private decimal? ParseAmount(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (decimal.TryParse(value.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) && amount > 0m)
        {
            return amount;
        }

        ModelState.AddModelError(field, S["Enter an amount more than zero, such as 50.00, or leave it blank."]);

        return null;
    }

    private async Task<PosDepartmentEditViewModel> PopulateAsync(PosDepartmentEditViewModel model, string itemId = null)
    {
        model.ItemId = itemId;

        var catalog = _taxCategories.FirstOrDefault();

        if (catalog is not null)
        {
            model.HasTaxCategories = true;
            model.TaxCategories =
            [
                new SelectListItem(S["The store's default rate"], string.Empty, string.IsNullOrEmpty(model.TaxCategoryCode)),
                .. (await catalog.GetAllAsync())
                    .Where(category => !string.IsNullOrEmpty(category.Code))
                    .OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(category => new SelectListItem($"{category.Name} ({category.Code})", category.Code, string.Equals(category.Code, model.TaxCategoryCode, StringComparison.OrdinalIgnoreCase))),
            ];

            // A code no category has any more still shows, so saving doesn't quietly drop it.
            if (!string.IsNullOrEmpty(model.TaxCategoryCode) && !model.TaxCategories.Any(item => item.Selected))
            {
                model.TaxCategories.Add(new SelectListItem(model.TaxCategoryCode, model.TaxCategoryCode, true));
            }
        }

        return model;
    }
}
