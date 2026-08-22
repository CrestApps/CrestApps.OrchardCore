using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Taxation.Core;
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
/// Provides admin endpoints for managing <see cref="TaxRule"/> entries.
/// </summary>
[Admin]
public sealed class TaxRulesController : Controller
{
    private const string _optionsSearch = "Options.Search";
    private const string _nameFieldName = "Name";

    private readonly INamedSourceCatalogManager<TaxRule> _manager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<TaxRule> _displayManager;
    private readonly TaxCalculationMethodOptions _methodOptions;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaxRulesController"/> class.
    /// </summary>
    /// <param name="manager">The tax rule catalog manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The tax rule display manager.</param>
    /// <param name="methodOptions">The tax calculation method options.</param>
    /// <param name="notifier">The notifier service.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TaxRulesController(
        INamedSourceCatalogManager<TaxRule> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<TaxRule> displayManager,
        IOptions<TaxCalculationMethodOptions> methodOptions,
        INotifier notifier,
        IHtmlLocalizer<TaxRulesController> htmlLocalizer,
        IStringLocalizer<TaxRulesController> stringLocalizer)
    {
        _manager = manager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        _methodOptions = methodOptions.Value;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("taxation/rules", "TaxationRulesIndex")]
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

        var viewModel = new ListSourceModelViewModel<TaxCalculationMethodEntry, CatalogEntryViewModel<TaxRule>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            Sources = _methodOptions.Methods.Values
                .OrderBy(entry => entry.DisplayName.Value, StringComparer.OrdinalIgnoreCase),
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<TaxRule>
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
    [Admin("taxation/rules", "TaxationRulesIndex")]
    public IActionResult IndexFilterPost(ListCatalogEntryViewModel model)
        => RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });

    [Admin("taxation/rules/create/{source}", "TaxationRulesCreate")]
    public async Task<IActionResult> Create(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TaxationPermissions.ManageTaxation))
        {
            return Forbid();
        }

        if (!_methodOptions.Methods.TryGetValue(source, out var method))
        {
            await _notifier.ErrorAsync(H["Unable to find a calculation method with the name '{0}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var model = await _manager.NewAsync(method.Name);

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = method.DisplayName.Value,
            Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("taxation/rules/create/{source}", "TaxationRulesCreate")]
    public async Task<IActionResult> CreatePost(string source)
    {
        if (!await _authorizationService.AuthorizeAsync(User, TaxationPermissions.ManageTaxation))
        {
            return Forbid();
        }

        if (!_methodOptions.Methods.TryGetValue(source, out var method))
        {
            await _notifier.ErrorAsync(H["Unable to find a calculation method with the name '{0}'.", source]);

            return RedirectToAction(nameof(Index));
        }

        var model = await _manager.NewAsync(method.Name);

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = method.DisplayName.Value,
            Editor = await _displayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        var isValid = await CatalogEntryValidation.ValidateAsync(_manager, model, _updateModelAccessor.ModelUpdater, nameof(TaxRule));

        if (isValid && ModelState.IsValid)
        {
            var existing = await _manager.FindByNameAsync(model.Name);

            if (existing != null)
            {
                ModelState.AddModelError(_nameFieldName, S["A tax rule with the same name already exists."]);
            }

            if (ModelState.IsValid)
            {
                await _manager.CreateAsync(model);
                await _notifier.SuccessAsync(H["The tax rule has been created successfully."]);

                return RedirectToAction(nameof(Index));
            }
        }

        return View(viewModel);
    }

    [Admin("taxation/rules/edit/{id}", "TaxationRulesEdit")]
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
    [Admin("taxation/rules/edit/{id}", "TaxationRulesEdit")]
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

        var isValid = await CatalogEntryValidation.ValidateAsync(_manager, model, _updateModelAccessor.ModelUpdater, nameof(TaxRule));

        if (isValid && ModelState.IsValid)
        {
            var existing = await _manager.FindByNameAsync(model.Name);

            if (existing != null && !string.Equals(existing.ItemId, model.ItemId, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(_nameFieldName, S["A tax rule with the same name already exists."]);
            }

            if (ModelState.IsValid)
            {
                await _manager.UpdateAsync(model);
                await _notifier.SuccessAsync(H["The tax rule has been updated successfully."]);

                return RedirectToAction(nameof(Index));
            }
        }

        return View(viewModel);
    }

    [HttpPost]
    [ActionName("Delete")]
    [Admin("taxation/rules/delete/{id}", "TaxationRulesDelete")]
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
            await _notifier.SuccessAsync(H["The tax rule has been deleted successfully."]);
        }

        return RedirectToAction(nameof(Index));
    }
}
