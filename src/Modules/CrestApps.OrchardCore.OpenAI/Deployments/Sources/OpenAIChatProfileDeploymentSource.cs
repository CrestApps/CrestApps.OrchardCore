using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.OpenAI.Deployments.Sources;

public sealed class OpenAIChatProfileDeploymentSource : DeploymentSourceBase<OpenAIChatProfileDeploymentStep>
{
    private readonly IOpenAIChatProfileStore _profileStore;

    public OpenAIChatProfileDeploymentSource(IOpenAIChatProfileStore profileStore)
    {
        _profileStore = profileStore;
    }

    protected override async Task ProcessAsync(OpenAIChatProfileDeploymentStep step, DeploymentPlanResult result)
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
                { "Source", profile.Source },
                { "Name", profile.Name },
                { "WelcomeMessage", profile.WelcomeMessage },
                { "Type", profile.Type.ToString() },
                { "PromptTemplate", profile.PromptTemplate },
                { "DeploymentId", profile.DeploymentId },
                { "SystemMessage", profile.SystemMessage },
            };

            if (profile.FunctionNames is not null && profile.FunctionNames.Length > 0)
            {
                var functionNames = new JsonArray();

                foreach (var functionName in profile.FunctionNames)
                {
                    functionNames.Add(functionName);
                }

                profileInfo.Add("FunctionNames", functionNames);
            }

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

            profilesData.Add(profileInfo);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["profiles"] = profilesData,
        });
    }
}
