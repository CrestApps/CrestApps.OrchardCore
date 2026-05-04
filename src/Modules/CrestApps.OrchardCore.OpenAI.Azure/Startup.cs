using CrestApps.Core.AI;
using CrestApps.Core.AI.Clients;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.OpenAI;
using CrestApps.Core.AI.OpenAI.Azure;
using CrestApps.Core.AI.OpenAI.Azure.Handlers;
using CrestApps.Core.AI.OpenAI.Azure.Models;
using CrestApps.Core.AI.OpenAI.Azure.Services;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Drivers;
using CrestApps.OrchardCore.OpenAI.Azure.Handlers;
using CrestApps.OrchardCore.OpenAI.Azure.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI.Azure;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="shellConfiguration">The shell configuration.</param>
    public Startup(
        IStringLocalizer<Startup> stringLocalizer,
        IShellConfiguration shellConfiguration)
    {
        S = stringLocalizer;
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<AzureOpenAIFeatureMigrations>();
        services.AddSingleton<IODataValidator, ODataFilterValidator>();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IAIProviderConnectionHandler, AzureOpenAIConnectionHandler>());

        services
            .AddScoped<IAIClientProvider, AzureOpenAIClientProvider>()
            .AddScoped<IAIClientProvider, AzureSpeechClientProvider>()
            .AddScoped<IOpenAIChatOptionsConfiguration, AzurePatchOpenAIDataSourceHandler>()
            .AddCoreAIDeploymentProvider(AzureOpenAIConstants.ClientName, o =>
            {
                o.DisplayName = S["Azure OpenAI"];
                o.Description = S["Azure OpenAI model deployments."];
            })
            .AddCoreAIDeploymentProvider(AzureOpenAIConstants.AzureSpeechClientName, o =>
            {
                o.UseContainedConnection = true;
                o.DisplayName = S["Azure AI Services"];
                o.Description = S["Azure deployment via a service connection."];
            })
            .AddDisplayDriver<AIDeployment, AzureSpeechDeploymentDisplayDriver>();

        services.AddCoreAIProfile<AzureOpenAICompletionClient>(AzureOpenAIConstants.ClientName, o =>
        {
            o.DisplayName = S["Azure OpenAI"];
            o.Description = S["Provides AI profiles using Azure OpenAI models."];
        });

        services.PostConfigure<AzureClientOptions>(options =>
        {
            _shellConfiguration
                .GetSection("CrestApps:AI:AzureClient")
                .Bind(options);
        });
    }
}

/// <summary>
/// Registers services and configuration for the DataSources feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.DataSources)]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<AzureOpenAIOwnDataAIDataSourceMigrations>();
        services.AddDataMigration<AzureOpenAIDataSourceMetadataMigrations>();
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
        services.AddScoped<ICatalogEntryHandler<AIProviderConnection>, AzureOpenAIConnectionSettingsHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IAIProviderConnectionHandler, AzureOpenAIConnectionHandler>());
        services.AddDisplayDriver<AIProviderConnection, AzureOpenAIConnectionDisplayDriver>();
        services.AddCoreAIConnectionSource(AzureOpenAIConstants.ClientName, o =>
        {
            o.DisplayName = S["Azure OpenAI"];
            o.Description = S["Provides a way to configure Azure OpenAI connections."];
        });
    }
}
