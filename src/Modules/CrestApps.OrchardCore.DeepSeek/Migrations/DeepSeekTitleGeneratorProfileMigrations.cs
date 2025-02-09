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
    private readonly IAIChatProfileManager _chatProfileManager;

    public DeepSeekTitleGeneratorProfileMigrations(IAIChatProfileManager chatProfileManager)
    {
        _chatProfileManager = chatProfileManager;
    }

    public async Task<int> CreateAsync()
    {
        var profile = await _chatProfileManager.NewAsync(DeepSeekAIDeploymentProvider.ProviderName);

        profile.Name = AIConstants.GetTitleGeneratorProfileName(DeepSeekAIDeploymentProvider.ProviderName);
        profile.DisplayText = "Chat Title Generator";
        profile.Type = AIChatProfileType.Utility;

        profile.WithSettings(new AIChatProfileSettings
        {
            IsRemovable = false,
            IsOnAdminMenu = false,
        });

        profile.WithSettings(new AIChatProfileSettings
        {
            LockSystemMessage = true,
        });

        profile.Put(new AIChatProfileMetadata
        {
            SystemMessage = AIConstants.TitleGeneratorSystemMessage,
        });

        await _chatProfileManager.SaveAsync(profile);

        return 1;
    }
}
