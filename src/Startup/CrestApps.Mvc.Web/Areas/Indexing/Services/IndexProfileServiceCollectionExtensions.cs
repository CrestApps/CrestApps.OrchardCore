using CrestApps.AI.Indexing;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Mvc.Web.Areas.Admin.Handlers;
using CrestApps.Mvc.Web.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CrestApps.Mvc.Web.Services;

public static class IndexProfileServiceCollectionExtensions
{
    public static IServiceCollection AddIndexProfileHandler<THandler>(this IServiceCollection services)
        where THandler : class, IIndexProfileHandler
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IIndexProfileHandler, THandler>());

        return services;
    }

    public static IServiceCollection AddElasticsearchDataSource(this IServiceCollection services, string type, Action<IndexProfileSourceDescriptor> configure = null)
        => services
            .AddIndexProfileHandler<DefaultSearchIndexProfileHandler>()
            .AddIndexingSource("Elasticsearch", "Elasticsearch", type, configure);

    public static IServiceCollection AddAzureAISearchDataSource(this IServiceCollection services, string type, Action<IndexProfileSourceDescriptor> configure = null)
        => services
            .AddIndexProfileHandler<DefaultSearchIndexProfileHandler>()
            .AddIndexingSource("AzureAISearch", "Azure AI Search", type, configure);

    public static IServiceCollection AddElasticsearchAIDocumentSource(this IServiceCollection services)
        => services
            .AddElasticsearchDataSource(IndexProfileTypes.AIDocuments, o =>
            {
                o.DisplayName = "AI Documents";
                o.Description = "Create an Elasticsearch index for uploaded and embedded AI document chunks.";
            })
            .AddIndexProfileHandler<AIDocumentSearchIndexProfileHandler>();

    public static IServiceCollection AddElasticsearchAIDataSource(this IServiceCollection services)
        => services
            .AddElasticsearchDataSource(IndexProfileTypes.DataSource, o =>
            {
                o.DisplayName = "Data Source";
                o.Description = "Create an Elasticsearch index for AI knowledge base data source documents.";
            })
            .AddIndexProfileHandler<DataSourceSearchIndexProfileHandler>();

    public static IServiceCollection AddElasticsearchAIMemorySource(this IServiceCollection services)
        => services
            .AddElasticsearchDataSource(IndexProfileTypes.AIMemory, o =>
            {
                o.DisplayName = "AI Memory";
                o.Description = "Create an Elasticsearch index for user and system memory records.";
            })
            .AddIndexProfileHandler<AIMemorySearchIndexProfileHandler>();

    public static IServiceCollection AddElasticsearchArticleSource(this IServiceCollection services)
        => services
            .AddElasticsearchDataSource(IndexProfileTypes.Articles, o =>
            {
                o.DisplayName = "Articles";
                o.Description = "Create an Elasticsearch index for sample article records managed in the MVC app.";
            })
            .AddIndexProfileHandler<ArticleIndexProfileHandler>();

    public static IServiceCollection AddAzureAISearchAIDocumentSource(this IServiceCollection services)
        => services
            .AddAzureAISearchDataSource(IndexProfileTypes.AIDocuments, o =>
            {
                o.DisplayName = "AI Documents";
                o.Description = "Create an Azure AI Search index for uploaded and embedded AI document chunks.";
            })
            .AddIndexProfileHandler<AIDocumentSearchIndexProfileHandler>();

    public static IServiceCollection AddAzureAISearchAIDataSource(this IServiceCollection services)
        => services
            .AddAzureAISearchDataSource(IndexProfileTypes.DataSource, o =>
            {
                o.DisplayName = "Data Source";
                o.Description = "Create an Azure AI Search index for AI knowledge base data source documents.";
            })
            .AddIndexProfileHandler<DataSourceSearchIndexProfileHandler>();

    public static IServiceCollection AddAzureAISearchAIMemorySource(this IServiceCollection services)
        => services
            .AddAzureAISearchDataSource(IndexProfileTypes.AIMemory, o =>
            {
                o.DisplayName = "AI Memory";
                o.Description = "Create an Azure AI Search index for user and system memory records.";
            })
            .AddIndexProfileHandler<AIMemorySearchIndexProfileHandler>();

    public static IServiceCollection AddAzureAISearchArticleSource(this IServiceCollection services)
        => services
            .AddAzureAISearchDataSource(IndexProfileTypes.Articles, o =>
            {
                o.DisplayName = "Articles";
                o.Description = "Create an Azure AI Search index for sample article records managed in the MVC app.";
            })
            .AddIndexProfileHandler<ArticleIndexProfileHandler>();

    public static IServiceCollection AddIndexingSource(this IServiceCollection services, string providerName, string providerDisplayName, string type, Action<IndexProfileSourceDescriptor> configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        services.TryAddSingleton<IConfigureOptions<IndexProfileSourceOptions>, ConfigureIndexProfileSourceOptions>();
        services.AddSingleton(new IndexProfileSourceRegistration(providerName, providerDisplayName, type, configure));

        return services;
    }
}

internal sealed class IndexProfileSourceRegistration
{
    public IndexProfileSourceRegistration(string providerName, string providerDisplayName, string type, Action<IndexProfileSourceDescriptor> configure)
    {
        ProviderName = providerName;
        ProviderDisplayName = providerDisplayName;
        Type = type;
        Configure = configure;
    }

    public string ProviderName { get; }

    public string ProviderDisplayName { get; }

    public string Type { get; }

    public Action<IndexProfileSourceDescriptor> Configure { get; }
}

internal sealed class ConfigureIndexProfileSourceOptions : IConfigureOptions<IndexProfileSourceOptions>
{
    private readonly IEnumerable<IndexProfileSourceRegistration> _registrations;

    public ConfigureIndexProfileSourceOptions(IEnumerable<IndexProfileSourceRegistration> registrations)
    {
        _registrations = registrations;
    }

    public void Configure(IndexProfileSourceOptions options)
    {
        foreach (var registration in _registrations)
        {
            if (options.Sources.Any(source =>
            string.Equals(source.ProviderName, registration.ProviderName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(source.Type, registration.Type, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var descriptor = new IndexProfileSourceDescriptor
            {
                ProviderName = registration.ProviderName,
                ProviderDisplayName = registration.ProviderDisplayName,
                Type = registration.Type,
                DisplayName = registration.Type,
                Description = registration.Type,
            };

            registration.Configure?.Invoke(descriptor);
            options.Sources.Add(descriptor);
        }
    }
}
