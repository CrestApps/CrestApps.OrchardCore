using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Skills.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Skills.Controllers;

/// <summary>
/// The skill catalog: the list and the editor.
/// </summary>
public sealed class SkillsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ISkillManager _skillManager;
    private readonly ICertificationManager _certificationManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="skillManager">The skill manager.</param>
    /// <param name="certificationManager">The certification manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public SkillsController(
        IAuthorizationService authorizationService,
        ISkillManager skillManager,
        ICertificationManager certificationManager,
        INotifier notifier,
        IHtmlLocalizer<SkillsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _skillManager = skillManager;
        _certificationManager = certificationManager;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the skills.
    /// </summary>
    /// <param name="search">The text the name must contain.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("skills", "Skills")]
    public async Task<IActionResult> Index(string search, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.ManageSkills))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, skills) = await _skillManager.SearchAsync(search, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        return View(new SkillsListViewModel
        {
            Search = search,
            List = new AdminListContext<Skill>
            {
                Source = skills.ToList(),
                Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "search", search } }),
            },
        });
    }

    /// <summary>
    /// Shows the editor of a new or existing skill.
    /// </summary>
    /// <param name="id">The skill id, or empty for a new skill.</param>
    /// <returns>The editor.</returns>
    [Admin("skills/edit/{id?}", "SkillsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.ManageSkills))
        {
            return Forbid();
        }

        var skill = string.IsNullOrEmpty(id)
            ? await _skillManager.NewAsync()
            : await _skillManager.FindByIdAsync(id);

        if (skill is null)
        {
            return NotFound();
        }

        var model = new SkillEditViewModel
        {
            Id = id,
            Name = skill.Name,
            Category = skill.Category,
            Description = skill.Description,
            RequiredCertificationId = skill.RequiredCertificationId,
            Enabled = skill.Enabled,
        };

        await FillAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves a skill.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("skills/edit/{id?}", "SkillsEdit")]
    public async Task<IActionResult> EditPost(SkillEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.ManageSkills))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var skill = isNew
            ? await _skillManager.NewAsync()
            : await _skillManager.FindByIdAsync(model.Id);

        if (skill is null)
        {
            return NotFound();
        }

        skill.Name = model.Name;
        skill.Category = model.Category;
        skill.Description = model.Description;
        skill.RequiredCertificationId = string.IsNullOrWhiteSpace(model.RequiredCertificationId) ? null : model.RequiredCertificationId;
        skill.Enabled = model.Enabled;

        var validation = await _skillManager.ValidateAsync(skill);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            await FillAsync(model);

            return View(model);
        }

        if (isNew)
        {
            await _skillManager.CreateAsync(skill);
        }
        else
        {
            await _skillManager.UpdateAsync(skill);
        }

        await _notifier.SuccessAsync(H["The skill has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a skill. People who held it no longer do.
    /// </summary>
    /// <param name="id">The skill id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("skills/delete/{id}", "SkillsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SkillsPermissions.ManageSkills))
        {
            return Forbid();
        }

        var skill = await _skillManager.FindByIdAsync(id);

        if (skill is not null)
        {
            await _skillManager.DeleteAsync(skill);
            await _notifier.SuccessAsync(H["The skill has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task FillAsync(SkillEditViewModel model)
    {
        model.CertificationOptions = (await _certificationManager.GetEnabledAsync(HttpContext.RequestAborted))
            .Select(certification => new SelectListItem(certification.Name, certification.ItemId, certification.ItemId == model.RequiredCertificationId))
            .ToList();
    }
}
