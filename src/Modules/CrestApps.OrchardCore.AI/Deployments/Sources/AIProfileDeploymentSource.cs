using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

public sealed class AIProfileDeploymentSource : DeploymentSourceBase<AIProfileDeploymentStep>
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
            };

            if (profile.TitleType.HasValue)
            {
                profileInfo.Add("TitleType", profile.TitleType.Value.ToString());
            }

            var properties = new JsonObject();

            foreach (var property in profile.Properties)
            {
                properties[property.Key] = property.Value.DeepClone();
            }

            profileInfo["Properties"] = properties;

            var settings = new JsonObject();

            foreach (var pair in profile.Settings)
            {
                settings[pair.Key] = pair.Value.DeepClone();
            }

            profileInfo["Settings"] = settings;

            profilesData.Add(profileInfo);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["profiles"] = profilesData,
        });
    }
}
