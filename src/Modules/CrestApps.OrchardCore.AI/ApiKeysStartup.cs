using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.AI.Drivers;
using CrestApps.OrchardCore.AI.Endpoints.Api;
using CrestApps.OrchardCore.AI.Migrations;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Registers API keys for the AI API, and the profiles API, as part of the AI Chat WebAPI feature.
/// </summary>
[Feature(AIConstants.Feature.ChatApi)]
public sealed class ApiKeysStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IAIApiKeyStore, AIApiKeyStore>()
            .AddIndexProvider<AIApiKeyIndexProvider>()
            .AddDataMigration<AIApiKeyIndexMigrations>()
            .AddDisplayDriver<AIApiKey, AIApiKeyDisplayDriver>()
            .AddNavigationProvider<AIApiKeysAdminMenu>()
            .AddPermissionProvider<AIApiKeysPermissionsProvider>();

        services.AddScoped<IAuthorizationHandler, AIApiKeyProfileAuthorizationHandler>();
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, AIApiKeyAuthenticationHandler>(AIApiKeys.Scheme, null);
    }

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapAIProfilesApi();
}
