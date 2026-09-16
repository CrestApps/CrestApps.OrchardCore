using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Provides the shared list/create/edit/delete orchestration for a Contact Center catalog entry type.
/// Concrete controllers supply only the routing shell, the managing permission, and the localized labels,
/// so every catalog admin section enforces the same authorization, validation, and notification flow.
/// </summary>
/// <typeparam name="TModel">The catalog entry type administered by the controller.</typeparam>
public abstract class ContactCenterCatalogController<TModel> : Controller
    where TModel : CatalogItem, INameAwareModel, new()
{
    private const string _optionsSearch = "Options.Search";
    private const string _indexAction = "Index";

    private readonly ICatalogManager<TModel> _manager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<TModel> _displayManager;
    private readonly INotifier _notifier;

    private protected readonly IHtmlLocalizer H;
    private protected readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterCatalogController{TModel}"/> class.
    /// </summary>
    /// <param name="manager">The catalog manager that owns the entry type.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer resolved for the concrete controller.</param>
    /// <param name="stringLocalizer">The string localizer resolved for the concrete controller.</param>
    protected ContactCenterCatalogController(
        ICatalogManager<TModel> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<TModel> displayManager,
        INotifier notifier,
        IHtmlLocalizer htmlLocalizer,
        IStringLocalizer stringLocalizer)
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
    /// Gets the permission that guards every action on the catalog section.
    /// </summary>
    protected abstract Permission ManagePermission { get; }

    /// <summary>
    /// Gets the label shown on the create form for a new entry.
    /// </summary>
    protected abstract LocalizedString CreateDisplayName { get; }

    /// <summary>
    /// Gets the label shown while binding a submitted new entry.
    /// </summary>
    protected abstract LocalizedString NewDisplayName { get; }

    /// <summary>
    /// Gets the success notification shown after an entry is created.
    /// </summary>
    protected abstract LocalizedHtmlString CreatedNotification { get; }

    /// <summary>
    /// Gets the success notification shown after an entry is updated.
    /// </summary>
    protected abstract LocalizedHtmlString UpdatedNotification { get; }

    /// <summary>
    /// Gets the success notification shown after an entry is deleted.
    /// </summary>
    protected abstract LocalizedHtmlString DeletedNotification { get; }

    /// <summary>
    /// Lists the catalog entries.
    /// </summary>
    /// <param name="options">The catalog entry options.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list view.</returns>
    protected async Task<IActionResult> IndexAsync(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        IOptions<PagerOptions> pagerOptions,
        IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ManagePermission))
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

        var viewModel = new ListCatalogEntryViewModel<CatalogEntryViewModel<TModel>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<TModel>
            {
                Model = model,
                Shape = await _displayManager.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin"),
            });
        }

        return View(viewModel);
    }

    /// <summary>
    /// Applies the list filter.
    /// </summary>
    /// <param name="model">The submitted list model.</param>
    /// <returns>A redirect to the filtered list.</returns>
    protected async Task<IActionResult> IndexFilterPostAsync(ListCatalogEntryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ManagePermission))
        {
            return Forbid();
        }

        return RedirectToAction(_indexAction, new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    /// <summary>
    /// Displays the create form.
    /// </summary>
    /// <returns>The create view.</returns>
    protected async Task<IActionResult> CreateAsync()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ManagePermission))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();
        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = CreateDisplayName,
            Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(viewModel);
    }

    /// <summary>
    /// Persists a new entry.
    /// </summary>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    protected async Task<IActionResult> CreatePostAsync()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ManagePermission))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();
        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = NewDisplayName,
            Editor = await _displayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        var isValid = await CatalogEntryValidation.ValidateAsync(_manager, model, _updateModelAccessor.ModelUpdater, typeof(TModel).Name);

        if (isValid && ModelState.IsValid)
        {
            await _manager.CreateAsync(model);
            await _notifier.SuccessAsync(CreatedNotification);

            return RedirectToAction(_indexAction);
        }

        return View(viewModel);
    }

    /// <summary>
    /// Displays the edit form.
    /// </summary>
    /// <param name="id">The entry identifier.</param>
    /// <returns>The edit view.</returns>
    protected async Task<IActionResult> EditAsync(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ManagePermission))
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
    /// Persists changes to an entry.
    /// </summary>
    /// <param name="id">The entry identifier.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    protected async Task<IActionResult> EditPostAsync(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ManagePermission))
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

        var isValid = await CatalogEntryValidation.ValidateAsync(_manager, model, _updateModelAccessor.ModelUpdater, typeof(TModel).Name);

        if (isValid && ModelState.IsValid)
        {
            await _manager.UpdateAsync(model);
            await _notifier.SuccessAsync(UpdatedNotification);

            return RedirectToAction(_indexAction);
        }

        return View(viewModel);
    }

    /// <summary>
    /// Deletes an entry.
    /// </summary>
    /// <param name="id">The entry identifier.</param>
    /// <returns>A redirect to the list.</returns>
    protected async Task<IActionResult> DeleteAsync(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ManagePermission))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model is not null)
        {
            await _manager.DeleteAsync(model);
            await _notifier.SuccessAsync(DeletedNotification);
        }

        return RedirectToAction(_indexAction);
    }
}
