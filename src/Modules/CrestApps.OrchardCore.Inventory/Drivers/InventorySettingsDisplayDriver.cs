using CrestApps.Core;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Inventory.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore;
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
    private const int MaxLocations = 50;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IRoleService _roleService;

    internal readonly IStringLocalizer S;

    public InventorySettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IRoleService roleService,
        IStringLocalizer<InventorySettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _roleService = roleService;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => InventoryConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, InventorySettings settings, BuildEditorContext context)
        => Initialize<InventorySettingsViewModel>("InventorySettings_Edit", async model =>
        {
            model.DefaultLowStockThreshold = settings.DefaultLowStockThreshold;
            model.HoldMinutes = settings.HoldMinutes;
            model.LowStockNotificationRoles = settings.LowStockNotificationRoles ?? [];
            model.Roles = (await _roleService.GetRoleNamesAsync())
                .Where(role => !IsEveryone(role))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            model.Locations = settings.GetLocations()
                .Select(location => new InventoryLocationViewModel
                {
                    LocationId = location.LocationId,
                    Name = location.Name,
                    Address = location.Address,
                    IsDefault = location.LocationId == InventoryConstants.DefaultLocationId,
                })
                .ToList();

            // A blank row to add a location with, while there is room for one.
            if (model.Locations.Count < MaxLocations)
            {
                model.Locations.Add(new InventoryLocationViewModel());
            }
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, InventoryPermissions.ManageInventory))
        .OnGroup(SettingsGroupId);

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
            ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<InventorySyncService>().SyncAllAsync());
        }

        // Only roles that exist are kept. Anonymous and Authenticated would mean every visitor or every user.
        var roles = (await _roleService.GetRoleNamesAsync()).Where(role => !IsEveryone(role)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        settings.LowStockNotificationRoles = (model.LowStockNotificationRoles ?? [])
            .Where(roles.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        settings.Locations = ParseLocations(model.Locations, context);

        return Edit(site, settings, context);
    }

    // A blank row, such as the one to add a location with, is dropped, and so is one marked for removal, except the
    // default location: stock counted before there were locations is there. A removed location's units stay on its
    // items, where they can still be sold and moved to another location.
    private List<InventoryLocation> ParseLocations(List<InventoryLocationViewModel> rows, UpdateEditorContext context)
    {
        var locations = new List<InventoryLocation>();

        for (var i = 0; i < (rows?.Count ?? 0); i++)
        {
            var row = rows[i];
            var isDefault = row?.LocationId == InventoryConstants.DefaultLocationId;

            if (row is null || (row.Remove && !isDefault) || (!isDefault && string.IsNullOrWhiteSpace(row.Name) && string.IsNullOrWhiteSpace(row.Address)))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{nameof(InventorySettingsViewModel.Locations)}[{i}].{nameof(row.Name)}", S["Give the location a name, such as Main warehouse."]);
            }

            locations.Add(new InventoryLocation
            {
                LocationId = string.IsNullOrWhiteSpace(row.LocationId) ? UniqueId.GenerateId() : row.LocationId.Trim(),
                Name = row.Name?.Trim(),
                Address = string.IsNullOrWhiteSpace(row.Address) ? null : row.Address.Trim(),
            });
        }

        return locations.DistinctBy(location => location.LocationId, StringComparer.Ordinal).Take(MaxLocations).ToList();
    }

    private static bool IsEveryone(string role)
        => string.Equals(role, OrchardCoreConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, OrchardCoreConstants.Roles.Authenticated, StringComparison.OrdinalIgnoreCase);
}
