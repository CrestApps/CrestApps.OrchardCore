using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Calendars.Deployments;

/// <summary>
/// Exports calendars, and optionally their events, as a Calendars recipe step.
/// </summary>
public sealed class CalendarsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<CalendarsDeploymentStep>("Calendar");
    private static readonly LocalizationSource _title = LocalizationSource.Create<CalendarsDeploymentStep>("Calendars");

    /// <inheritdoc />
    public override string Name => CalendarsConstants.RecipeStepName;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets whether the events are exported with their calendars.
    /// </summary>
    public bool IncludeEvents { get; set; }

    /// <summary>
    /// Gets or sets whether the categories (everyone's) are exported too.
    /// </summary>
    public bool IncludeCategories { get; set; } = true;
}
