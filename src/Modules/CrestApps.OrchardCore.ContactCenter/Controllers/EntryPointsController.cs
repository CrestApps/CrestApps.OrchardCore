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
/// Provides administration of Contact Center inbound entry points.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.Voice)]
public sealed class EntryPointsController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IContactCenterEntryPointManager _manager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<ContactCenterEntryPoint> _displayManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntryPointsController"/> class.
    /// </summary>
    /// <param name="manager">The entry point manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EntryPointsController(
        IContactCenterEntryPointManager manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<ContactCenterEntryPoint> displayManager,
        INotifier notifier,
        IHtmlLocalizer<EntryPointsController> htmlLocalizer,
        IStringLocalizer<EntryPointsController> stringLocalizer)
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
    /// Lists the entry points.
    /// </summary>
    /// <param name="options">The catalog entry options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The entry points list view.</returns>
    [Admin("contact-center/entry-points", "ContactCenterEntryPointsIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageQueues))
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

        var viewModel = new ListCatalogEntryViewModel<CatalogEntryViewModel<ContactCenterEntryPoint>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<ContactCenterEntryPoint>
            {
                Model = model,
                Shape = await _displayManager.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        return View(viewModel);
    }

    /// <summary>
    /// Applies the entry points list filter.
    /// </summary>
    /// <param name="model">The submitted list model.</param>
    /// <returns>A redirect to the filtered list.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("contact-center/entry-points", "ContactCenterEntryPointsIndex")]
    public async Task<ActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageQueues))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    /// <summary>
    /// Displays the entry point create form.
    /// </summary>
    /// <returns>The create view.</returns>
    [Admin("contact-center/entry-points/create", "ContactCenterEntryPointsCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageQueues))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();
        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["Entry point"],
            Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    /// <summary>
    /// Persists a new entry point.
    /// </summary>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("contact-center/entry-points/create", "ContactCenterEntryPointsCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageQueues))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();
        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["New entry point"],
            Editor = await _displayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _manager.CreateAsync(model);
            await _notifier.SuccessAsync(H["A new entry point has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    /// <summary>
    /// Displays the entry point edit form.
    /// </summary>
    /// <param name="id">The entry point identifier.</param>
    /// <returns>The edit view.</returns>
    [Admin("contact-center/entry-points/edit/{id}", "ContactCenterEntryPointsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageQueues))
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
    /// Persists changes to an entry point.
    /// </summary>
    /// <param name="id">The entry point identifier.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("contact-center/entry-points/edit/{id}", "ContactCenterEntryPointsEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageQueues))
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
            await _notifier.SuccessAsync(H["The entry point has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    /// <summary>
    /// Deletes an entry point.
    /// </summary>
    /// <param name="id">The entry point identifier.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("contact-center/entry-points/delete/{id}", "ContactCenterEntryPointsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageQueues))
        {
            return Forbid();
        }

        var entryPoint = await _manager.FindByIdAsync(id);

        if (entryPoint is not null)
        {
            await _manager.DeleteAsync(entryPoint);
            await _notifier.SuccessAsync(H["The entry point has been deleted successfully."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
