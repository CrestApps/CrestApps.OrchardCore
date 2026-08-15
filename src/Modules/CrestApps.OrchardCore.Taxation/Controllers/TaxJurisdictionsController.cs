using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Taxation.Models;
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

namespace CrestApps.OrchardCore.Taxation.Controllers;

/// <summary>
/// Provides admin endpoints for managing <see cref="TaxJurisdiction"/> entries.
/// </summary>
[Admin]
public sealed class TaxJurisdictionsController : Controller
{
    private const string _optionsSearch = "Options.Search";
    private const string _nameFieldName = "Name";

    private readonly INamedCatalogManager<TaxJurisdiction> _manager;
    private readonly INamedCatalog<TaxJurisdiction> _catalog;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<TaxJurisdiction> _displayManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public TaxJurisdictionsController(
        INamedCatalogManager<TaxJurisdiction> manager,
        INamedCatalog<TaxJurisdiction> catalog,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<TaxJurisdiction> displayManager,
        INotifier notifier,
        IHtmlLocalizer<TaxJurisdictionsController> htmlLocalizer,
        IStringLocalizer<TaxJurisdictionsController> stringLocalizer)
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

    [Admin("taxation/jurisdictions", "TaxationJurisdictionsIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TaxationPermissions.ManageTaxation))
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

        var viewModel = new ListCatalogEntryViewModel<CatalogEntryViewModel<TaxJurisdiction>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<TaxJurisdiction>
            {
                Model = model,
                Shape = await _displayManager.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        viewModel.Options.BulkActions = [];

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("taxation/jurisdictions", "TaxationJurisdictionsIndex")]
    public IActionResult IndexFilterPost(ListCatalogEntryViewModel model)
        => RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });

    [Admin("taxation/jurisdictions/create", "TaxationJurisdictionsCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TaxationPermissions.ManageTaxation))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["Tax Jurisdiction"],
            Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("taxation/jurisdictions/create", "TaxationJurisdictionsCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, TaxationPermissions.ManageTaxation))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["Tax Jurisdiction"],
            Editor = await _displayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        var isValid = await CatalogEntryValidation.ValidateAsync(_manager, model, _updateModelAccessor.ModelUpdater, nameof(TaxJurisdiction));

        if (isValid && ModelState.IsValid)
        {
            var existing = await _catalog.FindByNameAsync(model.Name);

            if (existing != null)
            {
                ModelState.AddModelError(_nameFieldName, S["A tax jurisdiction with the same name already exists."]);
            }

            if (ModelState.IsValid)
            {
                await _manager.CreateAsync(model);
                await _notifier.SuccessAsync(H["The tax jurisdiction has been created successfully."]);

                return RedirectToAction(nameof(Index));
            }
        }

        return View(viewModel);
    }

    [Admin("taxation/jurisdictions/edit/{id}", "TaxationJurisdictionsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TaxationPermissions.ManageTaxation))
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

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("taxation/jurisdictions/edit/{id}", "TaxationJurisdictionsEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TaxationPermissions.ManageTaxation))
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

        var isValid = await CatalogEntryValidation.ValidateAsync(_manager, model, _updateModelAccessor.ModelUpdater, nameof(TaxJurisdiction));

        if (isValid && ModelState.IsValid)
        {
            var existing = await _catalog.FindByNameAsync(model.Name);

            if (existing != null && !string.Equals(existing.ItemId, model.ItemId, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(_nameFieldName, S["A tax jurisdiction with the same name already exists."]);
            }

            if (ModelState.IsValid)
            {
                await _manager.UpdateAsync(model);
                await _notifier.SuccessAsync(H["The tax jurisdiction has been updated successfully."]);

                return RedirectToAction(nameof(Index));
            }
        }

        return View(viewModel);
    }

    [HttpPost]
    [ActionName("Delete")]
    [Admin("taxation/jurisdictions/delete/{id}", "TaxationJurisdictionsDelete")]
    public async Task<IActionResult> DeletePost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TaxationPermissions.ManageTaxation))
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
            await _notifier.SuccessAsync(H["The tax jurisdiction has been deleted successfully."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
