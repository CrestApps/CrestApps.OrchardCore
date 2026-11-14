using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Exports every desk.
/// </summary>
public sealed class TicketDesksDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TicketDesksDeploymentStep>("Tickets");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TicketDesksDeploymentStep>("Desks");

    /// <inheritdoc/>
    public override string Name => TicketsConstants.RecipeSteps.Desks;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
