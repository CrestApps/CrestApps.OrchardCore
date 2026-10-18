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
            model.Carriers = string.Join(Environment.NewLine, (settings.Carriers ?? []).Select(carrier => string.IsNullOrEmpty(carrier.TrackingUrlTemplate) ? carrier.Name : $"{carrier.Name} | {carrier.TrackingUrlTemplate}"));
            model.SendShippedEmails = settings.SendShippedEmails;
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
        settings.Carriers = ParseCarriers(model.Carriers);
        settings.SendShippedEmails = model.SendShippedEmails;

        return Edit(site, settings, context);
    }

    // One carrier per line: its name, then optionally "|" and its tracking link with {0} for the number.
    internal static List<ShippingCarrier> ParseCarriers(string text)
        => (text ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('|', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts[0].Length > 0)
            .Select(parts => new ShippingCarrier
            {
                Name = parts[0],
                TrackingUrlTemplate = parts.Length > 1 && IsWebLink(parts[1]) ? parts[1] : null,
            })
            .DistinctBy(carrier => carrier.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxClasses)
            .ToList();

    private static bool IsWebLink(string value)
        => Uri.TryCreate(value.Replace("{0}", "0", StringComparison.Ordinal), UriKind.Absolute, out var url) && url.Scheme is "http" or "https";

    internal static List<string> ParseClasses(string text)
        => (text ?? string.Empty)
            .Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxClasses)
            .ToList();
}
