using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch.Handlers;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Azure.Drivers;
using CrestApps.OrchardCore.OpenAI.Azure.Handlers;
using CrestApps.OrchardCore.OpenAI.Azure.Migrations;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
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
        services
            .AddScoped<IAIClientProvider, AzureOpenAIClientProvider>()
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
        services.AddAIProfile<AzureOpenAICompletionClient>(AzureOpenAIConstants.StandardImplementationName, AzureOpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = S["Azure OpenAI"];
            o.Description = S["Provides AI profiles using Azure OpenAI models."];
        });
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
        services.AddScoped<IModelHandler<AIProviderConnection>, AzureOpenAIConnectionSettingsHandler>();
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

[Feature(AzureOpenAIConstants.Feature.DataSources)]
public sealed class DataSourcesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public DataSourcesStartup(IStringLocalizer<DataSourcesStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIProfile<AzureOpenAIDataSourceCompletionClient>(AzureOpenAIConstants.AzureOpenAIOwnData, AzureOpenAIConstants.ProviderName, o =>
        {
            o.DisplayName = S["Azure OpenAI with Your Data"];
            o.Description = S["Provides AI profiles using Azure OpenAI models with your data."];
        });
    }
}

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
        services.AddScoped<IModelHandler<AIDataSource>, AzureAISearchAIDataSourceHandler>();

#pragma warning disable CS0618 // Type or member is obsolete
        services.AddDataMigration<DataSourceMigrations>();
#pragma warning restore CS0618 // Type or member is obsolete

        services
            .AddScoped<IAzureOpenAIDataSourceHandler, AzureAISearchOpenAIDataSourceHandler>()
            .AddAIDataSource(AzureOpenAIConstants.AzureOpenAIOwnData, AzureOpenAIConstants.DataSourceTypes.AzureAISearch, o =>
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
        services.AddScoped<IModelHandler<AIDataSource>, ElasticsearchAIDataSourceHandler>();
        services.AddTransient<IConfigureOptions<ElasticsearchServerOptions>, ElasticsearchServerOptionsConfigurations>();

        services
            .AddScoped<IAzureOpenAIDataSourceHandler, ElasticsearchOpenAIDataSourceHandler>()
            .AddAIDataSource(AzureOpenAIConstants.AzureOpenAIOwnData, AzureOpenAIConstants.DataSourceTypes.Elasticsearch, o =>
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
        services.AddScoped<IModelHandler<AIDataSource>, MongoDbAIProfileHandler>();

        services
            .AddScoped<IAzureOpenAIDataSourceHandler, MongoDBOpenAIDataSourceHandler>()
            .AddAIDataSource(AzureOpenAIConstants.AzureOpenAIOwnData, AzureOpenAIConstants.DataSourceTypes.MongoDB, o =>
            {
                o.DisplayName = S["Azure OpenAI with Mongo DB"];
                o.Description = S["Enables AI models to use Mongo DB as a data source for your data."];
            });
    }
}
#endregion
