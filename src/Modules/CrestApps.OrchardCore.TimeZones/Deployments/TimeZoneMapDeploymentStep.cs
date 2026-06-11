using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.TimeZones.Deployments;

/// <summary>
/// Represents a deployment step that exports time zone maps.
/// </summary>
public sealed class TimeZoneMapDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneMapDeploymentStep"/> class.
    /// </summary>
    public TimeZoneMapDeploymentStep()
    {
        Name = TimeZonesConstants.Recipes.TimeZoneMaps;
    }

    /// <summary>
    /// Gets or sets a value indicating whether all maps should be exported.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the selected map identifiers.
    /// </summary>
    public string[] MapIds { get; set; }
}
