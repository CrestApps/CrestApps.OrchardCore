using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Exports every ticket status.
/// </summary>
public sealed class TicketStatusesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TicketStatusesDeploymentStep>("Tickets");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TicketStatusesDeploymentStep>("Ticket statuses");

    /// <inheritdoc/>
    public override string Name => TicketsConstants.RecipeSteps.Statuses;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
