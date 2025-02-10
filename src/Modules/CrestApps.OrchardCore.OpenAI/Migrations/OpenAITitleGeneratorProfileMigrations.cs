using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.OpenAI.Services;
using CrestApps.OrchardCore.OpenAI.Core;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Migrations;

internal sealed class OpenAITitleGeneratorProfileMigrations : DataMigration
{
    private readonly IAIProfileManager _chatProfileManager;

    public OpenAITitleGeneratorProfileMigrations(IAIProfileManager chatProfileManager)
    {
        _chatProfileManager = chatProfileManager;
    }

    public async Task<int> CreateAsync()
    {
        var profile = await _chatProfileManager.NewAsync(OpenAIProfileSource.Key);

        profile.Name = AIConstants.GetTitleGeneratorProfileName(OpenAIProfileSource.Key);
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
            SystemMessage = OpenAIConstants.TitleGeneratorSystemMessage,
        });

        await _chatProfileManager.SaveAsync(profile);

        return 1;
    }
}
