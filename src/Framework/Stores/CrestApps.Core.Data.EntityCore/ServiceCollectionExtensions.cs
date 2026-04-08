using CrestApps.Core.AI;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Data.EntityCore.Services;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.Core.Data.EntityCore;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEntityCoreDataStore(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configure,
        Action<EntityCoreDataStoreOptions> configureStore = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<EntityCoreDataStoreOptions>();

        if (configureStore is not null)
        {
            services.Configure(configureStore);
        }

        services.AddDbContext<CrestAppsEntityDbContext>(configure);

        return services;
    }

    public static IServiceCollection AddEntityCoreSqliteDataStore(
        this IServiceCollection services,
        string connectionString,
        string tablePrefix = "CA_")
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        return services.AddEntityCoreDataStore(
            options => options.UseSqlite(connectionString),
            store => store.TablePrefix = tablePrefix);
    }

    public static IServiceCollection AddDocumentCatalogs(this IServiceCollection services)
    {
        services.TryAddScoped(typeof(ICatalog<>), typeof(DocumentCatalog<>));
        services.TryAddScoped(typeof(INamedCatalog<>), typeof(NamedDocumentCatalog<>));
        services.TryAddScoped(typeof(ISourceCatalog<>), typeof(SourceDocumentCatalog<>));
        services.TryAddScoped(typeof(INamedSourceCatalog<>), typeof(NamedSourceDocumentCatalog<>));

        return services;
    }

    public static IServiceCollection AddDocumentCatalog<TModel>(this IServiceCollection services)
        where TModel : CatalogItem
    {
        services.RemoveAll<ICatalog<TModel>>();
        services.AddScoped<ICatalog<TModel>, DocumentCatalog<TModel>>();

        return services;
    }

    public static IServiceCollection AddNamedDocumentCatalog<TModel>(this IServiceCollection services)
        where TModel : CatalogItem, INameAwareModel
    {
        services.RemoveAll<ICatalog<TModel>>();
        services.RemoveAll<INamedCatalog<TModel>>();

        services.AddScoped<ICatalog<TModel>, NamedDocumentCatalog<TModel>>();
        services.AddScoped<INamedCatalog<TModel>>(sp => (INamedCatalog<TModel>)sp.GetRequiredService<ICatalog<TModel>>());

        return services;
    }

    public static IServiceCollection AddSourceDocumentCatalog<TModel>(this IServiceCollection services)
        where TModel : CatalogItem, ISourceAwareModel
    {
        services.RemoveAll<ICatalog<TModel>>();
        services.RemoveAll<ISourceCatalog<TModel>>();

        services.AddScoped<ICatalog<TModel>, SourceDocumentCatalog<TModel>>();
        services.AddScoped<ISourceCatalog<TModel>>(sp => (ISourceCatalog<TModel>)sp.GetRequiredService<ICatalog<TModel>>());

        return services;
    }

    public static IServiceCollection AddNamedSourceDocumentCatalog<TModel>(this IServiceCollection services)
        where TModel : CatalogItem, INameAwareModel, ISourceAwareModel
    {
        services.RemoveAll<ICatalog<TModel>>();
        services.RemoveAll<INamedCatalog<TModel>>();
        services.RemoveAll<ISourceCatalog<TModel>>();
        services.RemoveAll<INamedSourceCatalog<TModel>>();

        services.AddScoped<ICatalog<TModel>, NamedSourceDocumentCatalog<TModel>>();
        services.AddScoped<INamedCatalog<TModel>>(sp => (INamedCatalog<TModel>)sp.GetRequiredService<ICatalog<TModel>>());
        services.AddScoped<ISourceCatalog<TModel>>(sp => (ISourceCatalog<TModel>)sp.GetRequiredService<ICatalog<TModel>>());
        services.AddScoped<INamedSourceCatalog<TModel>>(sp => (INamedSourceCatalog<TModel>)sp.GetRequiredService<ICatalog<TModel>>());

        return services;
    }

    public static IServiceCollection AddEntityCoreCoreStores(this IServiceCollection services)
    {
        services
            .AddNamedSourceDocumentCatalog<AIProfile>()
            .AddNamedSourceDocumentCatalog<AIProviderConnection>()
            .AddDocumentCatalog<A2AConnection>()
            .AddSourceDocumentCatalog<McpConnection>()
            .AddNamedDocumentCatalog<McpPrompt>()
            .AddSourceDocumentCatalog<McpResource>()
            .AddNamedSourceDocumentCatalog<AIProfileTemplate>()
            .AddDocumentCatalog<ChatInteraction>()
            .AddScoped<IAIChatSessionManager, EntityCoreAIChatSessionManager>()
            .AddScoped<IAIChatSessionPromptStore, EntityCoreAIChatSessionPromptStore>()
            .AddScoped<ICatalog<AIChatSessionPrompt>>(sp => sp.GetRequiredService<IAIChatSessionPromptStore>())
            .AddScoped<IAIDocumentStore, EntityCoreAIDocumentStore>()
            .AddScoped<ICatalog<AIDocument>>(sp => sp.GetRequiredService<IAIDocumentStore>())
            .AddScoped<IAIDocumentChunkStore, EntityCoreAIDocumentChunkStore>()
            .AddScoped<ICatalog<AIDocumentChunk>>(sp => sp.GetRequiredService<IAIDocumentChunkStore>())
            .AddScoped<ISearchIndexProfileStore, EntityCoreSearchIndexProfileStore>()
            .AddScoped<ICatalog<SearchIndexProfile>>(sp => sp.GetRequiredService<ISearchIndexProfileStore>())
            .AddScoped<INamedCatalog<SearchIndexProfile>>(sp => (INamedCatalog<SearchIndexProfile>)sp.GetRequiredService<ISearchIndexProfileStore>())
            .AddScoped<IAIDataSourceStore, EntityCoreAIDataSourceStore>()
            .AddScoped<ICatalog<AIDataSource>>(sp => sp.GetRequiredService<IAIDataSourceStore>())
            .AddScoped<IAIMemoryStore, EntityCoreAIMemoryStore>()
            .AddScoped<ICatalog<AIMemoryEntry>>(sp => sp.GetRequiredService<IAIMemoryStore>())
            .AddScoped<IChatInteractionPromptStore, EntityCoreChatInteractionPromptStore>()
            .AddScoped<ICatalog<ChatInteractionPrompt>>(sp => sp.GetRequiredService<IChatInteractionPromptStore>())
            .AddScoped<IAIDeploymentStore, EntityCoreAIDeploymentStore>()
            .AddScoped<ICatalog<AIDeployment>>(sp => sp.GetRequiredService<IAIDeploymentStore>())
            .AddScoped<INamedCatalog<AIDeployment>>(sp => sp.GetRequiredService<IAIDeploymentStore>())
            .AddScoped<ISourceCatalog<AIDeployment>>(sp => sp.GetRequiredService<IAIDeploymentStore>())
            .AddScoped<INamedSourceCatalog<AIDeployment>>(sp => sp.GetRequiredService<IAIDeploymentStore>());

        return services;
    }

    public static async Task InitializeEntityCoreSchemaAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CrestAppsEntityDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
