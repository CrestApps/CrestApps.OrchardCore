using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.ViewModels;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.DncRegistry.Drivers;

/// <summary>
/// Display driver that provides the file upload editor shape for importing a local DNC list.
/// </summary>
public sealed class ImportLocalDncListDisplayDriver : DisplayDriver<ImportLocalDncList>
{
    private readonly IPhoneNumberService _phoneNumberService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImportLocalDncListDisplayDriver"/> class.
    /// </summary>
    /// <param name="phoneNumberService">The phone number service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ImportLocalDncListDisplayDriver(
        IPhoneNumberService phoneNumberService,
        IStringLocalizer<ImportLocalDncListDisplayDriver> stringLocalizer)
    {
        _phoneNumberService = phoneNumberService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Builds the editor shape for the import form.
    /// </summary>
    /// <param name="model">The import model.</param>
    /// <param name="context">The build editor context.</param>
    public override Task<IDisplayResult> EditAsync(ImportLocalDncList model, BuildEditorContext context)
        => Task.FromResult<IDisplayResult>(
            Initialize<UploadLocalDncListViewModel>("ImportLocalDncListFile_Edit", viewModel =>
            {
                viewModel.Name = model.Name;
                viewModel.CountryCode = model.CountryCode;
                viewModel.File = model.File;
                viewModel.CountryOptions = GetCountryOptions();
            })
            .Location("Content:1"));

    /// <summary>
    /// Updates the import model from form data.
    /// </summary>
    /// <param name="model">The import model.</param>
    /// <param name="context">The update editor context.</param>
    public override async Task<IDisplayResult> UpdateAsync(ImportLocalDncList model, UpdateEditorContext context)
    {
        var viewModel = new UploadLocalDncListViewModel();

        if (await context.Updater.TryUpdateModelAsync(viewModel, Prefix))
        {
            if (string.IsNullOrWhiteSpace(viewModel.Name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Name), S["A list name is required."]);
            }

            if (string.IsNullOrWhiteSpace(viewModel.CountryCode))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.CountryCode), S["A country is required."]);
            }

            if (viewModel.File == null || viewModel.File.Length == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.File), S["A CSV file is required."]);
            }

            model.Name = viewModel.Name;
            model.CountryCode = viewModel.CountryCode;
            model.File = viewModel.File;
        }

        return await EditAsync(model, context);
    }

    private SelectListItem[] GetCountryOptions()
    {
        return
        [
            CreateCountryOption("United States", "US"),
            CreateCountryOption("Canada", "CA"),
            CreateCountryOption("United Kingdom", "GB"),
            CreateCountryOption("Australia", "AU"),
            CreateCountryOption("Germany", "DE"),
            CreateCountryOption("France", "FR"),
            CreateCountryOption("India", "IN"),
            CreateCountryOption("Brazil", "BR"),
            CreateCountryOption("Mexico", "MX"),
            CreateCountryOption("Japan", "JP"),
            CreateCountryOption("South Korea", "KR"),
            CreateCountryOption("Italy", "IT"),
            CreateCountryOption("Spain", "ES"),
            CreateCountryOption("Netherlands", "NL"),
            CreateCountryOption("Belgium", "BE"),
            CreateCountryOption("Switzerland", "CH"),
            CreateCountryOption("Sweden", "SE"),
            CreateCountryOption("Norway", "NO"),
            CreateCountryOption("Denmark", "DK"),
            CreateCountryOption("Finland", "FI"),
            CreateCountryOption("Ireland", "IE"),
            CreateCountryOption("New Zealand", "NZ"),
            CreateCountryOption("South Africa", "ZA"),
            CreateCountryOption("Argentina", "AR"),
            CreateCountryOption("Colombia", "CO"),
            CreateCountryOption("Chile", "CL"),
            CreateCountryOption("Poland", "PL"),
            CreateCountryOption("Austria", "AT"),
            CreateCountryOption("Portugal", "PT"),
        ];
    }

    private SelectListItem CreateCountryOption(string displayName, string countryCode)
    {
        var callingCode = _phoneNumberService.GetCountryCode(countryCode);

        if (callingCode > 0)
        {
            return new SelectListItem(S["{0} (+{1})", displayName, callingCode], countryCode);
        }

        return new SelectListItem(S[displayName], countryCode);
    }
}
