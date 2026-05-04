using CrestApps.Core.AI;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.OpenAI;
using CrestApps.Core.AI.OpenAI.Services;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.OpenAI.Drivers;
using CrestApps.OrchardCore.OpenAI.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient<IAIProviderConnectionHandler, OpenAIProviderConnectionHandler>());
        services.AddScoped<IAIClientProvider, OpenAIClientProvider>();
        services.AddCoreAIProfile<ProviderAICompletionClient<OpenAIClientMarker>>(OpenAIConstants.ClientName, o =>
        {
            o.DisplayName = S["OpenAI"];
            o.Description = S["Provides AI profiles using OpenAI."];
        });

        services.AddCoreAIDeploymentProvider(OpenAIConstants.ClientName, o =>
        {
            o.DisplayName = S["OpenAI"];
            o.Description = S["OpenAI model deployments."];
        });
    }
}

/// <summary>
/// Registers services and configuration for the ConnectionManagement feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.ConnectionManagement)]
public sealed class ConnectionManagementStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionManagementStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ConnectionManagementStartup(IStringLocalizer<ConnectionManagementStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICatalogEntryHandler<AIProviderConnection>, OpenAIProviderConnectionSettingsHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IAIProviderConnectionHandler, OpenAIProviderConnectionHandler>());
        services.AddDisplayDriver<AIProviderConnection, OpenAIProviderConnectionDisplayDriver>();
        services.AddCoreAIConnectionSource(OpenAIConstants.ClientName, o =>
        {
            o.DisplayName = S["OpenAI"];
            o.Description = S["Provides a way to configure OpenAI-compatible connection for any provider."];
        });
    }
}
