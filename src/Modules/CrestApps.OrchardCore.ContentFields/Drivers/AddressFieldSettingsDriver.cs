using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.ContentFields.Settings;
using CrestApps.OrchardCore.ContentFields.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContentFields.Drivers;

public sealed class AddressFieldSettingsDriver : ContentPartFieldDefinitionDisplayDriver<AddressField>
{
    private readonly ICountryService _countryService;

    internal readonly IStringLocalizer S;

    public AddressFieldSettingsDriver(ICountryService countryService, IStringLocalizer<AddressFieldSettingsDriver> stringLocalizer)
    {
        _countryService = countryService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ContentPartFieldDefinition model, BuildEditorContext context)
        => Combine(
            Initialize<AddressFieldSettingsViewModel>("AddressFieldSettingsBehavior_Edit", viewModel => FillValues(viewModel, model))
                .Location(l => l.Zone("Content", "1").Card(S["Behavior"], "15")),
            Initialize<AddressFieldSettingsViewModel>("AddressFieldSettings_Edit", async viewModel =>
            {
                FillValues(viewModel, model);
                viewModel.CountryOptions = (await _countryService.GetCountriesAsync())
                    .Select(country => new SelectListItem(country.Name, country.Code))
                    .ToArray();
            }).Location(l => l.Zone("Content", "2").Card(S["Settings"], "20")));

    private static void FillValues(AddressFieldSettingsViewModel viewModel, ContentPartFieldDefinition model)
    {
        var settings = model.GetSettings<AddressFieldSettings>();

        viewModel.Hint = settings.Hint;
        viewModel.Required = settings.Required;
        viewModel.ShowName = settings.ShowName;
        viewModel.ShowCompany = settings.ShowCompany;
        viewModel.ShowPhone = settings.ShowPhone;
        viewModel.Countries = settings.Countries ?? [];
        viewModel.DefaultCountry = settings.DefaultCountry;
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition model, UpdatePartFieldEditorContext context)
    {
        var viewModel = new AddressFieldSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        var countries = (viewModel.Countries ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct()
            .ToArray();

        context.Builder.WithSettings(new AddressFieldSettings
        {
            Hint = viewModel.Hint,
            Required = viewModel.Required,
            ShowName = viewModel.ShowName,
            ShowCompany = viewModel.ShowCompany,
            ShowPhone = viewModel.ShowPhone,
            Countries = countries.Length > 0 ? countries : null,
            DefaultCountry = string.IsNullOrWhiteSpace(viewModel.DefaultCountry) ? null : viewModel.DefaultCountry.Trim().ToUpperInvariant(),
        });

        return Edit(model, context);
    }
}
