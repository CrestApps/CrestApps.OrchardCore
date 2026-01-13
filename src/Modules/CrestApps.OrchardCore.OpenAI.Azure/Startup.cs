using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Azure.Core.MongoDb;
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
        services.AddAIProfile<AzureOpenAIDataSourceCompletionClient>(AzureOpenAIConstants.ProviderName, AzureOpenAIConstants.ProviderName, o =>
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

#region Data Sources Features

[Feature(AzureOpenAIConstants.Feature.AISearch)]
public sealed class AISearchStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public AISearchStartup(IStringLocalizer<AISearchStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIDataSource, AzureOpenAISearchADataSourceDisplayDriver>();
        services.AddScoped<ICatalogEntryHandler<AIDataSource>, AzureAISearchAIDataSourceHandler>();

        services
            .AddScoped<AzureAISearchOpenAIChatOptionsConfiguration>()
            .AddScoped<IOpenAIChatOptionsConfiguration>(sp => sp.GetRequiredService<AzureAISearchOpenAIChatOptionsConfiguration>())
            .AddScoped<IAzureOpenAIDataSourceHandler>(sp => sp.GetRequiredService<AzureAISearchOpenAIChatOptionsConfiguration>())
            .AddAIDataSource(AzureOpenAIConstants.ProviderName, AzureOpenAIConstants.DataSourceTypes.AzureAISearch, o =>
            {
                o.DisplayName = S["Azure OpenAI with Azure AI Search"];
                o.Description = S["Enables AI models to use Azure AI Search as a data source for your data."];
            });
    }
}

[Feature(AzureOpenAIConstants.Feature.Elasticsearch)]
public sealed class ElasticsearchStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public ElasticsearchStartup(IStringLocalizer<ElasticsearchStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIDataSource, AzureOpenAIElasticsearchDataSourceDisplayDriver>();
        services.AddScoped<ICatalogEntryHandler<AIDataSource>, ElasticsearchAIDataSourceHandler>();

        services
            .AddScoped<ElasticsearchOpenAIChatOptionsConfiguration>()
            .AddScoped<IOpenAIChatOptionsConfiguration>(sp => sp.GetRequiredService<ElasticsearchOpenAIChatOptionsConfiguration>())
            .AddScoped<IAzureOpenAIDataSourceHandler>(sp => sp.GetRequiredService<ElasticsearchOpenAIChatOptionsConfiguration>())
            .AddAIDataSource(AzureOpenAIConstants.ProviderName, AzureOpenAIConstants.DataSourceTypes.Elasticsearch, o =>
            {
                o.DisplayName = S["Azure OpenAI with Elasticsearch"];
                o.Description = S["Enables AI models to use Elasticsearch as a data source for your data."];
            });
    }
}

[Feature(AzureOpenAIConstants.Feature.MongoDB)]
public sealed class MongoDBStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public MongoDBStartup(IStringLocalizer<MongoDBStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIDataSource, AzureOpenAIMongoDBDataSourceDisplayDriver>();
        services.AddScoped<ICatalogEntryHandler<AIDataSource>, MongoDBAIProfileHandler>();

        services
            .AddScoped<MongoDBOpenAIChatOptionsConfiguration>()
            .AddScoped<IOpenAIChatOptionsConfiguration>(sp => sp.GetRequiredService<MongoDBOpenAIChatOptionsConfiguration>())
            .AddScoped<IAzureOpenAIDataSourceHandler>(sp => sp.GetRequiredService<MongoDBOpenAIChatOptionsConfiguration>())
            .AddAIDataSource(AzureOpenAIConstants.ProviderName, AzureOpenAIConstants.DataSourceTypes.MongoDB, o =>
            {
                o.DisplayName = S["Azure OpenAI with Mongo DB"];
                o.Description = S["Enables AI models to use Mongo DB as a data source for your data."];
            });
    }
}
#endregion
