using System.Globalization;
using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.ContentFields.Settings;
using CrestApps.OrchardCore.ContentFields.ViewModels;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContentFields.Drivers;

/// <summary>
/// Display driver for the <see cref="PhoneFieldSettings"/> configuration.
/// </summary>
public sealed class PhoneFieldSettingsDriver : ContentPartFieldDefinitionDisplayDriver<PhoneField>
{
    private readonly IPhoneNumberService _phoneNumberService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneFieldSettingsDriver"/> class.
    /// </summary>
    /// <param name="phoneNumberService">The phone number service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PhoneFieldSettingsDriver(
        IPhoneNumberService phoneNumberService,
        IStringLocalizer<PhoneFieldSettingsDriver> stringLocalizer)
    {
        _phoneNumberService = phoneNumberService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Builds the edit shape for phone field settings.
    /// </summary>
    /// <param name="model">The part field definition.</param>
    /// <param name="context">The build editor context.</param>
    /// <returns>The display result.</returns>
    public override IDisplayResult Edit(ContentPartFieldDefinition model, BuildEditorContext context)
    {
        return Initialize<PhoneFieldSettingsViewModel>("PhoneFieldSettings_Edit", viewModel =>
        {
            var settings = model.GetSettings<PhoneFieldSettings>();

            viewModel.Hint = settings.Hint;
            viewModel.Required = settings.Required;
            viewModel.InitialCountryMode = settings.InitialCountryMode;
            viewModel.SpecificCountryCode = settings.SpecificCountryCode;
            viewModel.InitialCountryModeOptions = GetInitialCountryModeOptions(settings.InitialCountryMode);
            viewModel.CountryOptions = GetCountryOptions(settings.SpecificCountryCode);
        }).Location("Content");
    }

    /// <summary>
    /// Updates the phone field settings from the form submission.
    /// </summary>
    /// <param name="model">The part field definition.</param>
    /// <param name="context">The update context.</param>
    /// <returns>The display result.</returns>
    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition model, UpdatePartFieldEditorContext context)
    {
        var viewModel = new PhoneFieldSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        var settings = new PhoneFieldSettings
        {
            Hint = viewModel.Hint,
            Required = viewModel.Required,
            InitialCountryMode = viewModel.InitialCountryMode,
            SpecificCountryCode = viewModel.InitialCountryMode == InitialCountryMode.Specific
                ? viewModel.SpecificCountryCode?.ToUpperInvariant()
                : null,
        };

        context.Builder.WithSettings(settings);

        return Edit(model, context);
    }

    private List<SelectListItem> GetInitialCountryModeOptions(InitialCountryMode selected)
    {
        return
        [
            new SelectListItem(S["Globe"], nameof(InitialCountryMode.Globe), selected == InitialCountryMode.Globe),
            new SelectListItem(S["Current culture"], nameof(InitialCountryMode.CurrentCulture), selected == InitialCountryMode.CurrentCulture),
            new SelectListItem(S["Specific"], nameof(InitialCountryMode.Specific), selected == InitialCountryMode.Specific),
        ];
    }

    private List<SelectListItem> GetCountryOptions(string selectedCode)
    {
        var regions = _phoneNumberService.GetSupportedRegions();
        var items = new List<SelectListItem>(regions.Count + 1)
        {
            new(S["Select a country"], string.Empty),
        };

        foreach (var regionCode in regions.OrderBy(r => r, StringComparer.OrdinalIgnoreCase))
        {
            string displayName;

            try
            {
                var regionInfo = new RegionInfo(regionCode);
                displayName = $"{regionInfo.EnglishName} ({regionCode})";
            }
            catch
            {
                displayName = regionCode;
            }

            items.Add(new SelectListItem(
                displayName,
                regionCode,
                string.Equals(regionCode, selectedCode, StringComparison.OrdinalIgnoreCase)));
        }

        return items;
    }
}
