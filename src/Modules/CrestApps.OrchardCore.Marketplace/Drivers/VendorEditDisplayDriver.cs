using CrestApps.OrchardCore.Marketplace.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Marketplace.Drivers;

/// <summary>
/// Builds the editor of a vendor, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class VendorEditDisplayDriver : DisplayDriver<VendorEditViewModel>
{
    internal readonly IStringLocalizer S;

    public VendorEditDisplayDriver(IStringLocalizer<VendorEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(VendorEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<VendorEditViewModel>("VendorDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Vendor"], "10")),
            Initialize<VendorEditViewModel>("VendorCommission_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Commission"], "20")));
    }

    public override async Task<IDisplayResult> UpdateAsync(VendorEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Name,
            m => m.Email,
            m => m.UserNames,
            m => m.Country,
            m => m.Status,
            m => m.CommissionPercent);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates.
    protected override void BuildPrefix(VendorEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(VendorEditViewModel source, VendorEditViewModel model)
    {
        model.Name = source.Name;
        model.Email = source.Email;
        model.UserNames = source.UserNames;
        model.CommissionPercent = source.CommissionPercent;
        model.Country = source.Country;
        model.Status = source.Status;
    }
}
