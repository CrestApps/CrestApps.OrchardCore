using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.TimeTracker.Controllers;

/// <summary>
/// The stages and categories.
/// </summary>
public sealed class TimeSettingsController : Controller
{
    /// <summary>
    /// The kind of list of stages.
    /// </summary>
    public const string Stages = "stages";

    /// <summary>
    /// The kind of list of categories.
    /// </summary>
    public const string Categories = "categories";

    private readonly IAuthorizationService _authorizationService;
    private readonly TimeStageManager _stages;
    private readonly TimeCategoryManager _categories;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeSettingsController"/> class.
    /// </summary>
    public TimeSettingsController(IAuthorizationService authorizationService, TimeStageManager stages, TimeCategoryManager categories, INotifier notifier, IHtmlLocalizer<TimeSettingsController> htmlLocalizer, IStringLocalizer<TimeSettingsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _stages = stages;
        _categories = categories;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the stages or the categories.
    /// </summary>
    [Admin("time/settings/{kind}", "TimeTrackerSettings")]
    public async Task<IActionResult> Index(string kind, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.ManageTimeTrackerSettings))
        {
            return Forbid();
        }

        var categories = (await _categories.GetAllAsync()).ToList();
        var names = categories.ToDictionary(category => category.ItemId, category => category.Name, StringComparer.Ordinal);
        List<TimeSettingRow> rows = kind == Categories
            ? [.. categories.OrderBy(item => item.Position).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Select(item => new TimeSettingRow
            {
                Kind = Categories,
                Id = item.ItemId,
                Name = item.Name,
                Code = item.Code,
                Color = item.Color,
                Enabled = item.Enabled,
                Detail = item.Factor == 1 ? null : S["Each minute counts {0} times", item.Factor].Value,
            })]
            : [.. (await _stages.GetAllAsync()).OrderBy(item => item.Position).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).Select(item => new TimeSettingRow
            {
                Kind = Stages,
                Id = item.ItemId,
                Name = item.Name,
                Code = item.Code,
                Color = item.Color,
                Enabled = item.Enabled,
                Detail = string.Join(", ", item.CategoryIds.Select(id => names.GetValueOrDefault(id)).Where(name => name is not null)),
            })];

        return View(new TimeSettingsListViewModel
        {
            Kind = kind == Categories ? Categories : Stages,
            List = new AdminListContext<TimeSettingRow> { Source = rows },
        });
    }

    /// <summary>
    /// Shows a stage's form.
    /// </summary>
    [Admin("time/stages/edit/{id?}", "TimeTrackerStagesEdit")]
    public async Task<IActionResult> EditStage(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.ManageTimeTrackerSettings))
        {
            return Forbid();
        }

        var stage = string.IsNullOrEmpty(id) ? await _stages.NewAsync() : await _stages.FindByIdAsync(id);

        if (stage is null)
        {
            return NotFound();
        }

        var model = new TimeStageEditViewModel { Id = id, Name = stage.Name, Code = stage.Code, Color = stage.Color ?? Palette((await _stages.GetAllAsync()).Count()), Position = stage.Position, CategoryIds = [.. stage.CategoryIds], SelfService = stage.SelfService, Enabled = stage.Enabled };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves a stage.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(EditStage))]
    [Admin("time/stages/edit/{id?}", "TimeTrackerStagesEdit")]
    public async Task<IActionResult> EditStagePost(TimeStageEditViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.ManageTimeTrackerSettings))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(form.Id);
        var stage = isNew ? await _stages.NewAsync() : await _stages.FindByIdAsync(form.Id);

        if (stage is null)
        {
            return NotFound();
        }

        stage.Name = form.Name?.Trim();
        stage.Code = string.IsNullOrWhiteSpace(form.Code) ? null : form.Code.Trim().ToUpperInvariant();
        stage.Color = string.IsNullOrWhiteSpace(form.Color) ? null : form.Color.Trim();
        stage.Position = form.Position;
        stage.CategoryIds = [.. (form.CategoryIds ?? []).Where(categoryId => !string.IsNullOrEmpty(categoryId)).Distinct(StringComparer.Ordinal)];
        stage.SelfService = form.SelfService;
        stage.Enabled = form.Enabled;

        var validation = await _stages.ValidateAsync(stage);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(form);

            return View(form);
        }

        if (isNew)
        {
            await _stages.CreateAsync(stage);
        }
        else
        {
            await _stages.UpdateAsync(stage);
        }

        await _notifier.SuccessAsync(H["The stage was saved."]);

        return RedirectToAction(nameof(Index), new { kind = Stages });
    }

    /// <summary>
    /// Shows a category's form.
    /// </summary>
    [Admin("time/categories/edit/{id?}", "TimeTrackerCategoriesEdit")]
    public async Task<IActionResult> EditCategory(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.ManageTimeTrackerSettings))
        {
            return Forbid();
        }

        var category = string.IsNullOrEmpty(id) ? await _categories.NewAsync() : await _categories.FindByIdAsync(id);

        if (category is null)
        {
            return NotFound();
        }

        return View(new TimeCategoryEditViewModel { Id = id, Name = category.Name, Code = category.Code, Color = category.Color ?? Palette((await _categories.GetAllAsync()).Count()), Position = category.Position, Factor = category.Factor, Enabled = category.Enabled });
    }

    /// <summary>
    /// Saves a category.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(EditCategory))]
    [Admin("time/categories/edit/{id?}", "TimeTrackerCategoriesEdit")]
    public async Task<IActionResult> EditCategoryPost(TimeCategoryEditViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.ManageTimeTrackerSettings))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(form.Id);
        var category = isNew ? await _categories.NewAsync() : await _categories.FindByIdAsync(form.Id);

        if (category is null)
        {
            return NotFound();
        }

        category.Name = form.Name?.Trim();
        category.Code = string.IsNullOrWhiteSpace(form.Code) ? null : form.Code.Trim().ToUpperInvariant();
        category.Color = string.IsNullOrWhiteSpace(form.Color) ? null : form.Color.Trim();
        category.Position = form.Position;
        category.Factor = form.Factor;
        category.Enabled = form.Enabled;

        var validation = await _categories.ValidateAsync(category);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        if (isNew)
        {
            await _categories.CreateAsync(category);
        }
        else
        {
            await _categories.UpdateAsync(category);
        }

        await _notifier.SuccessAsync(H["The category was saved."]);

        return RedirectToAction(nameof(Index), new { kind = Categories });
    }

    /// <summary>
    /// Removes a stage or category.
    /// </summary>
    [HttpPost]
    [Admin("time/settings/{kind}/delete/{id}", "TimeTrackerSettingsDelete")]
    public async Task<IActionResult> Delete(string kind, string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeTrackerPermissions.ManageTimeTrackerSettings))
        {
            return Forbid();
        }

        if (kind == Categories && await _categories.FindByIdAsync(id) is { } category)
        {
            await _categories.DeleteAsync(category);
        }
        else if (kind == Stages && await _stages.FindByIdAsync(id) is { } stage)
        {
            // A stage with time recorded in it is turned off instead, so the time still adds up.
            stage.Enabled = false;
            await _stages.UpdateAsync(stage);
            await _notifier.InformationAsync(H["The stage was turned off; its recorded time stays."]);

            return RedirectToAction(nameof(Index), new { kind });
        }

        await _notifier.SuccessAsync(H["Removed."]);

        return RedirectToAction(nameof(Index), new { kind });
    }

    // A color picker always sends a color, so a new record starts with one from this palette rather than black.
    private static readonly string[] _palette = ["#2b8a3e", "#e8590c", "#1971c2", "#6741d9", "#c2255c", "#0c8599", "#f08c00", "#5c940d", "#495057"];

    private static string Palette(int index) => _palette[index % _palette.Length];

    private async Task PopulateAsync(TimeStageEditViewModel model)
    {
        model.CategoryOptions = [.. (await _categories.GetAllAsync())
            .OrderBy(category => category.Position)
            .ThenBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(category => new SelectListItem(category.Name, category.ItemId, model.CategoryIds.Contains(category.ItemId)))];
    }
}
