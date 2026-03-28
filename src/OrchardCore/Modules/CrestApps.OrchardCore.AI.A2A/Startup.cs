using A2A;
using A2A.AspNetCore;
using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.A2A.Drivers;
using CrestApps.OrchardCore.AI.A2A.Functions;
using CrestApps.OrchardCore.AI.A2A.Handlers;
using CrestApps.OrchardCore.AI.A2A.Models;
using CrestApps.OrchardCore.AI.A2A.Services;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.A2A;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIProfile, AIProfileA2AConnectionsDisplayDriver>();
        services.AddDisplayDriver<AIProfileTemplate, AIProfileTemplateA2AConnectionsDisplayDriver>();
        services.AddDisplayDriver<ChatInteraction, ChatInteractionA2AConnectionsDisplayDriver>();
        services.AddScoped<IToolRegistryProvider, A2AToolRegistryProvider>();
        services.AddNavigationProvider<A2AAdminMenu>();
        services.AddPermissionProvider<A2APermissionsProvider>();
        services.AddScoped<ICatalogEntryHandler<A2AConnection>, A2AConnectionHandler>();
        services.AddScoped<ICatalogEntryHandler<A2AConnection>, A2AConnectionSettingsHandler>();
        services.AddDisplayDriver<A2AConnection, A2AConnectionDisplayDriver>();
        services.AddScoped<IAICompletionContextBuilderHandler, A2AAICompletionContextBuilderHandler>();
        services.AddSingleton<IA2AAgentCardCacheService, DefaultA2AAgentCardCacheService>();
        services.AddScoped<IA2AConnectionAuthService, DefaultA2AConnectionAuthService>();

        // Register AI system functions for agent and tool discovery.
        services.AddAITool<ListAvailableAgentsFunction>(ListAvailableAgentsFunction.TheName);
        services.AddAITool<FindAgentForTaskFunction>(FindAgentForTaskFunction.TheName);
        services.AddAITool<FindToolsForTaskFunction>(FindToolsForTaskFunction.TheName);
    }
}

[Feature(A2AConstants.Feature.Host)]
public sealed class A2AHostStartup : StartupBase
{
    private const string A2AHostPolicyName = "A2AHostPolicy";

    private readonly IShellConfiguration _shellConfiguration;

    public A2AHostStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<A2AHostOptions>(_shellConfiguration.GetSection("CrestApps_AI:A2AHost"));

        services.AddPermissionProvider<A2AHostPermissionsProvider>();

        services.AddScoped<IAuthorizationHandler, A2AHostAuthorizationHandler>();

        services.AddAuthentication()
            .AddScheme<A2AApiKeyAuthenticationOptions, A2AApiKeyAuthenticationHandler>(
                A2AApiKeyAuthenticationDefaults.AuthenticationScheme, options => { });

        services.AddSingleton(A2ATaskManagerFactory.Create);

        services.AddAuthorizationBuilder()
            .AddPolicy(A2AHostPolicyName, policy =>
            {
                policy.AddAuthenticationSchemes(A2AApiKeyAuthenticationDefaults.AuthenticationScheme, "Api");
                policy.AddRequirements(new A2AHostAuthorizationRequirement());
            });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var taskManager = serviceProvider.GetRequiredService<ITaskManager>();

        // The well-known endpoint is always public so clients can discover agents and auth requirements.
        routes.MapGet("/.well-known/agent-card.json", A2AWellKnownEndpointHandler.HandleAsync);

        // Always apply the authorization policy. The A2AHostAuthorizationHandler dynamically
        // checks A2AHostOptions.AuthenticationType on every request, allowing the "None" mode
        // to pass through without credentials.
        routes.MapA2A(taskManager, "a2a")
            .RequireAuthorization(A2AHostPolicyName);
    }
}
