using CrestApps.Core;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Inventory.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Security.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Inventory.Drivers;

/// <summary>
/// Settings › Commerce › Inventory.
/// </summary>
public sealed class InventorySettingsDisplayDriver : SiteDisplayDriver<InventorySettings>
{
    private const int MaxThreshold = 100_000;
    private const int MaxHoldMinutes = 7 * 24 * 60;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IRoleService _roleService;
    private readonly InventoryLocationService _locationService;
    private readonly StockLocationStore _locationStore;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public InventorySettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IRoleService roleService,
        InventoryLocationService locationService,
        StockLocationStore locationStore,
        IClock clock,
        IStringLocalizer<InventorySettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _roleService = roleService;
        _locationService = locationService;
        _locationStore = locationStore;
        _clock = clock;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => InventoryConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, InventorySettings settings, BuildEditorContext context)
    {
        return Combine(
            Initialize<InventorySettingsViewModel>("InventorySettings_Edit", model => FillAsync(model, settings))
                .Location(l => l.Zone("Content", "1").Card(S["Stock"], "10"))
                .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, InventoryPermissions.ManageInventory))
                .OnGroup(SettingsGroupId),
            Initialize<InventorySettingsViewModel>("InventorySettingsLocations_Edit", model => FillAsync(model, settings))
                .Location(l => l.Zone("Content", "2").Card(S["Locations"], "20"))
                .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, InventoryPermissions.ManageInventory))
                .OnGroup(SettingsGroupId));
    }

    private async ValueTask FillAsync(InventorySettingsViewModel model, InventorySettings settings)
    {
        model.DefaultLowStockThreshold = settings.DefaultLowStockThreshold;
        model.HoldMinutes = settings.HoldMinutes;
        model.LowStockNotificationRoles = settings.LowStockNotificationRoles ?? [];
        model.Roles = (await _roleService.GetRoleNamesAsync())
            .Where(role => !IsEveryone(role))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        // Locations owned by other records, such as vans, are managed with their owner.
        model.Locations = StockLocationEditor.GetSiteLocations(await _locationService.GetLocationsAsync())
            .Select(location => new InventoryLocationViewModel
            {
                LocationId = location.LocationId,
                Name = location.Name,
                Address = location.Address,
                IsDefault = location.LocationId == InventoryConstants.DefaultLocationId,
            })
            .ToList();

        // A blank row to add a location with, while there is room for one.
        if (model.Locations.Count < StockLocationEditor.MaxLocations)
        {
            model.Locations.Add(new InventoryLocationViewModel());
        }
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, InventorySettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, InventoryPermissions.ManageInventory))
        {
            return null;
        }

        var model = new InventorySettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.HoldMinutes is < 1 or > MaxHoldMinutes)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.HoldMinutes), S["Enter a number of minutes from 1 to {0}.", MaxHoldMinutes]);
        }
        else
        {
            settings.HoldMinutes = model.HoldMinutes;
        }

        if (model.DefaultLowStockThreshold is < 0 or > MaxThreshold)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DefaultLowStockThreshold), S["Enter a number from 0 to {0}.", MaxThreshold]);
        }
        else if (model.DefaultLowStockThreshold != settings.DefaultLowStockThreshold)
        {
            settings.DefaultLowStockThreshold = model.DefaultLowStockThreshold;

            // Products without their own threshold take the new one once the settings are saved.
            DeferredWork.Add("inventory_sync_all", scope => scope.ServiceProvider.GetRequiredService<InventorySyncService>().SyncAllAsync());
        }

        // Only roles that exist are kept. Anonymous and Authenticated would mean every visitor or every user.
        var roles = (await _roleService.GetRoleNamesAsync()).Where(role => !IsEveryone(role)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        settings.LowStockNotificationRoles = (model.LowStockNotificationRoles ?? [])
            .Where(roles.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var (locations, rowsWithoutName) = StockLocationEditor.Parse(model.Locations);

        foreach (var row in rowsWithoutName)
        {
            context.Updater.ModelState.AddModelError(Prefix, $"{nameof(InventorySettingsViewModel.Locations)}[{row}].{nameof(InventoryLocationViewModel.Name)}", S["Give the location a name, such as Main warehouse."]);
        }

        if (context.Updater.ModelState.IsValid)
        {
            await StockLocationEditor.SaveAsync(_locationStore, _clock, locations);

            // The locations live in the store now; the settings keep none.
            settings.Locations = [];
        }

        return Edit(site, settings, context);
    }

    private static bool IsEveryone(string role)
        => string.Equals(role, OrchardCoreConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, OrchardCoreConstants.Roles.Authenticated, StringComparison.OrdinalIgnoreCase);
}
