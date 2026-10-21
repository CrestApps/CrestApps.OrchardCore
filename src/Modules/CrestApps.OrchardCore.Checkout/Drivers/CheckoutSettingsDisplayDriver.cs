using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Checkout.Drivers;

/// <summary>
/// Settings › Commerce › Checkout: how the checkout is laid out for the buyer.
/// </summary>
public sealed class CheckoutSettingsDisplayDriver : SiteDisplayDriver<CheckoutSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public CheckoutSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => CheckoutConstants.SettingsGroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, CheckoutSettings settings, BuildEditorContext context)
        => Initialize<CheckoutSettingsViewModel>("CheckoutSettings_Edit", model =>
        {
            model.Layout = settings.Layout;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, CheckoutPermissions.ManageCheckoutSettings))
        .OnGroup(SettingsGroupId);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, CheckoutSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, CheckoutPermissions.ManageCheckoutSettings))
        {
            return null;
        }

        var model = new CheckoutSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.Layout = Enum.IsDefined(model.Layout) ? model.Layout : CheckoutLayout.Steps;

        return Edit(site, settings, context);
    }
}
