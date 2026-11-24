using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Assessments.Controllers;

/// <summary>
/// Grade scales: their list and their editor.
/// </summary>
[Admin]
[Feature(AssessmentsConstants.Features.Gradebook)]
public sealed class GradeScalesController : Controller
{
    private readonly GradeScaleStore _store;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="GradeScalesController"/> class.
    /// </summary>
    public GradeScalesController(GradeScaleStore store, IAuthorizationService authorizationService, INotifier notifier, IHtmlLocalizer<GradeScalesController> htmlLocalizer)
    {
        _store = store;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the grade scales.
    /// </summary>
    [HttpGet]
    [Admin("learning/grade-scales", "LearningGradeScales")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.ManageGradeScales))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var all = (await _store.GetAllAsync()).OrderBy(scale => scale.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        return View(new GradeScaleListViewModel
        {
            List = new AdminListContext<GradeScale>
            {
                Source = all.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, all.Count, new RouteValueDictionary()),
            },
        });
    }

    /// <summary>
    /// Shows the editor of a new scale.
    /// </summary>
    [HttpGet]
    [Admin("learning/grade-scales/new", "LearningGradeScalesCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.ManageGradeScales))
        {
            return Forbid();
        }

        return View("Edit", WithEmptyRows(new GradeScaleEditViewModel
        {
            Bands =
            [
                new GradeBandRowViewModel { MinPercent = 90, Label = "A", Passing = true },
                new GradeBandRowViewModel { MinPercent = 80, Label = "B", Passing = true },
                new GradeBandRowViewModel { MinPercent = 70, Label = "C", Passing = true },
                new GradeBandRowViewModel { MinPercent = 60, Label = "D", Passing = true },
                new GradeBandRowViewModel { MinPercent = 0, Label = "F", Passing = false },
            ],
        }));
    }

    /// <summary>
    /// Shows the editor of a scale.
    /// </summary>
    [HttpGet]
    [Admin("learning/grade-scales/{id}", "LearningGradeScalesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.ManageGradeScales))
        {
            return Forbid();
        }

        var scale = await _store.FindByIdAsync(id);

        if (scale is null)
        {
            return NotFound();
        }

        return View(WithEmptyRows(new GradeScaleEditViewModel
        {
            Id = scale.ItemId,
            Name = scale.Name,
            Bands = scale.Bands.OrderByDescending(band => band.MinPercent).Select(band => new GradeBandRowViewModel { MinPercent = band.MinPercent, Label = band.Label, Passing = band.Passing }).ToList(),
        }));
    }

    /// <summary>
    /// Saves a scale.
    /// </summary>
    [HttpPost]
    [Admin("learning/grade-scales/save", "LearningGradeScalesSave")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(GradeScaleEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.ManageGradeScales))
        {
            return Forbid();
        }

        var scale = string.IsNullOrEmpty(model.Id) ? new GradeScale() : await _store.FindByIdAsync(model.Id);

        if (scale is null)
        {
            return NotFound();
        }

        var bands = model.Bands.Where(band => band.MinPercent is not null && !string.IsNullOrWhiteSpace(band.Label))
            .Select(band => new GradeBand { MinPercent = Math.Clamp(band.MinPercent.Value, 0, 100), Label = band.Label.Trim(), Passing = band.Passing })
            .OrderByDescending(band => band.MinPercent)
            .ToList();

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), H["Give the scale a name."].Value);
        }
        else if (await _store.FindByNameAsync(model.Name.Trim()) is { } other && other.ItemId != scale.ItemId)
        {
            ModelState.AddModelError(nameof(model.Name), H["Another scale has this name."].Value);
        }

        if (bands.Count == 0 || bands.Min(band => band.MinPercent) != 0)
        {
            ModelState.AddModelError(string.Empty, H["Add bands down to 0%, so every percentage has a grade."].Value);
        }
        else if (bands.Select(band => band.MinPercent).Distinct().Count() != bands.Count)
        {
            ModelState.AddModelError(string.Empty, H["Two bands start at the same percentage."].Value);
        }

        if (!ModelState.IsValid)
        {
            return View("Edit", WithEmptyRows(model));
        }

        scale.Name = model.Name.Trim();
        scale.Bands = bands;

        if (string.IsNullOrEmpty(scale.ItemId))
        {
            await _store.CreateAsync(scale);
        }
        else
        {
            await _store.UpdateAsync(scale);
        }

        await _notifier.SuccessAsync(H["The grade scale is saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a scale.
    /// </summary>
    [HttpPost]
    [Admin("learning/grade-scales/{id}/delete", "LearningGradeScalesDelete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssessmentPermissions.ManageGradeScales))
        {
            return Forbid();
        }

        var scale = await _store.FindByIdAsync(id);

        if (scale is null)
        {
            return NotFound();
        }

        await _store.DeleteAsync(scale);
        await _notifier.SuccessAsync(H["The grade scale was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private static GradeScaleEditViewModel WithEmptyRows(GradeScaleEditViewModel model)
    {
        while (model.Bands.Count < Math.Max(6, model.Bands.Count(band => band.MinPercent is not null) + 2))
        {
            model.Bands.Add(new GradeBandRowViewModel());
        }

        return model;
    }
}
