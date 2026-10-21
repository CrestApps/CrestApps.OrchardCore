using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Deployments.Drivers;

/// <summary>
/// Shows a taxation deployment step in a deployment plan through its <c>{Step}_Summary</c> and <c>{Step}_Thumbnail</c>
/// shapes. The steps export every entry, so they have nothing to edit.
/// </summary>
/// <typeparam name="TStep">The deployment step type.</typeparam>
internal sealed class TaxationDeploymentStepDisplayDriver<TStep> : DisplayDriver<DeploymentStep, TStep>
    where TStep : DeploymentStep, new()
{
    private static readonly string _summaryShapeType = $"{typeof(TStep).Name}_Summary";
    private static readonly string _thumbnailShapeType = $"{typeof(TStep).Name}_Thumbnail";

    public override IDisplayResult Display(TStep step, BuildDisplayContext context)
    {
        return Combine(
            View(_summaryShapeType, step).Location("Summary", "Content"),
            View(_thumbnailShapeType, step).Location("Thumbnail", "Content"));
    }
}
