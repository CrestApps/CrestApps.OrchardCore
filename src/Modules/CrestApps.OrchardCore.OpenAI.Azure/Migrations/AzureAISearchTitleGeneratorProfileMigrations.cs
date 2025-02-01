using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Models;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Migrations;

internal sealed class AzureAISearchTitleGeneratorProfileMigrations : DataMigration
{
    private readonly IAIChatProfileManager _chatProfileManager;
    private readonly IAIDeploymentStore _deploymentStore;

    public AzureAISearchTitleGeneratorProfileMigrations(
        IAIChatProfileManager chatProfileManager,
        IAIDeploymentStore deploymentStore)
    {
        _chatProfileManager = chatProfileManager;
        _deploymentStore = deploymentStore;
    }

    public async Task<int> CreateAsync()
    {
        var deployment = (await _deploymentStore.GetDeploymentsAsync(AzureWithAzureAISearchProfileSource.Key)).FirstOrDefault();

        var profile = await _chatProfileManager.NewAsync(AzureWithAzureAISearchProfileSource.Key);

        profile.Name = AIConstants.GetTitleGeneratorProfileName(AzureWithAzureAISearchProfileSource.Key);
        profile.Type = AIChatProfileType.Chat;
        profile.DeploymentId = deployment?.Id;

        profile.WithSettings(new AIChatProfileSettings
        {
            IsRemovable = false,
            IsOnAdminMenu = false,
        });

        profile.WithSettings(new OpenAIChatProfileSettings
        {
            LockSystemMessage = true,
        });

        profile.Put(new OpenAIChatProfileMetadata
        {
            SystemMessage = OpenAIConstants.TitleGeneratorSystemMessage,
        });

        await _chatProfileManager.SaveAsync(profile);

        return 1;
    }
}
