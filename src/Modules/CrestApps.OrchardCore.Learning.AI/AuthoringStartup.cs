using CrestApps.OrchardCore.Learning.AI.Navigation;
using CrestApps.OrchardCore.Learning.AI.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.AI;

/// <summary>
/// Registers the authoring assistant: the author prompts page, its menu entry and the permissions.
/// </summary>
[Feature(LearningAIConstants.Features.Authoring)]
public sealed class AuthoringStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILearningAICompleter, LearningAICompleter>();
        services.AddScoped<LearningAuthoringAssistant>();
        services.AddPermissionProvider<LearningAIPermissionsProvider>();
        services.AddNavigationProvider<AuthoringAdminMenu>();
    }
}
