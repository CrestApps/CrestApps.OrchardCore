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
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets whether the events are exported with their calendars.
    /// </summary>
    public bool IncludeEvents { get; set; }
}
