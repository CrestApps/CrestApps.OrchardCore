using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Migrations;

internal sealed class AzureTitleGeneratorProfileMigrations : DataMigration
{
    private readonly IAIProfileManager _chatProfileManager;

    public AzureTitleGeneratorProfileMigrations(IAIProfileManager chatProfileManager)
    {
        _chatProfileManager = chatProfileManager;
    }

    public async Task<int> CreateAsync()
    {
        var profile = await _chatProfileManager.NewAsync(AzureProfileSource.Key);

        profile.Name = AIConstants.GetTitleGeneratorProfileName(AzureProfileSource.Key);
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
