using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.DeepSeek.Core.Services;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.DeepSeek.Migrations;

internal sealed class DeepSeekTitleGeneratorProfileMigrations : DataMigration
{
    private readonly IAIProfileManager _chatProfileManager;

    public DeepSeekTitleGeneratorProfileMigrations(IAIProfileManager chatProfileManager)
    {
        _chatProfileManager = chatProfileManager;
    }

    public async Task<int> CreateAsync()
    {
        var profile = await _chatProfileManager.NewAsync(DeepSeekAIDeploymentProvider.ProviderName);

        profile.Name = AIConstants.GetTitleGeneratorProfileName(DeepSeekAIDeploymentProvider.ProviderName);
        profile.DisplayText = "Chat Title Generator";
        profile.Type = AIProfileType.Utility;

        profile.WithSettings(new AIProfileSettings
        {
            IsRemovable = false,
            IsOnAdminMenu = false,
        });

        profile.WithSettings(new AIProfileSettings
        {
            LockSystemMessage = true,
        });

        profile.Put(new AIProfileMetadata
        {
            SystemMessage = AIConstants.TitleGeneratorSystemMessage,
        });

        await _chatProfileManager.SaveAsync(profile);

        return 1;
    }
}
