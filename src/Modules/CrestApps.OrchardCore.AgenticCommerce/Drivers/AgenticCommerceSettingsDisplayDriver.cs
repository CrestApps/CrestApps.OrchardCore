using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Drivers;

/// <summary>
/// Settings › Commerce › Agentic commerce: how long agent checkouts last, how many a platform may keep open, and how
/// agents pay.
/// </summary>
public sealed class AgenticCommerceSettingsDisplayDriver : SiteDisplayDriver<AgenticCommerceSettings>
{
    /// <summary>
    /// The settings group of the page, at <c>Admin/Settings/agentic-commerce</c>.
    /// </summary>
    public const string GroupId = "agentic-commerce";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public AgenticCommerceSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<AgenticCommerceSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override IDisplayResult Edit(ISite site, AgenticCommerceSettings settings, BuildEditorContext context)
        => Initialize<AgenticCommerceSettingsViewModel>("AgenticCommerceSettings_Edit", model =>
        {
            model.CheckoutLifetimeHours = settings.CheckoutLifetimeHours;
            model.MaxOpenCheckoutsPerPlatform = settings.MaxOpenCheckoutsPerPlatform;
            model.AllowUnregisteredUcpPlatforms = settings.AllowUnregisteredUcpPlatforms;
            model.StripeBusinessProfileId = settings.StripeBusinessProfileId;
            model.EnableGooglePay = settings.EnableGooglePay;
            model.GooglePayMerchantId = settings.GooglePayMerchantId;
            model.GooglePayMerchantName = settings.GooglePayMerchantName;
            model.GooglePayTestEnvironment = settings.GooglePayTestEnvironment;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, AgenticCommercePermissions.ManageAgenticCommerceSettings))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, AgenticCommerceSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, AgenticCommercePermissions.ManageAgenticCommerceSettings))
        {
            return null;
        }

        var model = new AgenticCommerceSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.CheckoutLifetimeHours < 1 || model.CheckoutLifetimeHours > 72)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CheckoutLifetimeHours), S["Enter a number of hours from 1 to 72."]);
        }

        if (model.MaxOpenCheckoutsPerPlatform < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxOpenCheckoutsPerPlatform), S["Enter a number of 1 or more."]);
        }

        if (model.EnableGooglePay && string.IsNullOrWhiteSpace(model.GooglePayMerchantId) && !model.GooglePayTestEnvironment)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.GooglePayMerchantId), S["Enter the store's Google Pay merchant ID, or use Google Pay's test environment."]);
        }

        settings.CheckoutLifetimeHours = model.CheckoutLifetimeHours;
        settings.MaxOpenCheckoutsPerPlatform = model.MaxOpenCheckoutsPerPlatform;
        settings.AllowUnregisteredUcpPlatforms = model.AllowUnregisteredUcpPlatforms;
        settings.StripeBusinessProfileId = Clean(model.StripeBusinessProfileId);
        settings.EnableGooglePay = model.EnableGooglePay;
        settings.GooglePayMerchantId = Clean(model.GooglePayMerchantId);
        settings.GooglePayMerchantName = Clean(model.GooglePayMerchantName);
        settings.GooglePayTestEnvironment = model.GooglePayTestEnvironment;

        return Edit(site, settings, context);
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
