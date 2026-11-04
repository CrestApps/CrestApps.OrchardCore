using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

/// <summary>
/// Writes the selected AI profiles to a deployment plan, in the shape <c>AIProfileStep</c> reads.
/// </summary>
internal sealed class AIProfileDeploymentSource : CatalogDeploymentSourceBase<AIProfileDeploymentStep, AIProfile>
{
    private readonly IAIProfileStore _profileStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileDeploymentSource"/> class.
    /// </summary>
    /// <param name="profileStore">The profile store.</param>
    public AIProfileDeploymentSource(IAIProfileStore profileStore)
        : base("profiles")
    {
        _profileStore = profileStore;
    }

    protected override async Task<IEnumerable<AIProfile>> GetEntriesAsync()
        => await _profileStore.GetAllAsync();

    protected override bool IsSelected(AIProfileDeploymentStep step, AIProfile entry)
        => IsSelectedByKey(step.IncludeAll, step.ProfileNames, entry.Name);

    protected override JsonObject Export(AIProfile entry)
    {
        var profileInfo = new JsonObject()
        {
            { "ItemId", entry.ItemId },
            { "Name", entry.Name },
            { "DisplayText", entry.DisplayText },
            { "WelcomeMessage", entry.WelcomeMessage },
            { "Type", entry.Type.ToString() },
            { "PromptTemplate", entry.PromptTemplate },
            { "ChatDeploymentName", entry.ChatDeploymentName },
            { "UtilityDeploymentName", entry.UtilityDeploymentName },
            { "CreatedUtc", entry.CreatedUtc },
            { "OwnerId", entry.OwnerId },
            { "Author", entry.Author },
            { "Settings", JsonSerializer.SerializeToNode(entry.Settings) },
            { "Properties", JsonSerializer.SerializeToNode(entry.Properties) },
        };

        if (entry.TitleType.HasValue)
        {
            profileInfo.Add("TitleType", entry.TitleType.Value.ToString());
        }

        return profileInfo;
    }
}
