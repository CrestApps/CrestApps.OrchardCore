using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Endpoints;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// Settings › Commerce › Orders.
/// </summary>
public sealed class OrderSettingsDisplayDriver : SiteDisplayDriver<OrderSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly CouponItemLookup _lookup;

    internal readonly IStringLocalizer S;

    public OrderSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        CouponItemLookup lookup,
        IStringLocalizer<OrderSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _lookup = lookup;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => OrderConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, OrderSettings settings, BuildEditorContext context)
        => Initialize<OrderSettingsViewModel>("OrderSettings_Edit", async model =>
        {
            var pathBase = _httpContextAccessor.HttpContext?.Request.PathBase.Value ?? string.Empty;

            model.OrderBumpProductId = settings.OrderBumpProductId;
            model.OrderBumpPercentOff = settings.OrderBumpPercentOff;
            model.OrderBumpHeadline = settings.OrderBumpHeadline;
            model.ProductSearchEndpoint = $"{pathBase}/{CouponItemSearchEndpoints.ProductsPath}";
            model.InitialOrderBumpJson = System.Text.Json.JsonSerializer.Serialize(string.IsNullOrEmpty(settings.OrderBumpProductId)
                ? []
                : (await _lookup.GetProductsAsync([settings.OrderBumpProductId])).Select(product => new { value = product.Id, text = product.Text, selected = true }));
            model.SendConfirmations = settings.SendConfirmations;
            model.SendRecoveryEmails = settings.SendRecoveryEmails;
            model.RecoveryDelayHours = settings.RecoveryDelayHours;
            model.HasBaseUrl = !string.IsNullOrWhiteSpace(site.BaseUrl);
            model.AllowQuoteRequests = settings.AllowQuoteRequests;
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
        settings.SendRecoveryEmails = model.SendRecoveryEmails;
        settings.RecoveryDelayHours = Math.Clamp(model.RecoveryDelayHours, 1, 72);
        settings.AllowQuoteRequests = model.AllowQuoteRequests;

        if (model.OrderBumpPercentOff is < 0 or >= 100)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.OrderBumpPercentOff), S["The discount has to be from 0 to 99%."]);
        }

        settings.OrderBumpProductId = string.IsNullOrWhiteSpace(model.OrderBumpProductId) ? null : model.OrderBumpProductId.Trim();
        settings.OrderBumpPercentOff = model.OrderBumpPercentOff is > 0 and < 100 ? model.OrderBumpPercentOff : null;
        settings.OrderBumpHeadline = string.IsNullOrWhiteSpace(model.OrderBumpHeadline) ? null : model.OrderBumpHeadline.Trim();

        return Edit(site, settings, context);
    }
}
