using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Core.Deployments;

/// <summary>
/// Shows a deployment step that has nothing to edit, such as one that exports every record of a store, through its
/// <c>{Step}_Summary</c> and <c>{Step}_Thumbnail</c> shapes. The module that owns the step provides both views.
/// </summary>
/// <typeparam name="TStep">The deployment step type.</typeparam>
public sealed class SummaryDeploymentStepDisplayDriver<TStep> : DisplayDriver<DeploymentStep, TStep>
    where TStep : DeploymentStep, new()
{
    private static readonly string _summaryShapeType = $"{typeof(TStep).Name}_Summary";
    private static readonly string _thumbnailShapeType = $"{typeof(TStep).Name}_Thumbnail";

    /// <inheritdoc/>
    public override IDisplayResult Display(TStep step, BuildDisplayContext context)
    {
        return Combine(
            View(_summaryShapeType, step).Location("Summary", "Content"),
            View(_thumbnailShapeType, step).Location("Thumbnail", "Content"));
    }
}
