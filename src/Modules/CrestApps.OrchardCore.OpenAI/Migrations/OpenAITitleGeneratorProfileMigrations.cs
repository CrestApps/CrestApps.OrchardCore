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
    private readonly IAIChatProfileManager _chatProfileManager;

    public OpenAITitleGeneratorProfileMigrations(IAIChatProfileManager chatProfileManager)
    {
        _chatProfileManager = chatProfileManager;
    }

    public async Task<int> CreateAsync()
    {
        var profile = await _chatProfileManager.NewAsync(OpenAIProfileSource.Key);

        profile.Name = AIConstants.GetTitleGeneratorProfileName(OpenAIProfileSource.Key);
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
            SystemMessage = OpenAIConstants.TitleGeneratorSystemMessage,
        });

        await _chatProfileManager.SaveAsync(profile);

        return 1;
    }
}
