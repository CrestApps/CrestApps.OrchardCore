using CrestApps.OrchardCore.YesSql.Core;
using CrestApps.OrchardCore.YesSql.Core.Indexes;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

public sealed class AIProfileIndex : CatalogItemIndex, INameAwareIndex
{
    /// <summary>
    /// Gets or sets the technical name of the profile.
    /// Maps to <see cref="Models.AIProfile.Name"/> to satisfy <see cref="INameAwareIndex"/>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the profile type (e.g., Chat, TemplatePrompt, Utility, Agent).
    /// Stored as the string representation of <see cref="Models.AIProfileType"/>.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the description of the profile.
    /// Primarily used for Agent profiles to describe agent capabilities.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the deployment identifier associated with the profile.
    /// </summary>
    public string DeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the orchestrator name used by the profile.
    /// </summary>
    public string OrchestratorName { get; set; }

    /// <summary>
    /// Gets or sets the owner user identifier.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the author name.
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Gets or sets whether the profile is listable in the admin UI.
    /// </summary>
    public bool IsListable { get; set; }
}
