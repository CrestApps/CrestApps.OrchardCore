using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Shipping.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Drivers;

/// <summary>
/// The EasyPost account, on Settings › Commerce › Shipping. A key or secret left blank keeps the one saved.
/// </summary>
public sealed class EasyPostSettingsDisplayDriver : SiteDisplayDriver<EasyPostSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly LinkGenerator _linkGenerator;

    public EasyPostSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        LinkGenerator linkGenerator)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _linkGenerator = linkGenerator;
    }

    protected override string SettingsGroupId
        => ShippingConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, EasyPostSettings settings, BuildEditorContext context)
        => Initialize<EasyPostSettingsViewModel>("EasyPostSettings_Edit", model =>
        {
            model.HasApiKey = !string.IsNullOrEmpty(settings.ApiKey);
            model.HasWebhookSecret = !string.IsNullOrEmpty(settings.WebhookSecret);
            model.WebhookUrl = _linkGenerator.GetUriByRouteValues(_httpContextAccessor.HttpContext, "EasyPostWebhook", values: null);
        }).Location("Content:10")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ShippingPermissions.ManageShipping))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, EasyPostSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ShippingPermissions.ManageShipping))
        {
            return null;
        }

        var model = new EasyPostSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var protector = _dataProtectionProvider.CreateProtector(EasyPostCarrierService.ProtectionPurpose);

        if (model.RemoveApiKey)
        {
            settings.ApiKey = null;
        }
        else if (!string.IsNullOrWhiteSpace(model.ApiKey))
        {
            settings.ApiKey = protector.Protect(model.ApiKey.Trim());
        }

        if (!string.IsNullOrWhiteSpace(model.WebhookSecret))
        {
            settings.WebhookSecret = protector.Protect(model.WebhookSecret.Trim());
        }

        return Edit(site, settings, context);
    }
}
