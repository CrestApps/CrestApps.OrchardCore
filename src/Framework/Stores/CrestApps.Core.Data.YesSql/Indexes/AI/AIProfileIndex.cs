using CrestApps.Core.AI.Models;
using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Indexes.AI;

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
    /// Gets or sets the legacy deployment identifier compatibility value.
    /// This mirrors <see cref="DeploymentName"/> so older queries do not lose data.
    /// </summary>
    public string DeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the deployment technical name associated with the profile.
    /// </summary>
    public string DeploymentName { get; set; }

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

public sealed class AIProfileIndexProvider : IndexProvider<AIProfile>
{
    public AIProfileIndexProvider()
    {
        CollectionName = OrchardCoreAICollectionNames.AI;
    }

    public override void Describe(DescribeContext<AIProfile> context)
    {
        context
            .For<AIProfileIndex>()
            .Map(profile =>
            {
                var settings = profile.GetSettings<AIProfileSettings>();

                return new AIProfileIndex
                {
                    ItemId = profile.ItemId,
                    Name = profile.Name,
                    Type = profile.Type.ToString(),
                    Description = profile.Description,
                    DeploymentId = profile.ChatDeploymentName,
                    DeploymentName = profile.ChatDeploymentName,
                    OrchestratorName = profile.OrchestratorName,
                    OwnerId = profile.OwnerId,
                    Author = profile.Author,
                    IsListable = settings.IsListable,
                };
            });
    }
}
