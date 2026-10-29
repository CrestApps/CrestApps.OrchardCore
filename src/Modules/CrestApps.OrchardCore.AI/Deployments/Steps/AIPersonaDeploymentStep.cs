using System.Text.Json.Serialization;
using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

/// <summary>
/// Exports AI personas.
/// </summary>
public sealed class AIPersonaDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<AIPersonaDeploymentStep>("Artificial Intelligence");
    private static readonly LocalizationSource _title = LocalizationSource.Create<AIPersonaDeploymentStep>("AI Personas");

    /// <inheritdoc />
    public override string Name => AIPersonaStep.StepKey;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets a value indicating whether every saved persona is exported.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the item ids of the personas to export when <see cref="IncludeAll"/> is <see langword="false"/>.
    /// </summary>
    public string[] PersonaIds { get; set; }
}
