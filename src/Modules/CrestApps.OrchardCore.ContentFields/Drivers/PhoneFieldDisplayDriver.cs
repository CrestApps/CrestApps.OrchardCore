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

            model.PhoneNumber = field.PhoneNumber;
            model.CountryCode = field.CountryCode;
            model.NationalNumber = field.NationalNumber;
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;

            if (string.IsNullOrEmpty(model.CountryCode) && !string.IsNullOrEmpty(settings.DefaultCountryCode))
            {
                model.CountryCode = settings.DefaultCountryCode;
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

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix, m => m.PhoneNumber, m => m.CountryCode, m => m.NationalNumber);

        var settings = context.PartFieldDefinition.GetSettings<PhoneFieldSettings>();

        if (settings.Required && string.IsNullOrWhiteSpace(viewModel.PhoneNumber))
        {
            context.Updater.ModelState.AddModelError(
                Prefix,
                nameof(viewModel.PhoneNumber),
                S["The {0} field is required.", context.PartFieldDefinition.DisplayName()]);
        }
        else if (!string.IsNullOrWhiteSpace(viewModel.PhoneNumber))
        {
            var regionCode = viewModel.CountryCode;

            if (!_phoneNumberService.IsValidNumber(viewModel.PhoneNumber, regionCode))
            {
                context.Updater.ModelState.AddModelError(
                    Prefix,
                    nameof(viewModel.PhoneNumber),
                    S["The {0} field does not contain a valid phone number.", context.PartFieldDefinition.DisplayName()]);
            }
            else if (_phoneNumberService.TryFormatToE164(viewModel.PhoneNumber, regionCode, out var e164Number))
            {
                viewModel.PhoneNumber = e164Number;
            }
        }

        field.PhoneNumber = viewModel.PhoneNumber;
        field.CountryCode = viewModel.CountryCode?.ToUpperInvariant();
        field.NationalNumber = viewModel.NationalNumber;

        return Edit(field, context);
    }
}
