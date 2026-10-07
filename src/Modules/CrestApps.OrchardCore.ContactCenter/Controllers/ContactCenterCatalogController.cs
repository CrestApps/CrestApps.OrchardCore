using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
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
    private const string _editAction = "Edit";

    // Every Contact Center catalog indexes its name in a 255-character column.
    private const int _maxNameLength = 255;

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
    /// Gets the success notification shown after a copy of an entry is created.
    /// </summary>
    protected virtual LocalizedHtmlString ClonedNotification
        => H["A copy has been created successfully. Review it before putting it to use."];

    /// <summary>
    /// Gets the notifier, so a concrete controller can add its own messages to the shared flow.
    /// </summary>
    private protected INotifier Notifier
        => _notifier;

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

        var pager = new Pager(pagerParameters, pagerOptions.Value);
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
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>A redirect to the filtered list.</returns>
    protected async Task<IActionResult> IndexFilterPostAsync(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ManagePermission))
        {
            return Forbid();
        }

        return RedirectToAction(_indexAction, new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
            { "pageSize", pagerParameters.PageSize },
        });
    }

    /// <summary>
    /// Sets up a new entry from the request before its editor is built, for a catalog whose entries come in kinds.
    /// </summary>
    /// <param name="model">The new entry.</param>
    /// <returns><see langword="false"/> when the request asks for a kind that cannot be added.</returns>
    protected virtual Task<bool> InitializeNewAsync(TModel model)
        => Task.FromResult(true);

    /// <summary>
    /// Adjusts a copy of an existing entry before its editor is built, so it can be told apart from its source and
    /// does not claim anything only one entry may hold.
    /// </summary>
    /// <param name="clone">The new, unsaved entry carrying the configuration of <paramref name="source"/>.</param>
    /// <param name="source">The entry being cloned.</param>
    protected virtual void InitializeClone(TModel clone, TModel source)
    {
    }

    /// <summary>
    /// Gets the name proposed for a copy of an entry.
    /// </summary>
    /// <param name="sourceName">The name of the entry being cloned.</param>
    /// <param name="copyNumber">
    /// The one-based number of the attempt: <c>1</c> for the first proposal, and higher while the proposed name is
    /// already taken.
    /// </param>
    /// <returns>The proposed name.</returns>
    protected virtual string GetCloneName(string sourceName, int copyNumber)
        => copyNumber <= 1
            ? S["{0} (copy)", sourceName]
            : S["{0} (copy {1})", sourceName, copyNumber];

    /// <summary>
    /// Runs after a copy created by <see cref="ClonePostAsync(string)"/> is stored, so a concrete controller can tell the
    /// operator what the copy did not keep.
    /// </summary>
    /// <param name="clone">The stored copy.</param>
    /// <param name="source">The entry it was copied from.</param>
    /// <returns>A task that completes when the work is done.</returns>
    protected virtual Task OnClonedAsync(TModel clone, TModel source)
        => Task.CompletedTask;

    /// <summary>
    /// Displays the create form.
    /// </summary>
    /// <param name="cloneId">The identifier of an entry whose configuration the new entry starts from, if any.</param>
    /// <returns>The create view.</returns>
    protected async Task<IActionResult> CreateAsync(string cloneId = null)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ManagePermission))
        {
            return Forbid();
        }

        var model = await NewModelAsync(cloneId);

        if (model is null)
        {
            return NotFound();
        }

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
    /// <param name="cloneId">The identifier of an entry whose configuration the new entry starts from, if any.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    protected async Task<IActionResult> CreatePostAsync(string cloneId = null)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ManagePermission))
        {
            return Forbid();
        }

        var model = await NewModelAsync(cloneId);

        if (model is null)
        {
            return NotFound();
        }

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

    /// <summary>
    /// Stores a copy of an entry and opens the copy in the editor.
    /// </summary>
    /// <remarks>
    /// The copy carries every configured member of its source, including the settings other features keep in its
    /// property bag, under a new identifier and a name no other entry holds. Nothing the source has recorded while in
    /// use travels with it: measurements and runtime records are keyed by the entry identifier, so the copy starts
    /// with none. A copy the entry's rules refuse is not stored, and the reasons are shown on the list.
    /// </remarks>
    /// <param name="id">The identifier of the entry to copy.</param>
    /// <returns>A redirect to the copy's editor, or to the list when the copy cannot be stored.</returns>
    protected async Task<IActionResult> ClonePostAsync(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ManagePermission))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var source = await _manager.FindByIdAsync(id);

        if (source is null)
        {
            return NotFound();
        }

        var clone = await NewCloneAsync(source);
        SetName(clone, await GetUniqueCloneNameAsync(source.Name));

        var updater = _updateModelAccessor.ModelUpdater;

        if (!await CatalogEntryValidation.ValidateAsync(_manager, clone, updater, typeof(TModel).Name))
        {
            // There is no form to show the reasons on, so they travel with the notification instead.
            var reasons = string.Join(" ", updater.ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => error.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.Ordinal));

            await _notifier.ErrorAsync(H["The copy could not be created. {0}", reasons]);

            return RedirectToAction(_indexAction);
        }

        await _manager.CreateAsync(clone);
        await _notifier.SuccessAsync(ClonedNotification);
        await OnClonedAsync(clone, source);

        return RedirectToAction(_editAction, new RouteValueDictionary
        {
            { "id", clone.ItemId },
        });
    }

    // A clone is built the way a deployment plan would recreate its source: every configured member is carried and
    // none of the record's identity or history, so it is a new entry with the same settings. The post rebuilds it the
    // same way before binding the form, which keeps the settings the editor does not show.
    private async Task<TModel> NewModelAsync(string cloneId)
    {
        if (string.IsNullOrEmpty(cloneId))
        {
            var model = await _manager.NewAsync();

            return await InitializeNewAsync(model) ? model : null;
        }

        var source = await _manager.FindByIdAsync(cloneId);

        if (source is null)
        {
            return null;
        }

        return await NewCloneAsync(source);
    }

    private async Task<TModel> NewCloneAsync(TModel source)
    {
        var clone = await _manager.NewAsync(ContactCenterDeploymentSerializer.Export(source));

        InitializeClone(clone, source);

        return clone;
    }

    // The name contract is read-only, so a copy is renamed through the entry's own writable Name property.
    private static void SetName(TModel entry, string name)
    {
        var property = typeof(TModel).GetProperty(nameof(INameAwareModel.Name));

        if (property?.CanWrite != true)
        {
            throw new InvalidOperationException($"The catalog entry type '{typeof(TModel).FullName}' must expose a writable '{nameof(INameAwareModel.Name)}' property.");
        }

        property.SetValue(entry, name);
    }

    // Names are compared without regard to case, the way an operator reads the list.
    private async Task<string> GetUniqueCloneNameAsync(string sourceName)
    {
        var takenNames = (await _manager.GetAllAsync())
            .Select(entry => entry.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        sourceName ??= string.Empty;

        for (var copyNumber = 1; ; copyNumber++)
        {
            var name = GetCloneName(sourceName, copyNumber);

            // A long source name is shortened so the suffix still fits in the stored name.
            if (name.Length > _maxNameLength)
            {
                var overflow = name.Length - _maxNameLength;
                name = GetCloneName(sourceName[..Math.Max(0, sourceName.Length - overflow)].TrimEnd(), copyNumber);
            }

            if (!takenNames.Contains(name))
            {
                return name;
            }
        }
    }
}
