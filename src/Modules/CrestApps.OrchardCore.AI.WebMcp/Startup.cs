using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebMcp;
using CrestApps.Core.AI.WebMcp.Endpoints;
using CrestApps.Core.AI.WebMcp.Models;
using CrestApps.Core.AI.WebMcp.Services;
using CrestApps.OrchardCore.AI.Chat.Hubs;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.AI.WebMcp.Drivers;
using CrestApps.OrchardCore.AI.WebMcp.Filters;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.WebMcp;

/// <summary>
/// Registers the services of the WebMCP Tools feature: CrestApps.Core's WebMCP tool service and endpoints, with the
/// tool selection from the site settings and the <see cref="WebMcpPermissionsProvider.UseWebMcpTools"/> permission.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAIWebMcp();

        // Only users with the permission may use the endpoints, instead of every signed-in user.
        services.Replace(ServiceDescriptor.Scoped<IWebMcpAccessEvaluator, PermissionWebMcpAccessEvaluator>());

        services
            .AddPermissionProvider<WebMcpPermissionsProvider>()
            .AddTransient<IConfigureOptions<WebMcpOptions>, WebMcpOptionsConfiguration>()
            .AddTransient<IConfigureOptions<WebMcpPageOptions>, WebMcpOptionsConfiguration>()
            .AddResourceConfiguration<ResourceManagementOptionsConfiguration>();

        services
            .AddSiteDisplayDriver<WebMcpSettingsDisplayDriver>()
            .AddNavigationProvider<AISiteSettingsAdminMenu>();
        services.AddSiteSettingsPermission(AIConstants.AISettingsGroupId, WebMcpPermissionsProvider.ManageWebMcpSettings);

        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<WebMcpScriptFilter>();
        });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapWebMcpTools();
    }
}

/// <summary>
/// Registers the services of the WebMCP Page Tools feature, which adds CrestApps.Core's page tools script.
/// </summary>
[Feature(WebMcpConstants.Feature.PageTools)]
public sealed class PageToolsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWebMcpClientConfigurationHandler, WebMcpPageToolsClientConfigurationHandler>();
    }
}

/// <summary>
/// Registers the services of the WebMCP Chat Bridge feature: CrestApps.Core's page tools in the AI chat, run in the
/// browser through the AI chat hub, and the profile options that allow them.
/// </summary>
[Feature(WebMcpConstants.Feature.Chat)]
public sealed class ChatStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatStartup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration.</param>
    public ChatStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAIWebMcpPageTools<AIChatHub>();
        services.Configure<WebMcpPageToolOptions>(_shellConfiguration.GetSection($"{WebMcpConstants.ConfigurationSection}:Chat"));

        services.AddDisplayDriver<AIProfile, AIProfileWebMcpDisplayDriver>();
    }
}
