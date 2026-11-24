using CrestApps.OrchardCore.Learning.AI.Drivers;
using CrestApps.OrchardCore.Learning.AI.Navigation;
using CrestApps.OrchardCore.Learning.AI.Services;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.AI;

/// <summary>
/// Registers the authoring assistant: the author prompts page, its menu entry, the permissions, and the scoring help
/// on the grading pages.
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
        services.AddScoped<EssayScoringAssistant>();
        services.AddDisplayDriver<GradingAssistContext, ScoringHelpDisplayDriver>();
    }
}
