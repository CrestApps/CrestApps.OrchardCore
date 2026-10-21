using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// Where the store sells from, under Settings > Commerce > Tax origin.
/// </summary>
public sealed class TaxOriginSettingsDisplayDriver : SiteDisplayDriver<TaxOriginSettings>
{
    public const string GroupId = "tax-origin";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public TaxOriginSettingsDisplayDriver(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    protected override string SettingsGroupId
        => GroupId;

    public override IDisplayResult Edit(ISite site, TaxOriginSettings settings, BuildEditorContext context)
        => Initialize<TaxOriginSettingsViewModel>("TaxOriginSettings_Edit", model =>
        {
            model.Country = settings.Origin?.Country;
            model.Region = settings.Origin?.Region;
            model.County = settings.Origin?.County;
            model.City = settings.Origin?.City;
            model.PostalCode = settings.Origin?.PostalCode;
            model.AddressLine1 = settings.Origin?.AddressLine1;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TaxationPermissions.ManageTaxation))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, TaxOriginSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TaxationPermissions.ManageTaxation))
        {
            return null;
        }

        var model = new TaxOriginSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.Origin = string.IsNullOrWhiteSpace(model.Country)
            ? null
            : new Address
            {
                Country = model.Country.Trim().ToUpperInvariant(),
                Region = Trim(model.Region)?.ToUpperInvariant(),
                County = Trim(model.County),
                City = Trim(model.City),
                PostalCode = Trim(model.PostalCode),
                AddressLine1 = Trim(model.AddressLine1),
            };

        return Edit(site, settings, context);
    }

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
