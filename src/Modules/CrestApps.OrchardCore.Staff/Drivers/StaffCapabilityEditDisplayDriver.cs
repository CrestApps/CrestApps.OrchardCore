using CrestApps.OrchardCore.Staff.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Staff.Drivers;

/// <summary>
/// The card of the form of a staff capability.
/// </summary>
internal sealed class StaffCapabilityEditDisplayDriver : DisplayDriver<StaffCapabilityEditViewModel>
{
    internal readonly IStringLocalizer S;

    public StaffCapabilityEditDisplayDriver(IStringLocalizer<StaffCapabilityEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(StaffCapabilityEditViewModel model, BuildEditorContext context)
    {
        return Initialize<StaffCapabilityEditViewModel>("StaffCapability_Edit", m => Populate(model, m))
            .Location(l => l.Zone("Content", "1").Card(S["Capability"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(StaffCapabilityEditViewModel model, UpdateEditorContext context)
    {
        // The key of a saved capability never changes, so only a new one binds it.
        if (context.IsNew)
        {
            await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.DisplayName, m => m.Description, m => m.Position, m => m.Enabled);
        }
        else
        {
            await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.DisplayName, m => m.Description, m => m.Position, m => m.Enabled);
        }

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(StaffCapabilityEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(StaffCapabilityEditViewModel source, StaffCapabilityEditViewModel model)
    {
        model.Name = source.Name;
        model.DisplayName = source.DisplayName;
        model.Description = source.Description;
        model.Position = source.Position;
        model.Enabled = source.Enabled;
        model.IsNew = source.IsNew;
        model.IsBuiltIn = source.IsBuiltIn;
    }
}
