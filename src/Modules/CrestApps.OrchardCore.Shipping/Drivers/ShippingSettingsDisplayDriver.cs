using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Drivers;

/// <summary>
/// Settings › Commerce › Shipping.
/// </summary>
public sealed class ShippingSettingsDisplayDriver : SiteDisplayDriver<ShippingSettings>
{
    private const int MaxClasses = 50;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public ShippingSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => ShippingConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, ShippingSettings settings, BuildEditorContext context)
        => Initialize<ShippingSettingsViewModel>("ShippingSettings_Edit", model =>
        {
            model.WeightUnit = settings.WeightUnit;
            model.ShippingClasses = string.Join(Environment.NewLine, settings.ShippingClasses ?? []);
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ShippingPermissions.ManageShipping))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ShippingSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ShippingPermissions.ManageShipping))
        {
            return null;
        }

        var model = new ShippingSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.WeightUnit = Enum.IsDefined(model.WeightUnit) ? model.WeightUnit : WeightUnit.Kilogram;
        settings.ShippingClasses = ParseClasses(model.ShippingClasses);

        return Edit(site, settings, context);
    }

    internal static List<string> ParseClasses(string text)
        => (text ?? string.Empty)
            .Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxClasses)
            .ToList();
}
