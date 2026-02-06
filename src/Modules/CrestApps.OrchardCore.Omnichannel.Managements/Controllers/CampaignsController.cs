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
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Controllers;

[Admin]
public sealed class CampaignsController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly ICatalogManager<OmnichannelCampaign> _manager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<OmnichannelCampaign> _displayDriver;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CampaignsController(
        ICatalogManager<OmnichannelCampaign> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<OmnichannelCampaign> displayManager,
        INotifier notifier,
        IHtmlLocalizer<CampaignsController> htmlLocalizer,
        IStringLocalizer<CampaignsController> stringLocalizer)
    {
        _manager = manager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayDriver = displayManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("omnichannel/campaigns", "OmnichannelCampaignsIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageCampaigns))
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

        var viewModel = new ListCatalogEntryViewModel<CatalogEntryViewModel<OmnichannelCampaign>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<OmnichannelCampaign>
            {
                Model = model,
                Shape = await _displayDriver.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        viewModel.Options.BulkActions = [];

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("omnichannel/campaigns", "OmnichannelCampaignsIndex")]
    public ActionResult IndexFilterPost(ListCatalogEntryViewModel model)
    {
        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    [Admin("omnichannel/campaigns/create", "OmnichannelCampaignsCreate")]
    public async Task<ActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageCampaigns))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["Campaign"],
            Editor = await _displayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("omnichannel/campaigns/create", "OmnichannelCampaignsCreate")]
    public async Task<ActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageCampaigns))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["New Campaign"],
            Editor = await _displayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _manager.CreateAsync(model);

            await _notifier.SuccessAsync(H["A new Campaign has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }

    [Admin("omnichannel/campaigns/edit/{id}", "OmnichannelCampaignsEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageCampaigns))
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
            Editor = await _displayDriver.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("omnichannel/campaigns/edit/{id}", "OmnichannelCampaignsEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, OmnichannelConstants.Permissions.ManageCampaigns))
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
            Editor = await _displayDriver.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _manager.UpdateAsync(model);

            await _notifier.SuccessAsync(H["The Campaign has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(viewModel);
    }
}
