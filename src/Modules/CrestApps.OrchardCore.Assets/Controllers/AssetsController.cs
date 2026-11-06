using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.ViewModels;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using YesSql;
using YesSql.Services;
using Asset = CrestApps.OrchardCore.Assets.Models.Asset;

namespace CrestApps.OrchardCore.Assets.Controllers;

/// <summary>
/// Assets: the list, the editor and an asset's page with its hierarchy and meter readings.
/// </summary>
public sealed class AssetsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly AssetManager _manager;
    private readonly AssetTypeManager _typeManager;
    private readonly AssetService _service;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly INotifier _notifier;
    private readonly AssetOwnerDirectory _owners;
    private readonly IDisplayManager<Asset> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="manager">The asset manager.</param>
    /// <param name="typeManager">The asset type manager.</param>
    /// <param name="service">The asset service.</param>
    /// <param name="session">The session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="owners">The directory of asset owners.</param>
    /// <param name="displayManager">The display manager that builds the asset editor from its drivers.</param>
    /// <param name="updateModelAccessor">The accessor of the model updater.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public AssetsController(
        IAuthorizationService authorizationService,
        AssetManager manager,
        AssetTypeManager typeManager,
        AssetService service,
        ISession session,
        IClock clock,
        INotifier notifier,
        AssetOwnerDirectory owners,
        IDisplayManager<Asset> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IHtmlLocalizer<AssetsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _manager = manager;
        _typeManager = typeManager;
        _service = service;
        _session = session;
        _clock = clock;
        _notifier = notifier;
        _owners = owners;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Lists the assets, searched by name, number or serial number, by type and state.
    /// </summary>
    /// <param name="form">The search and filters.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("assets", "Assets")]
    public async Task<IActionResult> Index(AssetsListViewModel form, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssetsPermissions.ViewAssets))
        {
            return Forbid();
        }

        var types = (await _typeManager.GetAllAsync(HttpContext.RequestAborted)).OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var query = _session.Query<Asset, AssetIndex>(collection: AssetsConstants.CollectionName);

        if (!string.IsNullOrEmpty(form.TypeId))
        {
            query = query.Where(index => index.AssetTypeId == form.TypeId);
        }

        if (Enum.TryParse<AssetStatus>(form.Status, out var status))
        {
            var value = status.ToString();

            query = query.Where(index => index.Status == value);
        }

        if (Enum.TryParse<AssetOwnerKind>(form.OwnerKind, out var ownerKind) && !string.IsNullOrEmpty(form.OwnerId))
        {
            var kind = ownerKind.ToString();
            var ownerId = form.OwnerId;

            query = query.Where(index => index.OwnerKind == kind && index.OwnerId == ownerId);
            form.OwnerName = await _owners.GetNameAsync(ownerKind, ownerId, HttpContext.RequestAborted) ?? ownerId;
        }

        if (!string.IsNullOrWhiteSpace(form.Q))
        {
            var q = form.Q.Trim();

            query = query.Where(index => index.Name.Contains(q) || index.Number.Contains(q) || index.SerialNumber.Contains(q));
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var count = await query.CountAsync(HttpContext.RequestAborted);
        var page = await query.OrderBy(index => index.Name).Skip(pager.GetStartIndex()).Take(pager.PageSize).ListAsync(HttpContext.RequestAborted);
        var parentIds = page.Select(asset => asset.ParentAssetId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
        var parents = parentIds.Length == 0 ? [] : (await _session.QueryIndex<AssetIndex>(index => index.ItemId.IsIn(parentIds), collection: AssetsConstants.CollectionName).ListAsync(HttpContext.RequestAborted)).ToDictionary(index => index.ItemId, index => index.Name, StringComparer.Ordinal);
        var typeNames = types.ToDictionary(type => type.ItemId, type => type.Name, StringComparer.Ordinal);
        var ownerNames = await _owners.GetNamesAsync(page, HttpContext.RequestAborted);
        var today = DateOnly.FromDateTime(_clock.UtcNow);

        form.TypeOptions = [.. types.Select(type => new SelectListItem(type.Name, type.ItemId, type.ItemId == form.TypeId))];
        form.List = new AdminListContext<AssetRow>
        {
            Source = page.Select(asset => new AssetRow
            {
                Asset = asset,
                TypeName = typeNames.GetValueOrDefault(asset.AssetTypeId ?? string.Empty),
                ParentName = parents.GetValueOrDefault(asset.ParentAssetId ?? string.Empty),
                OwnerName = asset.OwnerKind == AssetOwnerKind.Internal ? null : ownerNames.GetValueOrDefault(AssetOwnerDirectory.Key(asset.OwnerKind, asset.OwnerId ?? string.Empty)) ?? asset.OwnerId,
                Today = today,
            }).ToList(),
            Pager = await shapeFactory.PagerAsync(pager, count),
        };

        return View(form);
    }

    /// <summary>
    /// Shows an asset with its hierarchy and meter readings.
    /// </summary>
    /// <param name="id">The asset.</param>
    /// <returns>The page.</returns>
    [Admin("assets/view/{id}", "AssetsView")]
    public async Task<IActionResult> Details(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssetsPermissions.ViewAssets))
        {
            return Forbid();
        }

        var model = await DetailsAsync(id);

        return model is null ? NotFound() : View(model);
    }

    /// <summary>
    /// Records a meter reading.
    /// </summary>
    /// <param name="id">The asset.</param>
    /// <param name="form">The reading.</param>
    /// <returns>The asset's page.</returns>
    [HttpPost]
    [ActionName(nameof(Details))]
    [Admin("assets/view/{id}", "AssetsView")]
    public async Task<IActionResult> RecordReading(string id, AssetDetailsViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssetsPermissions.ManageAssets))
        {
            return Forbid();
        }

        if (form.Value is null)
        {
            ModelState.AddModelError(nameof(form.Value), H["Enter the reading."].Value);
        }
        else
        {
            var errors = await _service.RecordReadingAsync(new AssetMeterReading
            {
                AssetId = id,
                Meter = form.Meter,
                Value = form.Value.Value,
                Source = MeterReadingSource.Manual,
                RecordedBy = User.Identity?.Name,
            }, HttpContext.RequestAborted);

            foreach (var error in errors)
            {
                ModelState.AddModelError(nameof(form.Value), error);
            }
        }

        if (!ModelState.IsValid)
        {
            var model = await DetailsAsync(id);

            if (model is null)
            {
                return NotFound();
            }

            model.Meter = form.Meter;
            model.Value = form.Value;

            return View(model);
        }

        await _notifier.SuccessAsync(H["The reading was recorded."]);

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Shows the editor, built by the display drivers of <see cref="Asset"/>.
    /// </summary>
    /// <param name="id">The asset, or <see langword="null"/> for a new one.</param>
    /// <param name="assetTypeId">The type of a new asset.</param>
    /// <param name="parentAssetId">The asset a new asset is part of.</param>
    /// <param name="ownerKind">The kind of owner of a new asset.</param>
    /// <param name="ownerId">The owner of a new asset.</param>
    /// <returns>The page.</returns>
    [Admin("assets/edit/{id?}", "AssetsEdit")]
    public async Task<IActionResult> Edit(string id, string assetTypeId, string parentAssetId, AssetOwnerKind? ownerKind, string ownerId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssetsPermissions.ManageAssets))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(id);
        var item = isNew ? await _manager.NewAsync() : await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        if (isNew)
        {
            item.AssetTypeId = assetTypeId;
            item.ParentAssetId = parentAssetId;

            if (ownerKind is { } newKind && !string.IsNullOrEmpty(ownerId))
            {
                item.OwnerKind = newKind;
                item.OwnerId = ownerId;
            }
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = isNew ? null : item.Name,
            Editor = await _displayManager.BuildEditorAsync(item, _updateModelAccessor.ModelUpdater, isNew),
        });
    }

    /// <summary>
    /// Saves the editor.
    /// </summary>
    /// <param name="id">The asset, or <see langword="null"/> for a new one.</param>
    /// <returns>The asset's page, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("assets/edit/{id?}", "AssetsEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssetsPermissions.ManageAssets))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(id);
        var item = isNew ? await _manager.NewAsync() : await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var displayName = isNew ? null : item.Name;
        var editor = await _displayManager.UpdateEditorAsync(item, _updateModelAccessor.ModelUpdater, isNew);
        var isValid = await CatalogEntryValidation.ValidateAsync(_manager, item, _updateModelAccessor.ModelUpdater, nameof(Asset), HttpContext.RequestAborted);

        if (!isValid || !ModelState.IsValid)
        {
            // What a section's driver wrote while the editor was updated is not kept.
            await _session.CancelAsync();

            return View(new EditCatalogEntryViewModel
            {
                DisplayName = displayName,
                Editor = editor,
            });
        }

        if (isNew)
        {
            await _manager.CreateAsync(item);
        }
        else
        {
            await _manager.UpdateAsync(item);
        }

        await _notifier.SuccessAsync(H["The asset was saved."]);

        return RedirectToAction(nameof(Details), new { id = item.ItemId });
    }

    /// <summary>
    /// Deletes an asset with no assets inside it.
    /// </summary>
    /// <param name="id">The asset.</param>
    /// <returns>The list.</returns>
    [HttpPost]
    [Admin("assets/delete/{id}", "AssetsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssetsPermissions.ManageAssets))
        {
            return Forbid();
        }

        var item = await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        if ((await _service.ChildrenAsync(id, HttpContext.RequestAborted)).Count > 0)
        {
            await _notifier.WarningAsync(H["{0} can't be deleted: other assets are inside it.", item.Name]);

            return RedirectToAction(nameof(Details), new { id });
        }

        await _manager.DeleteAsync(item);
        await _notifier.SuccessAsync(H["The asset was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    private async Task<AssetDetailsViewModel> DetailsAsync(string id)
    {
        var asset = await _manager.FindByIdAsync(id);

        if (asset is null)
        {
            return null;
        }

        return new AssetDetailsViewModel
        {
            Asset = asset,
            Type = string.IsNullOrEmpty(asset.AssetTypeId) ? null : await _typeManager.FindByIdAsync(asset.AssetTypeId),
            Ancestors = await _service.AncestorsAsync(asset, HttpContext.RequestAborted),
            Children = await _service.ChildrenAsync(asset.ItemId, HttpContext.RequestAborted),
            Readings = await _service.ReadingsAsync(asset.ItemId, cancellationToken: HttpContext.RequestAborted),
            Today = DateOnly.FromDateTime(_clock.UtcNow),
            CanManage = await _authorizationService.AuthorizeAsync(User, AssetsPermissions.ManageAssets),
            OwnerName = asset.OwnerKind == AssetOwnerKind.Internal ? null : await _owners.GetNameAsync(asset.OwnerKind, asset.OwnerId, HttpContext.RequestAborted) ?? asset.OwnerId,
        };
    }
}
