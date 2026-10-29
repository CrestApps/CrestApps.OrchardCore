using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Integrations;
using CrestApps.OrchardCore.AI.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.AI.Integrations;

/// <summary>
/// Registers the AI Integrations feature: CRM, Notion and file store connections, the CRM lookup tool and the Log to CRM task.
/// </summary>
[Feature(AIConstants.Feature.Integrations)]
public sealed class AIIntegrationsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIIntegrationsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIIntegrationsStartup(IStringLocalizer<AIIntegrationsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IAIIntegrationConnectionStore, AIIntegrationConnectionStore>()
            .AddScoped<AIIntegrationTokens>()
            .AddScoped<AIIntegrationClients>()
            .AddIndexProvider<AIIntegrationConnectionIndexProvider>()
            .AddDataMigration<AIIntegrationIndexMigrations>()
            .AddDisplayDriver<AIIntegrationConnection, AIIntegrationConnectionDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileIntegrationDisplayDriver>()
            .AddNavigationProvider<AIIntegrationsAdminMenu>()
            .AddPermissionProvider<AIIntegrationsPermissionsProvider>();

        services.AddHttpClient(AIIntegrationTokens.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(20));

        services.AddCoreAITool<FindCrmContactTool>(FindCrmContactTool.TheName)
            .WithTitle(S["Find CRM Contact"])
            .WithDescription(S["Looks the customer up in the profile's CRM by phone."])
            .WithCategory(S["Integrations"])
            .Selectable();
    }
}

/// <summary>
/// Registers the Log to CRM workflow task.
/// </summary>
[Feature(AIConstants.Feature.Integrations)]
[RequireFeatures("OrchardCore.Workflows")]
public sealed class AIIntegrationsWorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddActivity<LogToCrmTask, LogToCrmTaskDisplayDriver>();
}

/// <summary>
/// Adds the integrations page to the Artificial Intelligence menu.
/// </summary>
public sealed class AIIntegrationsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIIntegrationsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIIntegrationsAdminMenu(IStringLocalizer<AIIntegrationsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Artificial Intelligence"], ai => ai
                .Add(S["Integrations"], S["Integrations"].PrefixPosition(), integrations => integrations
                    .Id("aiIntegrations")
                    .Action("Index", "AIIntegrations", AIConstants.Feature.Area)
                    .Permission(AIPermissions.ManageAIIntegrations)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Declares the integrations permission, granted to administrators.
/// </summary>
internal sealed class AIIntegrationsPermissionsProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        AIPermissions.ManageAIIntegrations,
    ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <inheritdoc/>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = global::OrchardCore.OrchardCoreConstants.Roles.Administrator,
            Permissions = _allPermissions,
        },
    ];
}
