using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Scheduling.Deployments;

/// <summary>
/// Exports the scheduling setup: appointment types and resources, and optionally the appointments and the waitlist.
/// </summary>
public sealed class SchedulingDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<SchedulingDeploymentStep>("Calendar");
    private static readonly LocalizationSource _title = LocalizationSource.Create<SchedulingDeploymentStep>("Scheduling");

    /// <inheritdoc />
    public override string Name => SchedulingConstants.RecipeStepName;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets whether the appointments are exported too.
    /// </summary>
    public bool IncludeAppointments { get; set; }

    /// <summary>
    /// Gets or sets whether the waitlist is exported too.
    /// </summary>
    public bool IncludeWaitlist { get; set; }
}
