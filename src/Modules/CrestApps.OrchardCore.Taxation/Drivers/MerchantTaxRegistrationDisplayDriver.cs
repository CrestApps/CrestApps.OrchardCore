using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Drivers;

/// <summary>
/// The rows of the tax registrations and the fields of their editor.
/// </summary>
internal sealed class MerchantTaxRegistrationDisplayDriver : DisplayDriver<MerchantTaxRegistration>
{
    private readonly INamedCatalog<TaxJurisdiction> _jurisdictions;
    private readonly INamedCatalog<TaxType> _types;

    internal readonly IStringLocalizer S;

    public MerchantTaxRegistrationDisplayDriver(
        INamedCatalog<TaxJurisdiction> jurisdictions,
        INamedCatalog<TaxType> types,
        IStringLocalizer<MerchantTaxRegistrationDisplayDriver> stringLocalizer)
    {
        _jurisdictions = jurisdictions;
        _types = types;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> DisplayAsync(MerchantTaxRegistration registration, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        var jurisdiction = string.IsNullOrEmpty(registration.JurisdictionId) ? null : await _jurisdictions.FindByIdAsync(registration.JurisdictionId);

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{registration.Name} {jurisdiction?.Name} {registration.RegistrationNumber}".ToLowerInvariant();
        }

        return Combine(
            View("MerchantTaxRegistration_Fields_SummaryAdmin", registration)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<MerchantTaxRegistrationRowViewModel>("MerchantTaxRegistration_Tags_SummaryAdmin", model =>
            {
                model.Registration = registration;
                model.JurisdictionName = jurisdiction?.Name ?? registration.JurisdictionId;
            })
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("MerchantTaxRegistration_Buttons_SummaryAdmin", registration).Location("Actions:5"));
    }

    public override IDisplayResult Edit(MerchantTaxRegistration registration, BuildEditorContext context)
        => Initialize<MerchantTaxRegistrationViewModel>("MerchantTaxRegistrationFields_Edit", async model =>
        {
            model.Name = registration.Name;
            model.JurisdictionId = registration.JurisdictionId;
            model.TaxType = registration.TaxType;
            model.RegistrationNumber = registration.RegistrationNumber;
            model.IsActive = registration.IsActive;
            model.ThresholdAmount = registration.ThresholdAmount;
            model.EffectiveFrom = registration.EffectiveFromUtc is { } from ? DateOnly.FromDateTime(from) : null;
            model.EffectiveTo = registration.EffectiveToUtc is { } to ? DateOnly.FromDateTime(to) : null;
            model.Jurisdictions =
            [
                new SelectListItem(S["Choose a jurisdiction"], string.Empty),
                .. (await _jurisdictions.GetAllAsync())
                    .OrderBy(jurisdiction => jurisdiction.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(jurisdiction => new SelectListItem(jurisdiction.Name, jurisdiction.ItemId)),
            ];
            model.TaxTypes =
            [
                new SelectListItem(S["Every tax type"], string.Empty),
                .. (await _types.GetAllAsync())
                    .OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(type => new SelectListItem(type.Name, type.Name)),
            ];

            if (!string.IsNullOrEmpty(registration.TaxType) && !model.TaxTypes.Any(item => string.Equals(item.Value, registration.TaxType, StringComparison.OrdinalIgnoreCase)))
            {
                model.TaxTypes.Add(new SelectListItem(registration.TaxType, registration.TaxType));
            }
        }).Location("Content:1");

    public override async Task<IDisplayResult> UpdateAsync(MerchantTaxRegistration registration, UpdateEditorContext context)
    {
        var model = new MerchantTaxRegistrationViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        registration.Name = model.Name?.Trim();
        registration.JurisdictionId = string.IsNullOrWhiteSpace(model.JurisdictionId) ? null : model.JurisdictionId;
        registration.TaxType = string.IsNullOrWhiteSpace(model.TaxType) ? null : model.TaxType.Trim();
        registration.RegistrationNumber = string.IsNullOrWhiteSpace(model.RegistrationNumber) ? null : model.RegistrationNumber.Trim();
        registration.IsActive = model.IsActive;
        registration.ThresholdAmount = model.ThresholdAmount;

        // Effective dates are pure dates, stored as the start of the day.
        registration.EffectiveFromUtc = model.EffectiveFrom is { } from ? DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) : null;
        registration.EffectiveToUtc = model.EffectiveTo is { } to ? DateTime.SpecifyKind(to.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) : null;

        return Edit(registration, context);
    }
}
