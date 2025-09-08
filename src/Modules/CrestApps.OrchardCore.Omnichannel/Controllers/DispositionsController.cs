using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Services;
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

namespace CrestApps.OrchardCore.Omnichannel.Controllers;

[Feature(OmnichannelConstants.Features.Managements)]
public sealed class DispositionsController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly ICatalogManager<OmnichannelDisposition> _manager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<OmnichannelDisposition> _instanceDisplayDriver;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public DispositionsController(
        ICatalogManager<OmnichannelDisposition> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<OmnichannelDisposition> instanceDisplayManager,
        INotifier notifier,
        IHtmlLocalizer<DispositionsController> htmlLocalizer,
        IStringLocalizer<DispositionsController> stringLocalizer)
    {
        _manager = manager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _instanceDisplayDriver = instanceDisplayManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("omnichannel/dispositions", "OmnichannelDispositionsIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageDispositions))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _manager.PageAsync(pager.Page, pager.PageSize, new QueryContext
        {
            Name = options.Search,
        });

        // Maintain previous route data when generating page links.
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var viewModel = new ListCatalogEntryViewModel<CatalogEntryViewModel<OmnichannelDisposition>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<OmnichannelDisposition>
            {
                Model = model,
                Shape = await _instanceDisplayDriver.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
            });
        }

        viewModel.Options.BulkActions = [];

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("omnichannel/dispositions", "OmnichannelDispositionsIndex")]
    public ActionResult IndexFilterPost(ListCatalogEntryViewModel model)
    {
        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    [Admin("omnichannel/dispositions/create", "OmnichannelDispositionsCreate")]
    public async Task<ActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageDispositions))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["Disposition"],
            Editor = await _instanceDisplayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("omnichannel/dispositions/create", "OmnichannelDispositionsCreate")]
    public async Task<ActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageDispositions))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["New Disposition"],
            Editor = await _instanceDisplayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _manager.CreateAsync(model);

            await _notifier.SuccessAsync(H["A new Disposition has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    [Admin("omnichannel/dispositions/edit/{id}", "OmnichannelDispositionsEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageDispositions))
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
            DisplayName = model.DisplayText,
            Editor = await _instanceDisplayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("omnichannel/dispositions/edit/{id}", "OmnichannelDispositionsEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageDispositions))
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
            DisplayName = model.DisplayText,
            Editor = await _instanceDisplayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _manager.UpdateAsync(model);

            await _notifier.SuccessAsync(H["The Disposition has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }
}
