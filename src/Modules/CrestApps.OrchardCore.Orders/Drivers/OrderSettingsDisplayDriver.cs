using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// Settings › Commerce › Orders.
/// </summary>
public sealed class OrderSettingsDisplayDriver : SiteDisplayDriver<OrderSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public OrderSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => OrderConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, OrderSettings settings, BuildEditorContext context)
        => Initialize<OrderSettingsViewModel>("OrderSettings_Edit", model =>
        {
            model.SendConfirmations = settings.SendConfirmations;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, OrderPermissions.ManageOrders))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, OrderSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, OrderPermissions.ManageOrders))
        {
            return null;
        }

        var model = new OrderSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.SendConfirmations = model.SendConfirmations;

        return Edit(site, settings, context);
    }
}
