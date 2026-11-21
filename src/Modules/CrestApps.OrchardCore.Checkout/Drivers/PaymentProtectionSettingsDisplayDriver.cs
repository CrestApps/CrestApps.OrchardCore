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
/// Settings › Commerce › Checkout › Payment protection: what the store does during a store-wide run of declined card
/// payments.
/// </summary>
public sealed class PaymentProtectionSettingsDisplayDriver : SiteDisplayDriver<PaymentProtectionSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentProtectionSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public PaymentProtectionSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => CheckoutConstants.SettingsGroupId;

    /// <inheritdoc/>
    public override IDisplayResult Edit(ISite site, PaymentProtectionSettings settings, BuildEditorContext context)
        => Initialize<PaymentProtectionSettingsViewModel>("PaymentProtectionSettings_Edit", model =>
        {
            model.OnDeclineSpike = settings.OnDeclineSpike;
        }).Location("Content:5")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, CheckoutPermissions.ManageCheckoutSettings))
        .OnGroup(SettingsGroupId);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, PaymentProtectionSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, CheckoutPermissions.ManageCheckoutSettings))
        {
            return null;
        }

        var model = new PaymentProtectionSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.OnDeclineSpike = Enum.IsDefined(model.OnDeclineSpike) ? model.OnDeclineSpike : DeclineSpikeAction.Notify;

        return Edit(site, settings, context);
    }
}
