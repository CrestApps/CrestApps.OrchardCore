using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

internal sealed class AIProfileDeploymentSource : DeploymentSourceBase<AIProfileDeploymentStep>
{
    private readonly IAIProfileStore _profileStore;

    public AIProfileDeploymentSource(IAIProfileStore profileStore)
    {
        _profileStore = profileStore;
    }

    protected override async Task ProcessAsync(AIProfileDeploymentStep step, DeploymentPlanResult result)
    {
        var profiles = await _profileStore.GetAllAsync();

        var profilesData = new JsonArray();

        var profileNames = step.IncludeAll
            ? []
            : step.ProfileNames ?? [];

        foreach (var profile in profiles)
        {
            if (profileNames.Length > 0 && !profileNames.Contains(profile.Name))
            {
                continue;
            }

            var profileInfo = new JsonObject()
            {
                { "ItemId" , profile.ItemId },
                { "Name", profile.Name },
                { "DisplayText", profile.DisplayText },
                { "WelcomeMessage", profile.WelcomeMessage },
                { "Type", profile.Type.ToString() },
                { "PromptTemplate", profile.PromptTemplate },
                { "ChatDeploymentId", profile.ChatDeploymentId },
                { "CreatedUtc", profile.CreatedUtc },
                { "OwnerId", profile.OwnerId },
                { "Author", profile.Author },
                { "Settings", profile.Settings?.DeepClone() },
                { "Properties", profile.Properties?.DeepClone() },
            };

            if (profile.TitleType.HasValue)
            {
                profileInfo.Add("TitleType", profile.TitleType.Value.ToString());
            }

            profilesData.Add(profileInfo);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["profiles"] = profilesData,
        });
    }
}
