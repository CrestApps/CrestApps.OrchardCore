using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Azure.Drivers;
using CrestApps.OrchardCore.OpenAI.Azure.Handlers;
using CrestApps.OrchardCore.OpenAI.Azure.Migrations;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI.Azure;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<AzureOpenAIOwnDataAIProfilesMigrations>();
        services.AddDataMigration<AzureOpenAIFeatureMigrations>();
        services.AddSingleton<IODataValidator, ODataFilterValidator>();

        services
            .AddScoped<IAIClientProvider, AzureOpenAIClientProvider>()
            .AddScoped<IOpenAIChatOptionsConfiguration, AzurePatchOpenAIDataSourceHandler>()
            .AddAIDeploymentProvider(AzureOpenAIConstants.ProviderName, o =>
            {
                o.DisplayName = S["Azure OpenAI"];
                o.Description = S["Azure OpenAI model deployments."];
            });
    }
}

[Feature(AzureOpenAIConstants.Feature.Standard)]
public sealed class StandardStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public StandardStartup(IStringLocalizer<StandardStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIProfile<AzureOpenAICompletionClient>(AzureOpenAIConstants.ProviderName, AzureOpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = S["Azure OpenAI"];
            o.Description = S["Provides AI profiles using Azure OpenAI models."];
        });
    }
}

[RequireFeatures(AIConstants.Feature.DataSources)]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<AzureOpenAIOwnDataAIDataSourceMigrations>();
        services.AddDataMigration<AzureOpenAIDataSourceMetadataMigrations>();
    }
}

[RequireFeatures(AIConstants.Feature.ConnectionManagement)]
public sealed class ConnectionManagementStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ConnectionManagementStartup(IStringLocalizer<ConnectionManagementStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICatalogEntryHandler<AIProviderConnection>, AzureOpenAIConnectionSettingsHandler>();
        services.AddTransient<IAIProviderConnectionHandler, AzureOpenAIConnectionHandler>();
        services.AddDisplayDriver<AIProviderConnection, AzureOpenAIConnectionDisplayDriver>();
        services.AddAIConnectionSource(AzureOpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = S["Azure OpenAI"];
            o.Description = S["Provides a way to configure Azure OpenAI connections."];
        });
    }
}
