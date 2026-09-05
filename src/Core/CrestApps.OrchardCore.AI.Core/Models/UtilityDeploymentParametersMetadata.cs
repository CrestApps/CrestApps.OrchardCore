namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Stores the model parameters (for example reasoning effort) selected for the <em>utility</em> deployment,
/// separate from the chat deployment parameters held by <c>AIDeploymentParametersMetadata</c>. The chat and
/// utility deployments can be different models with different capabilities, so their parameter selections are
/// kept independent.
/// </summary>
/// <remarks>
/// These values are persisted on the profile, profile template, and chat interaction. Applying them to the
/// utility completion at runtime requires framework support in CrestApps.Core; until that ships they are
/// stored but not sent to the provider.
/// </remarks>
public sealed class UtilityDeploymentParametersMetadata
{
    /// <summary>
    /// Gets or sets the selected parameter values keyed by their registered technical name.
    /// </summary>
    public Dictionary<string, string> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
