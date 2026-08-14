using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Display driver that adds omnichannel-specific import options to the import form
/// when the content type being imported has the <c>OmnichannelContactPart</c>.
/// </summary>
public sealed class OmnichannelContactImportOptionsDisplayDriver : DisplayDriver<ImportContent>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactImportOptionsDisplayDriver"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="phoneNumberService">The phone number service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelContactImportOptionsDisplayDriver(
        IContentDefinitionManager contentDefinitionManager,
        IPhoneNumberService phoneNumberService,
        IStringLocalizer<OmnichannelContactImportOptionsDisplayDriver> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _phoneNumberService = phoneNumberService;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(ImportContent model, BuildEditorContext context)
    {
        if (!await IsOmnichannelContactAsync(model))
        {
            return null;
        }

        var options = model.GetOrCreate<OmnichannelContactImportOptionsPart>();

        return Initialize<OmnichannelContactImportOptionsViewModel>("OmnichannelContactImportOptions_Edit", viewModel =>
        {
            viewModel.IgnoreDuplicateByPhoneNumber = options.IgnoreDuplicateByPhoneNumber;
            viewModel.SelectedCountryCode = NormalizeCountryCode(options.SelectedCountryCode);
            viewModel.AvailableCountries = GetCountryOptions(viewModel.SelectedCountryCode);
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ImportContent model, UpdateEditorContext context)
    {
        if (!await IsOmnichannelContactAsync(model))
        {
            return null;
        }

        var viewModel = new OmnichannelContactImportOptionsViewModel();

        if (await context.Updater.TryUpdateModelAsync(viewModel, Prefix))
        {
            if (string.IsNullOrWhiteSpace(viewModel.SelectedCountryCode))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.SelectedCountryCode), S["Lead country is required."]);
            }

            var options = model.GetOrCreate<OmnichannelContactImportOptionsPart>();
            options.IgnoreDuplicateByPhoneNumber = viewModel.IgnoreDuplicateByPhoneNumber;
            options.SelectedCountryCode = NormalizeCountryCode(viewModel.SelectedCountryCode);
            model.Put(options);
        }

        return await EditAsync(model, context);
    }

    private async Task<bool> IsOmnichannelContactAsync(ImportContent model)
    {
        if (string.IsNullOrEmpty(model.ContentTypeId))
        {
            return false;
        }

        var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(model.ContentTypeId);

        return contentTypeDefinition?.Parts?.Any(p =>
            p.PartDefinition.Name == OmnichannelConstants.ContentParts.OmnichannelContact) == true;
    }

    private List<SelectListItem> GetCountryOptions(string selectedCountryCode)
    {
        var countryOptions = new List<SelectListItem>
        {
            new()
            {
                Text = S["Select default country"],
                Value = string.Empty,
                Selected = string.IsNullOrEmpty(selectedCountryCode),
            },
        };

        foreach (var country in GetSupportedCountryOptions())
        {
            country.Selected = string.Equals(country.Value, selectedCountryCode, StringComparison.OrdinalIgnoreCase);
            countryOptions.Add(country);
        }

        return countryOptions;
    }

    private static string NormalizeCountryCode(string countryCode)
        => string.IsNullOrWhiteSpace(countryCode)
            ? null
            : countryCode.Trim().ToUpperInvariant();

    private SelectListItem[] GetSupportedCountryOptions()
        =>
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

    private SelectListItem CreateCountryOption(string displayName, string countryCode)
    {
        var callingCode = _phoneNumberService.GetCountryCode(countryCode);

        if (callingCode > 0)
        {
            return new SelectListItem
            {
                Text = S["{0} (+{1})", displayName, callingCode],
                Value = countryCode.ToUpperInvariant(),
            };
        }

        return new SelectListItem(S[displayName], countryCode.ToUpperInvariant());
    }
}
