using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Drivers;
using CrestApps.OrchardCore.AI.Endpoints.Api;
using CrestApps.OrchardCore.AI.Migrations;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Registers the AI Quality feature: simulation test cases and their runs.
/// </summary>
[Feature(AIConstants.Feature.Quality)]
public sealed class QualityStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IAITestCaseStore, AITestCaseStore>()
            .AddScoped<IAITestRunStore, AITestRunStore>()
            .AddScoped<AITestRunner>()
            .AddIndexProvider<AITestIndexProvider>()
            .AddIndexProvider<AITestRunIndexProvider>()
            .AddDataMigration<AIQualityIndexMigrations>()
            .AddDisplayDriver<AITestCase, AITestCaseDisplayDriver>()
            .AddDisplayDriver<AITestRun, AITestRunDisplayDriver>()
            .AddDisplayDriver<AIChatSessionEntry, AIChatSessionEntryTestDisplayDriver>()
            .AddNavigationProvider<AIQualityAdminMenu>()
            .AddPermissionProvider<AIQualityPermissionsProvider>();

        services.TryAddScoped<AIProfileVersionCopies>();
        services.AddSingleton<IBackgroundTask, AITestRunBackgroundTask>();
    }
}

/// <summary>
/// Maps the AI tests API when both the AI Quality and the AI Chat WebAPI features are on.
/// </summary>
[RequireFeatures(AIConstants.Feature.Quality, AIConstants.Feature.ChatApi)]
public sealed class QualityApiStartup : StartupBase
{
    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapAITestsApi();
}
