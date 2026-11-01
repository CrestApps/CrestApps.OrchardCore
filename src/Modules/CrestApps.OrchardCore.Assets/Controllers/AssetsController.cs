using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Fleet;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
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
    private static readonly AssetCategory[] _fleetCategories = [AssetCategory.Vehicle, AssetCategory.Equipment, AssetCategory.Tool];

    private readonly IAuthorizationService _authorizationService;
    private readonly AssetManager _manager;
    private readonly AssetTypeManager _typeManager;
    private readonly AssetService _service;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly INotifier _notifier;
    private readonly bool _fleet;

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
    /// <param name="fleet">The Fleet feature, when it is on.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public AssetsController(
        IAuthorizationService authorizationService,
        AssetManager manager,
        AssetTypeManager typeManager,
        AssetService service,
        ISession session,
        IClock clock,
        INotifier notifier,
        IEnumerable<FleetFeature> fleet,
        IHtmlLocalizer<AssetsController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _manager = manager;
        _typeManager = typeManager;
        _service = service;
        _session = session;
        _clock = clock;
        _notifier = notifier;
        _fleet = fleet.Any();
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
        var today = DateOnly.FromDateTime(_clock.UtcNow);

        form.TypeOptions = [.. types.Select(type => new SelectListItem(type.Name, type.ItemId, type.ItemId == form.TypeId))];
        form.List = new AdminListContext<AssetRow>
        {
            Source = page.Select(asset => new AssetRow
            {
                Asset = asset,
                TypeName = typeNames.GetValueOrDefault(asset.AssetTypeId ?? string.Empty),
                ParentName = parents.GetValueOrDefault(asset.ParentAssetId ?? string.Empty),
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
    /// Shows the editor.
    /// </summary>
    /// <param name="id">The asset, or <see langword="null"/> for a new one.</param>
    /// <param name="assetTypeId">The type of a new asset.</param>
    /// <param name="parentAssetId">The asset a new asset is part of.</param>
    /// <returns>The page.</returns>
    [Admin("assets/edit/{id?}", "AssetsEdit")]
    public async Task<IActionResult> Edit(string id, string assetTypeId, string parentAssetId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssetsPermissions.ManageAssets))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(id) ? await _manager.NewAsync() : await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var isNew = string.IsNullOrEmpty(id);
        var model = new AssetEditViewModel
        {
            Id = id,
            AssetTypeId = isNew ? assetTypeId : item.AssetTypeId,
            Number = item.Number,
            Name = item.Name,
            SerialNumber = item.SerialNumber,
            Manufacturer = item.Manufacturer,
            Model = item.Model,
            Status = item.Status,
            OwnerKind = item.OwnerKind,
            OwnerId = item.OwnerId,
            ParentAssetId = isNew ? parentAssetId : item.ParentAssetId,
            InstallDate = item.InstallDate,
            AddressLine1 = item.Location?.AddressLine1,
            City = item.Location?.City,
            Region = item.Location?.Region,
            PostalCode = item.Location?.PostalCode,
            Country = item.Location?.Country,
            WarrantyStart = item.Warranty?.StartDate,
            WarrantyEnd = item.Warranty?.EndDate,
            WarrantyProvider = item.Warranty?.Provider,
            Attributes = new Dictionary<string, string>(item.Attributes, StringComparer.OrdinalIgnoreCase),
            Documents = string.Join(Environment.NewLine, item.Documents),
            Schedulable = item.Fleet?.Schedulable ?? false,
            CarriesStock = item.Fleet?.CarriesStock ?? false,
            Plate = item.Fleet?.Plate,
            Vin = item.Fleet?.Vin,
            Capacity = item.Fleet?.Capacity,
            FuelType = item.Fleet?.FuelType,
        };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves the editor.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>The asset's page, or the editor with its errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("assets/edit/{id?}", "AssetsEdit")]
    public async Task<IActionResult> EditPost(AssetEditViewModel form)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AssetsPermissions.ManageAssets))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(form.Id);
        var item = isNew ? await _manager.NewAsync() : await _manager.FindByIdAsync(form.Id);

        if (item is null)
        {
            return NotFound();
        }

        var type = string.IsNullOrEmpty(form.AssetTypeId) ? null : await _typeManager.FindByIdAsync(form.AssetTypeId);

        item.AssetTypeId = type?.ItemId;
        item.Number = string.IsNullOrWhiteSpace(form.Number) ? null : form.Number.Trim();
        item.Name = form.Name?.Trim();
        item.SerialNumber = Blank(form.SerialNumber);
        item.Manufacturer = Blank(form.Manufacturer);
        item.Model = Blank(form.Model);
        item.Status = form.Status;
        item.OwnerKind = form.OwnerKind;
        item.OwnerId = form.OwnerKind == AssetOwnerKind.Internal ? null : Blank(form.OwnerId);
        item.ParentAssetId = Blank(form.ParentAssetId);
        item.InstallDate = form.InstallDate;
        item.Location = MakeAddress(form, item.Location);
        item.Warranty = new AssetWarranty { StartDate = form.WarrantyStart, EndDate = form.WarrantyEnd, Provider = Blank(form.WarrantyProvider), Terms = item.Warranty?.Terms };
        item.Attributes = new Dictionary<string, string>(
            (type?.Attributes ?? []).Select(attribute => (attribute.Name, Value: form.Attributes?.GetValueOrDefault(attribute.Name))).Where(entry => !string.IsNullOrWhiteSpace(entry.Value)).Select(entry => KeyValuePair.Create(entry.Name, entry.Value.Trim())),
            StringComparer.OrdinalIgnoreCase);
        item.Documents = [.. (form.Documents ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        if (_fleet && type is not null && _fleetCategories.Contains(type.Category) && form.OwnerKind == AssetOwnerKind.Internal)
        {
            item.Fleet ??= new AssetFleet();
            item.Fleet.Schedulable = form.Schedulable;
            item.Fleet.CarriesStock = form.CarriesStock;
            item.Fleet.Plate = Blank(form.Plate);
            item.Fleet.Vin = Blank(form.Vin);
            item.Fleet.Capacity = Blank(form.Capacity);
            item.Fleet.FuelType = Blank(form.FuelType);
        }

        var validation = await _manager.ValidateAsync(item);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(form);

            return View(form);
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
        };
    }

    private async Task PopulateAsync(AssetEditViewModel model)
    {
        var types = (await _typeManager.GetAllAsync(HttpContext.RequestAborted)).OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        model.Type = types.FirstOrDefault(type => type.ItemId == model.AssetTypeId);
        model.TypeOptions = [.. types.Select(type => new SelectListItem(type.Name, type.ItemId, type.ItemId == model.AssetTypeId))];
        model.ParentOptions = [.. (await _session.QueryIndex<AssetIndex>(index => index.ItemId != model.Id, collection: AssetsConstants.CollectionName).ListAsync(HttpContext.RequestAborted))
            .Where(index => index.ItemId != model.Id)
            .OrderBy(index => index.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(index => new SelectListItem($"{index.Name} ({index.Number})", index.ItemId, index.ItemId == model.ParentAssetId))];
        model.ShowFleet = _fleet && model.Type is not null && _fleetCategories.Contains(model.Type.Category);
    }

    private static Address MakeAddress(AssetEditViewModel form, Address existing)
    {
        if (string.IsNullOrWhiteSpace(form.AddressLine1) && string.IsNullOrWhiteSpace(form.City) && string.IsNullOrWhiteSpace(form.PostalCode))
        {
            return null;
        }

        // A moved address loses its old coordinates.
        var same = existing is not null && existing.AddressLine1 == Blank(form.AddressLine1) && existing.City == Blank(form.City) && existing.PostalCode == Blank(form.PostalCode) && existing.Country == Blank(form.Country);

        return new Address
        {
            AddressLine1 = Blank(form.AddressLine1),
            City = Blank(form.City),
            Region = Blank(form.Region),
            PostalCode = Blank(form.PostalCode),
            Country = Blank(form.Country)?.ToUpperInvariant(),
            Latitude = same ? existing.Latitude : null,
            Longitude = same ? existing.Longitude : null,
            GeocodedUtc = same ? existing.GeocodedUtc : null,
            GeocodeSource = same ? existing.GeocodeSource : null,
        };
    }

    private static string Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
