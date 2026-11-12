using CrestApps.OrchardCore.Assets.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Assets.Drivers;

/// <summary>
/// The staff member who drives a fleet vehicle by default, so the technician app shows it as theirs.
/// </summary>
internal sealed class FleetDriverDisplayDriver : DisplayDriver<Asset>
{
    public override IDisplayResult Edit(Asset asset, BuildEditorContext context)
        => Initialize<FleetDriverViewModel>("AssetFleetDriver_Edit", model => model.DefaultDriverWorkerId = asset.Fleet?.DefaultDriverWorkerId)
            .Location("Content:6")
            .RenderWhen(() => Task.FromResult(asset.Fleet is not null));

    public override async Task<IDisplayResult> UpdateAsync(Asset asset, UpdateEditorContext context)
    {
        if (asset.Fleet is null)
        {
            return null;
        }

        var model = new FleetDriverViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        asset.Fleet.DefaultDriverWorkerId = string.IsNullOrWhiteSpace(model.DefaultDriverWorkerId) ? null : model.DefaultDriverWorkerId.Trim();

        return Edit(asset, context);
    }
}

/// <summary>
/// The values of the fleet driver card.
/// </summary>
public class FleetDriverViewModel
{
    /// <summary>
    /// Gets or sets the staff member who drives it by default.
    /// </summary>
    public string DefaultDriverWorkerId { get; set; }
}
