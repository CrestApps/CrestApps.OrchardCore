using System.Globalization;
using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.ContentFields.Settings;
using CrestApps.OrchardCore.ContentFields.ViewModels;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ContentFields.Drivers;

/// <summary>
/// Display driver for the <see cref="PhoneField"/> content field.
/// </summary>
public sealed class PhoneFieldDisplayDriver : ContentFieldDisplayDriver<PhoneField>
{
    private readonly IPhoneNumberService _phoneNumberService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneFieldDisplayDriver"/> class.
    /// </summary>
    /// <param name="phoneNumberService">The phone number service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PhoneFieldDisplayDriver(
        IPhoneNumberService phoneNumberService,
        IStringLocalizer<PhoneFieldDisplayDriver> stringLocalizer)
    {
        _phoneNumberService = phoneNumberService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Builds the display shape for the phone field.
    /// </summary>
    /// <param name="field">The phone field instance.</param>
    /// <param name="context">The display context.</param>
    /// <returns>The display result.</returns>
    public override IDisplayResult Display(PhoneField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayPhoneFieldViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        }).Location("Detail", "Content")
        .Location("Summary", "Content");
    }

    /// <summary>
    /// Builds the edit shape for the phone field.
    /// </summary>
    /// <param name="field">The phone field instance.</param>
    /// <param name="context">The editor context.</param>
    /// <returns>The display result.</returns>
    public override IDisplayResult Edit(PhoneField field, BuildFieldEditorContext context)
    {
        return Initialize<EditPhoneFieldViewModel>(GetEditorShapeType(context), model =>
        {
            var settings = context.PartFieldDefinition.GetSettings<PhoneFieldSettings>();
            var countryCode = field.CountryCode;

            if (string.IsNullOrWhiteSpace(countryCode) && !string.IsNullOrWhiteSpace(field.PhoneNumber))
            {
                countryCode = _phoneNumberService.GetRegionCode(field.PhoneNumber);
            }

            model.PhoneNumber = field.PhoneNumber;
            model.CountryCode = countryCode?.ToUpperInvariant();
            model.NationalNumber = ResolveNationalNumber(field.PhoneNumber, model.CountryCode, field.NationalNumber);
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;

            if (string.IsNullOrEmpty(model.CountryCode))
            {
                model.CountryCode = ResolveInitialCountryCode(settings);
            }
        });
    }

    /// <summary>
    /// Updates the phone field from the editor form submission.
    /// </summary>
    /// <param name="field">The phone field instance to update.</param>
    /// <param name="context">The update context.</param>
    /// <returns>The display result.</returns>
    public override async Task<IDisplayResult> UpdateAsync(PhoneField field, UpdateFieldEditorContext context)
    {
        var viewModel = new EditPhoneFieldViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix,
            m => m.PhoneNumber,
            m => m.CountryCode,
            m => m.NationalNumber);

        var phoneNumber = viewModel.PhoneNumber?.Trim();
        var countryCode = viewModel.CountryCode?.Trim()?.ToUpperInvariant();
        var nationalNumber = viewModel.NationalNumber?.Trim();

        field.PhoneNumber = phoneNumber;
        field.CountryCode = countryCode;
        field.NationalNumber = nationalNumber;

        var settings = context.PartFieldDefinition.GetSettings<PhoneFieldSettings>();

        var hasPhoneNumber = !string.IsNullOrWhiteSpace(phoneNumber);

        if (settings.Required && !hasPhoneNumber)
        {
            context.Updater.ModelState.AddModelError(
                Prefix,
                nameof(viewModel.PhoneNumber),
                S["The {0} field is required.", context.PartFieldDefinition.DisplayName()]);
        }
        else if (hasPhoneNumber && !string.IsNullOrWhiteSpace(countryCode))
        {
            if (!_phoneNumberService.IsValidNumber(phoneNumber, countryCode))
            {
                context.Updater.ModelState.AddModelError(
                    Prefix,
                    nameof(viewModel.PhoneNumber),
                    S["The {0} field does not contain a valid phone number.", context.PartFieldDefinition.DisplayName()]);
            }
            else if (_phoneNumberService.TryFormatToE164(phoneNumber, countryCode, out var e164Number))
            {
                viewModel.PhoneNumber = e164Number;
                viewModel.CountryCode = _phoneNumberService.GetRegionCode(e164Number) ?? countryCode;
                viewModel.NationalNumber = ResolveNationalNumber(e164Number, viewModel.CountryCode, null);
            }
        }

        return Edit(field, context);
    }

    private static string ResolveInitialCountryCode(PhoneFieldSettings settings)
    {
        return settings.InitialCountryMode switch
        {
            InitialCountryMode.CurrentCulture => GetCountryCodeFromCulture(),
            InitialCountryMode.Specific => settings.SpecificCountryCode,
            _ => null,
        };
    }

    private string ResolveNationalNumber(string phoneNumber, string regionCode, string fallbackNationalNumber)
    {
        if (!string.IsNullOrWhiteSpace(fallbackNationalNumber))
        {
            return fallbackNationalNumber;
        }

        if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(regionCode))
        {
            return phoneNumber;
        }

        var countryCode = _phoneNumberService.GetCountryCode(regionCode);

        if (countryCode <= 0)
        {
            return phoneNumber;
        }

        var prefix = $"+{countryCode}";

        return phoneNumber.StartsWith(prefix, StringComparison.Ordinal)
            ? phoneNumber[prefix.Length..]
            : phoneNumber;
    }

    private static string GetCountryCodeFromCulture()
    {
        var culture = CultureInfo.CurrentCulture;

        if (culture.IsNeutralCulture || culture == CultureInfo.InvariantCulture)
        {
            return null;
        }

        try
        {
            var regionInfo = new RegionInfo(culture.Name);

            return regionInfo.TwoLetterISORegionName;
        }
        catch
        {
            return null;
        }
    }
}
