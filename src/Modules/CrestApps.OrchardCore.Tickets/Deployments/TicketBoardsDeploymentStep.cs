using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Exports every ticket board.
/// </summary>
public sealed class TicketBoardsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TicketBoardsDeploymentStep>("Tickets");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TicketBoardsDeploymentStep>("Ticket boards");

    /// <inheritdoc/>
    public override string Name => TicketsConstants.RecipeSteps.Boards;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
