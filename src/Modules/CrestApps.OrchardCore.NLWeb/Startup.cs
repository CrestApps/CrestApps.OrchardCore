using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.NLWeb.AI;
using CrestApps.OrchardCore.NLWeb.Drivers;
using CrestApps.OrchardCore.NLWeb.Endpoints;
using CrestApps.OrchardCore.NLWeb.Navigation;
using CrestApps.OrchardCore.NLWeb.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.RateLimits;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.NLWeb;

/// <summary>
/// Registers the NLWeb endpoints, the ask pipeline with its content item resolver, and the settings.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<INLWebAskService, NLWebAskService>()
            .AddScoped<INLWebResultResolver, ContentItemNLWebResultResolver>()
            .AddScoped<INLWebQueryProcessor, NLWebSettingsQueryProcessor>()
            .AddScoped<INLWebAskHandler, NLWebTelemetryAskHandler>();

        services
            .AddPermissionProvider<NLWebPermissionsProvider>()
            .AddSiteDisplayDriver<NLWebSettingsDisplayDriver>()
            .AddNavigationProvider<NLWebAdminMenu>();
        services.AddSiteSettingsPermission(NLWebConstants.SettingsGroupId, NLWebPermissionsProvider.ManageNLWebSettings);
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapNLWebEndpoints();
    }
}

/// <summary>
/// Answers questions with the keyword search of Orchard Core's search providers, which register their search service
/// only when Orchard Core's Search feature is enabled.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Search)]
public sealed class SearchStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<INLWebRetriever, IndexSearchNLWebRetriever>();
    }
}

/// <summary>
/// Uses AI when it is enabled: rewrites follow-up questions, ranks the results, writes summaries and answers, and
/// offers the <c>ask</c> tool to the MCP server, WebMCP and AI profiles.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
public sealed class AIStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIStartup(IStringLocalizer<AIStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<NLWebChatClientFactory>()
            .AddScoped<INLWebQueryProcessor, AIQueryRewriter>()
            .AddScoped<INLWebResultRanker, AIResultRanker>()
            .AddScoped<INLWebResponseGenerator, AIResponseGenerator>();

        services.AddSiteDisplayDriver<NLWebAISettingsDisplayDriver>();

        services.AddCoreAITool<NLWebAskTool>(NLWebConstants.AskToolName)
            .WithTitle(S["Ask the Site (NLWeb)"])
            .WithDescription(S["Asks the site a question in natural language and returns the matching content items as schema.org objects."])
            .WithCategory(S["Content Management"])
            .Selectable();
    }
}

/// <summary>
/// Searches an AI data source's vector index by meaning when AI Data Sources is enabled.
/// </summary>
[RequireFeatures(AIConstants.Feature.DataSources)]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<INLWebRetriever, VectorNLWebRetriever>();
        services.AddSiteDisplayDriver<NLWebDataSourceSettingsDisplayDriver>();
    }
}

/// <summary>
/// Holds each visitor to a number of questions a minute when Orchard Core's Rate Limits feature is enabled: every
/// question runs a search, and, with AI, calls a model the site pays for.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class RateLimitsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options => options.AddGroupRateLimit(
            NLWebConstants.RateLimitGroup,
            RateLimitPartitionHelpers.CreateFixedWindowPerIpPolicy(
                policyName: NLWebConstants.RateLimitGroup,
                permitLimit: NLWebConstants.RequestsPerMinute,
                window: TimeSpan.FromMinutes(1))));
    }
}
