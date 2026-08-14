using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.TimeZones.Models;
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
using OrchardCore.Navigation;
using OrchardCore.Routing;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.TimeZones.Controllers;

/// <summary>
/// Provides endpoints for managing time zone maps.
/// </summary>
[Admin]
public sealed class AdminController : Controller
{
    private const string _optionsSearch = "Options.Search";
    private const string _nameFieldName = "Name";

    private readonly INamedCatalogManager<TimeZoneMap> _manager;
    private readonly INamedCatalog<TimeZoneMap> _catalog;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<TimeZoneMap> _displayManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminController"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="catalog">The catalog.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AdminController(
        INamedCatalogManager<TimeZoneMap> manager,
        INamedCatalog<TimeZoneMap> catalog,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<TimeZoneMap> displayManager,
        INotifier notifier,
        IHtmlLocalizer<AdminController> htmlLocalizer,
        IStringLocalizer<AdminController> stringLocalizer)
    {
        _manager = manager;
        _catalog = catalog;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Displays the time zone maps list.
    /// </summary>
    /// <param name="options">The list options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    [Admin("timezones", "TimeZoneMapsIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeZonesConstants.Permissions.ManageTimeZoneMaps))
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

        var viewModel = new ListCatalogEntryViewModel<CatalogEntryViewModel<TimeZoneMap>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<TimeZoneMap>
            {
                Model = model,
                Shape = await _displayManager.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        viewModel.Options.BulkActions = [];

        return View(viewModel);
    }

    /// <summary>
    /// Handles filtering on the list page.
    /// </summary>
    /// <param name="model">The list view model.</param>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("timezones", "TimeZoneMapsIndex")]
    public async Task<ActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeZonesConstants.Permissions.ManageTimeZoneMaps))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    /// <summary>
    /// Displays the create screen.
    /// </summary>
    [Admin("timezones/create", "TimeZoneMapsCreate")]
    public async Task<ActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeZonesConstants.Permissions.ManageTimeZoneMaps))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();
        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["Time zone map"],
            Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    /// <summary>
    /// Creates a new time zone map.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("timezones/create", "TimeZoneMapsCreate")]
    public async Task<ActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeZonesConstants.Permissions.ManageTimeZoneMaps))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();
        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["New time zone map"],
            Editor = await _displayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            var existingMap = await _catalog.FindByNameAsync(model.Name);

            if (existingMap != null)
            {
                ModelState.AddModelError(_nameFieldName, S["A time zone map with the same name already exists."]);
            }

            if (ModelState.IsValid)
            {
                await _manager.CreateAsync(model);
                await _notifier.SuccessAsync(H["A new time zone map has been created successfully."]);

                return RedirectToAction(nameof(Index));
            }
        }

        return View(viewModel);
    }

    /// <summary>
    /// Displays the edit screen.
    /// </summary>
    /// <param name="id">The time zone map identifier.</param>
    [Admin("timezones/edit/{id}", "TimeZoneMapsEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeZonesConstants.Permissions.ManageTimeZoneMaps))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
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
    /// Updates an existing time zone map.
    /// </summary>
    /// <param name="id">The time zone map identifier.</param>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("timezones/edit/{id}", "TimeZoneMapsEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeZonesConstants.Permissions.ManageTimeZoneMaps))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
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
            var existingMap = await _catalog.FindByNameAsync(model.Name);

            if (existingMap != null && !string.Equals(existingMap.ItemId, model.ItemId, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(_nameFieldName, S["A time zone map with the same name already exists."]);
            }

            if (ModelState.IsValid)
            {
                await _manager.UpdateAsync(model);
                await _notifier.SuccessAsync(H["The time zone map has been updated successfully."]);

                return RedirectToAction(nameof(Index));
            }
        }

        return View(viewModel);
    }

    /// <summary>
    /// Deletes the time zone map.
    /// </summary>
    /// <param name="id">The time zone map identifier.</param>
    [HttpPost]
    [ActionName("Delete")]
    [Admin("timezones/delete/{id}", "TimeZoneMapsDelete")]
    public async Task<ActionResult> DeletePost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TimeZonesConstants.Permissions.ManageTimeZoneMaps))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model == null)
        {
            return NotFound();
        }

        if (await _manager.DeleteAsync(model))
        {
            await _notifier.SuccessAsync(H["The time zone map has been deleted successfully."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
