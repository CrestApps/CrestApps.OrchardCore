using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

internal sealed class AIProfileDeploymentSource : DeploymentSourceBase<AIProfileDeploymentStep>
{
    private readonly INamedModelStore<AIProfile> _profileStore;

    public AIProfileDeploymentSource(
        INamedModelStore<AIProfile> profileStore)
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
                { "Id", profile.Id },
                { "Source", profile.Source },
                { "Name", profile.Name },
                { "DisplayText", profile.DisplayText },
                { "WelcomeMessage", profile.WelcomeMessage },
                { "Type", profile.Type.ToString() },
                { "PromptTemplate", profile.PromptTemplate },
                { "DeploymentId", profile.DeploymentId },
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
