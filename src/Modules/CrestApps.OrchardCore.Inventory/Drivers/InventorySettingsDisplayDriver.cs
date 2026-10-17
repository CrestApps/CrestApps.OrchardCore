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
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Inventory.Drivers;

/// <summary>
/// Settings › Commerce › Inventory.
/// </summary>
public sealed class InventorySettingsDisplayDriver : SiteDisplayDriver<InventorySettings>
{
    private const int MaxThreshold = 100_000;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public InventorySettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<InventorySettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => InventoryConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, InventorySettings settings, BuildEditorContext context)
        => Initialize<InventorySettingsViewModel>("InventorySettings_Edit", model =>
        {
            model.DefaultLowStockThreshold = settings.DefaultLowStockThreshold;
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

        return Edit(site, settings, context);
    }
}
