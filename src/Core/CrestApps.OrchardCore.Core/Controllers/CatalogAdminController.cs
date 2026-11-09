using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
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
using OrchardCore.Security.Permissions;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.Core.Controllers;

/// <summary>
/// The admin controller a catalog section starts from: it lists the entries on <c>&lt;admin-list&gt;</c>, and creates,
/// edits, copies and deletes them with the same authorization, validation and notification flow everywhere.
/// </summary>
/// <remarks>
/// A concrete controller supplies the routes (one public action per page, each calling the matching protected method
/// here), the managing permission and its labels. It changes the flow only through the virtual hooks, such as
/// <see cref="ValidateEntryAsync"/> for a rule the editor adds, <see cref="OnSavedAsync"/> for work after a save, and
/// <see cref="CanDeleteAsync"/> to refuse a delete.
/// </remarks>
/// <typeparam name="TModel">The catalog entry type administered by the controller.</typeparam>
public abstract class CatalogAdminController<TModel> : Controller
    where TModel : CatalogItem, new()
{
    private const string _optionsSearch = "Options.Search";
    private const string _indexAction = "Index";
    private const string _editAction = "Edit";

    // Catalogs index the name in a 255-character column.
    private const int _maxNameLength = 255;

    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<TModel> _displayManager;

    /// <summary>
    /// The localizer for the HTML messages, resolved for the concrete controller.
    /// </summary>
    protected readonly IHtmlLocalizer H;

    /// <summary>
    /// The localizer for the text messages, resolved for the concrete controller.
    /// </summary>
    protected readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogAdminController{TModel}"/> class.
    /// </summary>
    /// <param name="manager">The catalog manager that owns the entry type.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="updateModelAccessor">The update model accessor.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer resolved for the concrete controller.</param>
    /// <param name="stringLocalizer">The string localizer resolved for the concrete controller.</param>
    protected CatalogAdminController(
        ICatalogManager<TModel> manager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<TModel> displayManager,
        INotifier notifier,
        IHtmlLocalizer htmlLocalizer,
        IStringLocalizer stringLocalizer)
    {
        Manager = manager;
        AuthorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _displayManager = displayManager;
        Notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the catalog manager that owns the entry type.
    /// </summary>
    protected ICatalogManager<TModel> Manager { get; }

    /// <summary>
    /// Gets the authorization service.
    /// </summary>
    protected IAuthorizationService AuthorizationService { get; }

    /// <summary>
    /// Gets the notifier, so a concrete controller can add its own messages to the shared flow.
    /// </summary>
    protected INotifier Notifier { get; }

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
    /// Gets the editor prefix the entry's rules report their failures against.
    /// </summary>
    protected virtual string ValidationPrefix
        => typeof(TModel).Name;

    /// <summary>
    /// Gets the name an entry is shown by: its <see cref="INameAwareModel.Name"/> or
    /// <see cref="IDisplayTextAwareModel.DisplayText"/>.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The entry's name.</returns>
    protected virtual string GetName(TModel entry)
        => entry switch
        {
            INameAwareModel named => named.Name,
            IDisplayTextAwareModel titled => titled.DisplayText,
            _ => entry?.ItemId,
        };

    /// <summary>
    /// Sets the name of an entry, for a copy. The name contracts are read-only, so each catalog renames a copy through
    /// its own model. A catalog that offers no copy leaves it as it is.
    /// </summary>
    /// <param name="entry">The entry to rename.</param>
    /// <param name="name">The new name.</param>
    protected virtual void SetName(TModel entry, string name)
        => throw new NotSupportedException($"{GetType().Name} does not copy entries.");

    /// <summary>
    /// Lists the catalog entries a page at a time, filtered by the search the list submitted.
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
        if (!await AuthorizeAsync())
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var result = await Manager.PageAsync(pager.Page, pager.PageSize, new QueryContext
        {
            Name = options.Search,
        });

        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var pagerShape = await shapeFactory.PagerAsync(pager, result.Count, routeData);

        options.BulkActions ??= [];

        return View(ListViewModel(result.Entries, options, pagerShape));
    }

    /// <summary>
    /// Lists every catalog entry on one page, in the order <see cref="OrderAll"/> gives, so the list's search filters
    /// the rows in the browser.
    /// </summary>
    /// <returns>The list view.</returns>
    protected async Task<IActionResult> IndexAllAsync()
    {
        if (!await AuthorizeAsync())
        {
            return Forbid();
        }

        return View(ListViewModel(OrderAll(await Manager.GetAllAsync()).ToList(), options: null, pager: null));
    }

    /// <summary>
    /// Orders the entries <see cref="IndexAllAsync"/> lists.
    /// </summary>
    /// <param name="entries">The stored entries.</param>
    /// <returns>The entries in the order they are listed.</returns>
    protected virtual IEnumerable<TModel> OrderAll(IEnumerable<TModel> entries)
        => entries.OrderBy(GetName, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Applies the list filter.
    /// </summary>
    /// <param name="model">The submitted list model.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <returns>A redirect to the filtered list.</returns>
    protected async Task<IActionResult> IndexFilterPostAsync(ListCatalogEntryViewModel model, PagerParameters pagerParameters)
    {
        if (!await AuthorizeAsync())
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
    /// Checks a bound entry against the rules only the editor adds, once the entry's own rules have passed. A failed
    /// rule is added to <see cref="ControllerBase.ModelState"/>.
    /// </summary>
    /// <param name="model">The bound entry.</param>
    /// <param name="isNew">Whether the entry is being created.</param>
    /// <returns>A task that completes when the rules have run.</returns>
    protected virtual Task ValidateEntryAsync(TModel model, bool isNew)
        => Task.CompletedTask;

    /// <summary>
    /// Runs after an entry is created or updated through the editor, before the success notification.
    /// </summary>
    /// <param name="model">The saved entry.</param>
    /// <param name="isNew">Whether the entry was created.</param>
    /// <returns>A task that completes when the work is done.</returns>
    protected virtual Task OnSavedAsync(TModel model, bool isNew)
        => Task.CompletedTask;

    /// <summary>
    /// Decides whether an entry may be deleted. A controller that refuses tells the operator why.
    /// </summary>
    /// <param name="model">The entry to delete.</param>
    /// <returns><see langword="true"/> to delete the entry; otherwise, <see langword="false"/>.</returns>
    protected virtual Task<bool> CanDeleteAsync(TModel model)
        => Task.FromResult(true);

    /// <summary>
    /// Displays the create form.
    /// </summary>
    /// <param name="cloneId">The identifier of an entry whose configuration the new entry starts from, if any.</param>
    /// <returns>The create view.</returns>
    protected async Task<IActionResult> CreateAsync(string cloneId = null)
    {
        if (!await AuthorizeAsync())
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
        if (!await AuthorizeAsync())
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

        var rulesPassed = await CatalogEntryValidation.ValidateAsync(Manager, model, _updateModelAccessor.ModelUpdater, ValidationPrefix);

        if (await IsValidAsync(model, rulesPassed, isNew: true))
        {
            await Manager.CreateAsync(model);
            await OnSavedAsync(model, isNew: true);
            await Notifier.SuccessAsync(CreatedNotification);

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
        if (!await AuthorizeAsync())
        {
            return Forbid();
        }

        var model = await FindAsync(id);

        if (model is null)
        {
            return NotFound();
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = GetName(model),
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
        if (!await AuthorizeAsync())
        {
            return Forbid();
        }

        var model = await FindAsync(id);

        if (model is null)
        {
            return NotFound();
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = GetName(model),
            Editor = await _displayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        var rulesPassed = await CatalogEntryValidation.ValidateAsync(Manager, model, _updateModelAccessor.ModelUpdater, ValidationPrefix);

        if (await IsValidAsync(model, rulesPassed, isNew: false))
        {
            await Manager.UpdateAsync(model);
            await OnSavedAsync(model, isNew: false);
            await Notifier.SuccessAsync(UpdatedNotification);

            return RedirectToAction(_indexAction);
        }

        return View(viewModel);
    }

    /// <summary>
    /// Deletes an entry. A missing entry is not an error: the list is shown again.
    /// </summary>
    /// <param name="id">The entry identifier.</param>
    /// <returns>A redirect to the list.</returns>
    protected async Task<IActionResult> DeleteAsync(string id)
    {
        if (!await AuthorizeAsync())
        {
            return Forbid();
        }

        var model = await FindAsync(id);

        if (model is not null && await CanDeleteAsync(model) && await Manager.DeleteAsync(model))
        {
            await Notifier.SuccessAsync(DeletedNotification);
        }

        return RedirectToAction(_indexAction);
    }

    /// <summary>
    /// Deletes the entries checked on the list, skipping those <see cref="CanDeleteAsync"/> refuses.
    /// </summary>
    /// <param name="itemIds">The identifiers of the checked entries.</param>
    /// <returns>A redirect to the list.</returns>
    protected async Task<IActionResult> BulkDeleteAsync(IEnumerable<string> itemIds)
    {
        if (!await AuthorizeAsync())
        {
            return Forbid();
        }

        var deleted = 0;

        foreach (var id in itemIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal) ?? [])
        {
            var model = await FindAsync(id);

            if (model is not null && await CanDeleteAsync(model) && await Manager.DeleteAsync(model))
            {
                deleted++;
            }
        }

        if (deleted > 0)
        {
            await Notifier.SuccessAsync(H["{0} entries have been deleted successfully.", deleted]);
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
        if (!await AuthorizeAsync())
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var source = await FindAsync(id);

        if (source is null)
        {
            return NotFound();
        }

        var clone = await NewCloneAsync(source);
        SetName(clone, await GetUniqueCloneNameAsync(GetName(source)));

        var updater = _updateModelAccessor.ModelUpdater;

        if (!await CatalogEntryValidation.ValidateAsync(Manager, clone, updater, ValidationPrefix))
        {
            // There is no form to show the reasons on, so they travel with the notification instead.
            var reasons = string.Join(" ", updater.ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => error.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.Ordinal));

            await Notifier.ErrorAsync(H["The copy could not be created. {0}", reasons]);

            return RedirectToAction(_indexAction);
        }

        await Manager.CreateAsync(clone);
        await Notifier.SuccessAsync(ClonedNotification);
        await OnClonedAsync(clone, source);

        return RedirectToAction(_editAction, new RouteValueDictionary
        {
            { "id", clone.ItemId },
        });
    }

    /// <summary>
    /// Checks whether the current user holds <see cref="ManagePermission"/>.
    /// </summary>
    /// <returns><see langword="true"/> when the user may manage the catalog.</returns>
    protected async Task<bool> AuthorizeAsync()
        => await AuthorizationService.AuthorizeAsync(User, ManagePermission);

    private async Task<TModel> FindAsync(string id)
        => string.IsNullOrEmpty(id) ? null : await Manager.FindByIdAsync(id);

    // The entry's own rules run first, in the action, so the editor-only rules only add to a model that passed them.
    private async Task<bool> IsValidAsync(TModel model, bool rulesPassed, bool isNew)
    {
        if (!rulesPassed || !ModelState.IsValid)
        {
            return false;
        }

        await ValidateEntryAsync(model, isNew);

        return ModelState.IsValid;
    }

    private static ListCatalogEntryViewModel<CatalogEntryViewModel<TModel>> ListViewModel(IEnumerable<TModel> entries, CatalogEntryOptions options, object pager)
    {
        var source = entries as IReadOnlyList<TModel> ?? entries.ToList();

        return new ListCatalogEntryViewModel<CatalogEntryViewModel<TModel>>
        {
            Models = source.Select(model => new CatalogEntryViewModel<TModel> { Model = model }).ToList(),
            Options = options,
            Pager = pager,

            // The admin-list tag builds a row per entry, in the layout (List or Grid) the site or the user picked.
            List = new AdminListContext<TModel>
            {
                Source = source,
                Pager = pager as IShape,
            },
        };
    }

    // A clone is built the way a deployment plan would recreate its source: every configured member is carried and
    // none of the record's identity or history, so it is a new entry with the same settings. The post rebuilds it the
    // same way before binding the form, which keeps the settings the editor does not show.
    private async Task<TModel> NewModelAsync(string cloneId)
    {
        if (string.IsNullOrEmpty(cloneId))
        {
            var model = await Manager.NewAsync();

            return await InitializeNewAsync(model) ? model : null;
        }

        var source = await FindAsync(cloneId);

        if (source is null)
        {
            return null;
        }

        return await NewCloneAsync(source);
    }

    private async Task<TModel> NewCloneAsync(TModel source)
    {
        var clone = await Manager.NewAsync(CatalogDeploymentSerializer.Export(source));

        InitializeClone(clone, source);

        return clone;
    }

    // Names are compared without regard to case, the way an operator reads the list.
    private async Task<string> GetUniqueCloneNameAsync(string sourceName)
    {
        var takenNames = (await Manager.GetAllAsync())
            .Select(GetName)
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
