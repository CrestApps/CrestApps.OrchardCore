using CrestApps.OrchardCore.OpenAI.Models;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.OpenAI.Migrations;

public sealed class TestMigMigrations : DataMigration
{
    private readonly IOpenAIChatProfileManager _openAIChatProfileManager;
    private readonly IOpenAIDeploymentManager _openAIDeploymentManager;

    public TestMigMigrations(
        IOpenAIChatProfileManager openAIChatProfileManager,
        IOpenAIDeploymentManager openAIDeploymentManager)
    {
        _openAIChatProfileManager = openAIChatProfileManager;
        _openAIDeploymentManager = openAIDeploymentManager;
    }

    public async Task<int> CreateAsync()
    {
        var deployments = await _openAIDeploymentManager.GetAllAsync();

        if (!deployments.Any())
        {
            return 1;
        }

        var profile = await _openAIChatProfileManager.NewAsync("Azure");

        profile.Name = "TechnicalName";
        profile.Type = OpenAIChatProfileType.Chat;
        profile.DeploymentId = deployments.First().Id;
        profile.SystemMessage = "some system message";
        profile.WelcomeMessage = "some welcome message";

        profile.WithSettings(new OpenAIChatProfileSettings
        {
            LockSystemMessage = true,
            IsRemovable = false,
        });

        await _openAIChatProfileManager.SaveAsync(profile);

        return 1;
    }
}
