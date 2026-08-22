using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Drivers;

internal sealed class TaxJurisdictionDisplayDriver : DisplayDriver<TaxJurisdiction>
{
    private readonly INamedCatalog<TaxJurisdiction> _jurisdictionStore;
    private readonly ICountryService _countryService;

    internal readonly IStringLocalizer S;

    public TaxJurisdictionDisplayDriver(
        INamedCatalog<TaxJurisdiction> jurisdictionStore,
        ICountryService countryService,
        IStringLocalizer<TaxJurisdictionDisplayDriver> stringLocalizer)
    {
        _jurisdictionStore = jurisdictionStore;
        _countryService = countryService;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(TaxJurisdiction jurisdiction, BuildDisplayContext context)
    {
        return CombineAsync(
            View("TaxJurisdiction_Fields_SummaryAdmin", jurisdiction)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("TaxJurisdiction_Buttons_SummaryAdmin", jurisdiction)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("TaxJurisdiction_DefaultMeta_SummaryAdmin", jurisdiction)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    public override IDisplayResult Edit(TaxJurisdiction jurisdiction, BuildEditorContext context)
    {
        return Initialize<TaxJurisdictionViewModel>("TaxJurisdictionFields_Edit", async model =>
        {
            model.IsNew = context.IsNew;
            model.Name = jurisdiction.Name;
            model.Code = jurisdiction.Code;
            model.Level = jurisdiction.Level;
            model.Country = jurisdiction.Country;
            model.Region = jurisdiction.Region;
            model.County = jurisdiction.County;
            model.City = jurisdiction.City;
            model.PostalCode = jurisdiction.PostalCode;
            model.ParentId = jurisdiction.ParentId;
            model.EffectiveFromUtc = jurisdiction.EffectiveFromUtc;
            model.EffectiveToUtc = jurisdiction.EffectiveToUtc;

            model.Levels = Enum.GetValues<JurisdictionLevel>()
                .Select(level => new SelectListItem(level.ToString(), level.ToString()))
                .ToList();

            var countries = await _countryService.GetCountriesAsync();

            model.Countries =
            [
                new SelectListItem(S["Select a country"], string.Empty),
                .. countries
                    .Select(country => new SelectListItem($"{country.Name} ({country.Code})", country.Code)),
            ];

            if (!string.IsNullOrEmpty(jurisdiction.Country) &&
                !model.Countries.Any(item => string.Equals(item.Value, jurisdiction.Country, StringComparison.OrdinalIgnoreCase)))
            {
                model.Countries.Insert(1, new SelectListItem(jurisdiction.Country, jurisdiction.Country));
            }

            var jurisdictions = await _jurisdictionStore.GetAllAsync();

            model.Parents = jurisdictions
                .Where(j => !string.Equals(j.ItemId, jurisdiction.ItemId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(j => j.Name)
                .Select(j => new SelectListItem(j.Name, j.ItemId))
                .ToList();
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(TaxJurisdiction jurisdiction, UpdateEditorContext context)
    {
        var model = new TaxJurisdictionViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            jurisdiction.Name = model.Name?.Trim();
        }

        jurisdiction.Code = model.Code?.Trim();
        jurisdiction.Level = model.Level;
        jurisdiction.Country = model.Country?.Trim();
        jurisdiction.Region = model.Region?.Trim();
        jurisdiction.County = model.County?.Trim();
        jurisdiction.City = model.City?.Trim();
        jurisdiction.PostalCode = model.PostalCode?.Trim();
        jurisdiction.ParentId = string.IsNullOrEmpty(model.ParentId) ? null : model.ParentId;
        jurisdiction.EffectiveFromUtc = model.EffectiveFromUtc;
        jurisdiction.EffectiveToUtc = model.EffectiveToUtc;

        return Edit(jurisdiction, context);
    }
}
