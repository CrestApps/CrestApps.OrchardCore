using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tickets.Deployments;

/// <summary>
/// Exports every ticket view.
/// </summary>
public sealed class TicketViewsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TicketViewsDeploymentStep>("Tickets");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TicketViewsDeploymentStep>("Ticket views");

    /// <inheritdoc/>
    public override string Name => TicketsConstants.RecipeSteps.Views;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
