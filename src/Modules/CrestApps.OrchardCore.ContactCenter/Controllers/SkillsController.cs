using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Provides administration of Contact Center skills.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.Queues)]
public sealed class SkillsController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IContactCenterSkillManager _manager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<ContactCenterSkill> _displayManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillsController"/> class.
    /// </summary>
    /// <param name="manager">The skill manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SkillsController(
        IContactCenterSkillManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<ContactCenterSkill> displayManager,
        INotifier notifier,
        IHtmlLocalizer<SkillsController> htmlLocalizer,
        IStringLocalizer<SkillsController> stringLocalizer)
    {
        _manager = manager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the skills.
    /// </summary>
    /// <param name="options">The catalog entry options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The skills list view.</returns>
    [Admin("contact-center/skills", "ContactCenterSkillsIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageSkills))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());
        var result = await _manager.PageAsync(pager.Page, pager.PageSize, new QueryContext
        {
            Name = options.Search,
        });

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var viewModel = new ListCatalogEntryViewModel<CatalogEntryViewModel<ContactCenterSkill>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<ContactCenterSkill>
            {
                Model = model,
                Shape = await _displayManager.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        return View(viewModel);
    }

    /// <summary>
    /// Applies the skills list filter.
    /// </summary>
    /// <param name="model">The submitted list model.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("contact-center/skills", "ContactCenterSkillsIndex")]
    public async Task<ActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageSkills))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    /// <summary>
    /// Displays the skill create form.
    /// </summary>
    /// <returns>The create view.</returns>
    [Admin("contact-center/skills/create", "ContactCenterSkillsCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageSkills))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();
        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["Skill"],
            Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    /// <summary>
    /// Persists a new skill.
    /// </summary>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("contact-center/skills/create", "ContactCenterSkillsCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageSkills))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();
        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["New Skill"],
            Editor = await _displayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _manager.CreateAsync(model);
            await _notifier.SuccessAsync(H["A new skill has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    /// <summary>
    /// Displays the skill edit form.
    /// </summary>
    /// <param name="id">The skill identifier.</param>
    /// <returns>The edit view.</returns>
    [Admin("contact-center/skills/edit/{id}", "ContactCenterSkillsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageSkills))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model is null)
        {
            return NotFound();
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.Name,
            Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(viewModel);
    }

    /// <summary>
    /// Persists changes to a skill.
    /// </summary>
    /// <param name="id">The skill identifier.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("contact-center/skills/edit/{id}", "ContactCenterSkillsEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageSkills))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model is null)
        {
            return NotFound();
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.Name,
            Editor = await _displayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _manager.UpdateAsync(model);
            await _notifier.SuccessAsync(H["The skill has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    /// <summary>
    /// Deletes a skill.
    /// </summary>
    /// <param name="id">The skill identifier.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("contact-center/skills/delete/{id}", "ContactCenterSkillsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageSkills))
        {
            return Forbid();
        }

        var skill = await _manager.FindByIdAsync(id);

        if (skill is not null)
        {
            await _manager.DeleteAsync(skill);
            await _notifier.SuccessAsync(H["The skill has been deleted successfully."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
