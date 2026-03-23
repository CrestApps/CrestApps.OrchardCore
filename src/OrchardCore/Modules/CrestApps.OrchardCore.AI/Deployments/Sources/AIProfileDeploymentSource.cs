using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

internal sealed class AIProfileDeploymentSource : DeploymentSourceBase<AIProfileDeploymentStep>
{
    private readonly INamedCatalog<AIProfile> _profilesCatalog;

    public AIProfileDeploymentSource(INamedCatalog<AIProfile> profilesCatalog)
    {
        _profilesCatalog = profilesCatalog;
    }

    protected override async Task ProcessAsync(AIProfileDeploymentStep step, DeploymentPlanResult result)
    {
        var profiles = await _profilesCatalog.GetAllAsync();

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
                { "Source", profile.Source },
                { "Name", profile.Name },
                { "DisplayText", profile.DisplayText },
                { "WelcomeMessage", profile.WelcomeMessage },
                { "Type", profile.Type.ToString() },
                { "PromptTemplate", profile.PromptTemplate },
                { "ChatDeploymentId", profile.ChatDeploymentId },
                { "CreatedUtc", profile.CreatedUtc },
                { "OwnerId", profile.OwnerId },
                { "Author", profile.Author },
                { "Settings", JsonSerializer.SerializeToNode(profile.Settings) },
                { "Properties", JsonSerializer.SerializeToNode(profile.Properties) },
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
