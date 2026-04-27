using A2A;
using A2A.AspNetCore;
using CrestApps.Core.AI.A2A;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.A2A.Services;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.A2A.Drivers;
using CrestApps.OrchardCore.AI.A2A.Handlers;
using CrestApps.OrchardCore.AI.A2A.Migrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.A2A;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAIA2AClient()
            .AddCoreAIA2AClientStoresYesSql()
            .AddDataMigration<A2AConnectionMigrations>();

        services.AddDisplayDriver<AIProfile, AIProfileA2AConnectionsDisplayDriver>();
        services.AddDisplayDriver<AIProfileTemplate, AIProfileTemplateA2AConnectionsDisplayDriver>();
        services.AddDisplayDriver<ChatInteraction, ChatInteractionA2AConnectionsDisplayDriver>();
        services.AddNavigationProvider<A2AAdminMenu>();
        services.AddPermissionProvider<A2APermissionsProvider>();
        services.AddScoped<ICatalogEntryHandler<A2AConnection>, A2AConnectionHandler>();
        services.AddScoped<ICatalogEntryHandler<A2AConnection>, A2AConnectionSettingsHandler>();
        services.AddDisplayDriver<A2AConnection, A2AConnectionDisplayDriver>();
    }
}

/// <summary>
/// Registers services and configuration for the A2AHost feature.
/// </summary>
[Feature(A2AConstants.Feature.Host)]
public sealed class A2AHostStartup : StartupBase
{
    private const string A2AHostPolicyName = "A2AHostPolicy";

    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2AHostStartup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration.</param>
    public A2AHostStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<A2AHostOptions>(_shellConfiguration.GetSection("CrestApps:A2AHost"));
        services.PostConfigure<A2AHostOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(_shellConfiguration["CrestApps:A2AHost:AuthenticationType"]))
            {
                options.AuthenticationType = A2AHostAuthenticationType.OpenId;
            }
        });

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
