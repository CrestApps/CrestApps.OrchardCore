using System.Globalization;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The shift templates: the list and the editor.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
public sealed class ShiftTemplatesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IShiftTemplateManager _templateManager;
    private readonly IActivityCodeManager _activityCodeManager;
    private readonly ITeamManager _teamManager;
    private readonly ISkillManager _skillManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShiftTemplatesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="templateManager">The shift template manager.</param>
    /// <param name="activityCodeManager">The activity code manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="skillManager">The skill manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShiftTemplatesController(
        IAuthorizationService authorizationService,
        IShiftTemplateManager templateManager,
        IActivityCodeManager activityCodeManager,
        ITeamManager teamManager,
        ISkillManager skillManager,
        INotifier notifier,
        IHtmlLocalizer<ShiftTemplatesController> htmlLocalizer,
        IStringLocalizer<ShiftTemplatesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _templateManager = templateManager;
        _activityCodeManager = activityCodeManager;
        _teamManager = teamManager;
        _skillManager = skillManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the shift templates.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/shift-templates", "ShiftTemplates")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageShiftTemplates))
        {
            return Forbid();
        }

        var templates = (await _templateManager.GetAllAsync(HttpContext.RequestAborted))
            .OrderBy(template => template.Start)
            .ThenBy(template => template.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new WorkforceListViewModel
        {
            List = new AdminListContext<ShiftTemplate>
            {
                Source = templates.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, templates.Count),
            },
        });
    }

    /// <summary>
    /// Shows the editor of a new or existing shift template.
    /// </summary>
    /// <param name="id">The template id, or empty for a new template.</param>
    /// <returns>The editor.</returns>
    [Admin("workforce/shift-templates/edit/{id?}", "ShiftTemplatesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageShiftTemplates))
        {
            return Forbid();
        }

        var template = string.IsNullOrEmpty(id)
            ? await _templateManager.NewAsync()
            : await _templateManager.FindByIdAsync(id);

        if (template is null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(id))
        {
            template.Start = new TimeOnly(9, 0);
        }

        var codes = await _activityCodeManager.GetAllAsync(HttpContext.RequestAborted);
        var model = new ShiftTemplateEditViewModel
        {
            Id = id,
            Name = template.Name,
            Color = template.Color,
            Start = template.Start.ToString("HH:mm", CultureInfo.InvariantCulture),
            End = template.Start.AddMinutes(template.LengthMinutes).ToString("HH:mm", CultureInfo.InvariantCulture),
            Segments = SegmentFormat.Write(SegmentFormat.FromTemplate(template), codes, template.Start),
            TeamIds = template.TeamIds.ToArray(),
            RequiredSkillIds = template.RequiredSkillIds.ToArray(),
            Enabled = template.Enabled,
        };

        await PopulateAsync(model, codes);

        return View(model);
    }

    /// <summary>
    /// Saves a shift template.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/shift-templates/edit/{id?}", "ShiftTemplatesEdit")]
    public async Task<IActionResult> EditPost(ShiftTemplateEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageShiftTemplates))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var template = isNew
            ? await _templateManager.NewAsync()
            : await _templateManager.FindByIdAsync(model.Id);

        if (template is null)
        {
            return NotFound();
        }

        var codes = await _activityCodeManager.GetAllAsync(HttpContext.RequestAborted);

        if (!PlanningInput.TryParseTime(model.Start, out var start))
        {
            ModelState.AddModelError(nameof(model.Start), S["Enter when the shift starts, such as 09:00."]);
        }

        if (!PlanningInput.TryParseTime(model.End, out var end))
        {
            ModelState.AddModelError(nameof(model.End), S["Enter when the shift ends, such as 17:00."]);
        }

        var errors = new List<string>();
        var segments = SegmentFormat.Read(model.Segments, codes, errors);

        foreach (var error in errors)
        {
            ModelState.AddModelError(nameof(model.Segments), S["This break or activity isn't understood: {0}", error]);
        }

        template.Name = model.Name;
        template.Color = string.IsNullOrWhiteSpace(model.Color) ? null : model.Color.Trim();
        template.Start = start;
        template.LengthMinutes = PlanningInput.LengthMinutes(start, end);
        template.Segments = SegmentFormat.ToTemplate(segments, start);
        template.TeamIds = (model.TeamIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
        template.RequiredSkillIds = (model.RequiredSkillIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToList();
        template.Enabled = model.Enabled;

        if (ModelState.IsValid)
        {
            var validation = await _templateManager.ValidateAsync(template);

            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(model, codes);

            return View(model);
        }

        if (isNew)
        {
            await _templateManager.CreateAsync(template);
        }
        else
        {
            await _templateManager.UpdateAsync(template);
        }

        await _notifier.SuccessAsync(H["The shift template has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a shift template.
    /// </summary>
    /// <param name="id">The template id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/shift-templates/delete/{id}", "ShiftTemplatesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageShiftTemplates))
        {
            return Forbid();
        }

        var template = await _templateManager.FindByIdAsync(id);

        if (template is not null)
        {
            await _templateManager.DeleteAsync(template);
            await _notifier.SuccessAsync(H["The shift template has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(ShiftTemplateEditViewModel model, IEnumerable<ActivityCode> codes)
    {
        var teams = model.TeamIds ?? [];
        var skills = model.RequiredSkillIds ?? [];

        model.ActivityNames = codes.Where(code => code.Enabled).Select(code => code.Name).Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        model.TeamOptions = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted))
            .Select(item => new SelectListItem(new string(' ', item.Depth * 4) + item.Team.Name, item.Team.ItemId, teams.Contains(item.Team.ItemId)))
            .ToList();
        model.SkillOptions = (await _skillManager.GetEnabledAsync(HttpContext.RequestAborted))
            .OrderBy(skill => skill.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(skill => new SelectListItem(skill.Name, skill.ItemId, skills.Contains(skill.ItemId)))
            .ToList();
    }
}
