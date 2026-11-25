using CrestApps.OrchardCore.PointOfSale.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.PointOfSale.Drivers;

/// <summary>
/// Builds the editor of a register, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class RegisterEditDisplayDriver : DisplayDriver<RegisterEditViewModel>
{
    internal readonly IStringLocalizer S;

    public RegisterEditDisplayDriver(IStringLocalizer<RegisterEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(RegisterEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<RegisterEditViewModel>("Register_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Register"], "10")),
            Initialize<RegisterEditViewModel>("RegisterAddress_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Store Address"], "20")),
            Initialize<RegisterEditViewModel>("RegisterRoles_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Who May Sell Here"], "30")),
            Initialize<RegisterEditViewModel>("RegisterKeys_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Department Keys"], "40")));
    }

    public override async Task<IDisplayResult> UpdateAsync(RegisterEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Name,
            m => m.LocationId,
            m => m.Currency,
            m => m.IsEnabled,
            m => m.AddressLine1,
            m => m.AddressLine2,
            m => m.City,
            m => m.Region,
            m => m.PostalCode,
            m => m.Country,
            m => m.Roles,
            m => m.Keys);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the page script renumbers.
    protected override void BuildPrefix(RegisterEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(RegisterEditViewModel source, RegisterEditViewModel model)
    {
        model.ItemId = source.ItemId;
        model.Locations = source.Locations;
        model.AvailableRoles = source.AvailableRoles;
        model.Departments = source.Departments;
        model.Name = source.Name;
        model.LocationId = source.LocationId;
        model.Currency = source.Currency;
        model.IsEnabled = source.IsEnabled;
        model.Roles = source.Roles;
        model.Keys = source.Keys;
        model.AddressLine1 = source.AddressLine1;
        model.AddressLine2 = source.AddressLine2;
        model.City = source.City;
        model.Region = source.Region;
        model.PostalCode = source.PostalCode;
        model.Country = source.Country;
    }
}
