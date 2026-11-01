using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.AI.WebMcp.Drivers;
using CrestApps.OrchardCore.AI.WebMcp.Endpoints;
using CrestApps.OrchardCore.AI.WebMcp.Filters;
using CrestApps.OrchardCore.AI.WebMcp.Hubs;
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
/// Registers the services of the WebMCP Tools feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddPermissionProvider<WebMcpPermissionsProvider>()
            .AddTransient<IConfigureOptions<WebMcpOptions>, WebMcpOptionsConfiguration>()
            .AddScoped<IWebMcpToolService, DefaultWebMcpToolService>()
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
        routes.AddWebMcpToolEndpoints();
    }
}

/// <summary>
/// Registers the services of the WebMCP Page Tools feature.
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
/// Registers the services of the WebMCP Chat Bridge feature.
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
        services.Configure<WebMcpChatOptions>(_shellConfiguration.GetSection($"{WebMcpConstants.ConfigurationSection}:Chat"));

        services
            .AddSingleton<IWebMcpBrowserToolRegistry, DefaultWebMcpBrowserToolRegistry>()
            .AddSingleton<IWebMcpBrowserClient, WebMcpHubBrowserClient>()
            .AddSingleton<IWebMcpBrowserToolInvoker, DefaultWebMcpBrowserToolInvoker>()
            .AddScoped<IWebMcpClientConfigurationHandler, WebMcpChatClientConfigurationHandler>()
            .AddDisplayDriver<AIProfile, AIProfileWebMcpDisplayDriver>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IToolRegistryProvider, WebMcpBrowserToolRegistryProvider>());
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapHub<WebMcpHub>(SignalRHubRoutes.GetHubPath<WebMcpHub>());
    }
}
