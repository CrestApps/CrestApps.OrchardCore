using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Exports every ticket priority.
/// </summary>
public sealed class TicketPrioritiesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TicketPrioritiesDeploymentStep>("Tickets");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TicketPrioritiesDeploymentStep>("Ticket priorities");

    /// <inheritdoc/>
    public override string Name => TicketsConstants.RecipeSteps.Priorities;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
