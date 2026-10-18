using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Inventory.Deployments;

internal sealed class StockDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, StockDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(StockDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("StockDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("StockDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(StockDeploymentStep step, BuildEditorContext context)
        => View("StockDeploymentStep_Fields_Edit", step).Location("Content");
}
