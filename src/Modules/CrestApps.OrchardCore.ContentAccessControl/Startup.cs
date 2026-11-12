using CrestApps.OrchardCore.ContentAccessControl.Drivers;
using CrestApps.OrchardCore.ContentAccessControl.Feeds;
using CrestApps.OrchardCore.ContentAccessControl.Handlers;
using CrestApps.OrchardCore.ContentAccessControl.Indexing;
using CrestApps.OrchardCore.ContentAccessControl.Schemas;
using CrestApps.OrchardCore.ContentAccessControl.Search;
using CrestApps.OrchardCore.ContentAccessControl.Sitemaps;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Feeds;
using OrchardCore.Indexing;
using OrchardCore.Modules;
using OrchardCore.Search.Abstractions;
using OrchardCore.Sitemaps.Builders;

namespace CrestApps.OrchardCore.ContentAccessControl;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContentTypePartDefinitionDisplayDriver, RolePickerPartContentAccessControlSettingsDisplayDriver>()
            .AddScoped<IAuthorizationHandler, RoleBasedContentItemAuthorizationHandler>();
    }
}

/// <summary>
/// Leaves restricted content items out of the sitemaps.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Sitemaps)]
public sealed class SitemapsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISitemapContentItemExtendedMetadataProvider, RestrictedContentSitemapFilter>();
    }
}

/// <summary>
/// Adds the roles of a restricted content item to its search document.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Indexing)]
public sealed class IndexingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDocumentIndexHandler, ContentAccessIndexHandler>();
    }
}

/// <summary>
/// Takes restricted content items out of RSS feeds for readers outside their roles. It wraps the feed item builder the
/// Contents feature registers, so it runs after the other startups.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Feeds)]
public sealed class FeedsStartup : StartupBase
{
    /// <inheritdoc/>
    public override int Order => ContentAccessDecorators.Order;

    public override void ConfigureServices(IServiceCollection services)
    {
        ContentAccessDecorators.Decorate<IFeedItemBuilder>(services, (serviceProvider, inner) => new RestrictedContentFeedItemBuilder(
            inner,
            serviceProvider.GetRequiredService<IAuthorizationService>(),
            serviceProvider.GetRequiredService<IHttpContextAccessor>()));
    }
}

/// <summary>
/// Takes restricted content items out of search results for searchers outside their roles. It wraps every search
/// provider's search service (Lucene, Elasticsearch, Azure AI Search), so it runs after the other startups.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Search)]
public sealed class SearchStartup : StartupBase
{
    /// <inheritdoc/>
    public override int Order => ContentAccessDecorators.Order;

    public override void ConfigureServices(IServiceCollection services)
    {
        ContentAccessDecorators.Decorate<ISearchService>(services, (serviceProvider, inner) => new RestrictedContentSearchService(
            inner,
            serviceProvider.GetRequiredService<IContentManager>(),
            serviceProvider.GetRequiredService<IAuthorizationService>(),
            serviceProvider.GetRequiredService<IHttpContextAccessor>()));
    }
}

/// <summary>
/// Registers recipe schema contributors for Content Access Control.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, RolePickerPartContentAccessControlSchemaDefinition>();
    }
}

/// <summary>
/// Wraps services other features registered, keyed ones included, keeping each one's lifetime.
/// </summary>
internal static class ContentAccessDecorators
{
    /// <summary>
    /// The order of the startups that decorate: after every startup that registers what they wrap.
    /// </summary>
    public const int Order = 10_000;

    public static void Decorate<TService>(IServiceCollection services, Func<IServiceProvider, TService, TService> decorate)
        where TService : class
    {
        foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(TService)).ToList())
        {
            var index = services.IndexOf(descriptor);

            services[index] = descriptor.IsKeyedService
                ? new ServiceDescriptor(typeof(TService), descriptor.ServiceKey, (serviceProvider, key) => decorate(serviceProvider, (TService)CreateKeyed(serviceProvider, descriptor, key)), descriptor.Lifetime)
                : new ServiceDescriptor(typeof(TService), serviceProvider => decorate(serviceProvider, (TService)Create(serviceProvider, descriptor)), descriptor.Lifetime);
        }
    }

    private static object Create(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
        => descriptor.ImplementationInstance
            ?? descriptor.ImplementationFactory?.Invoke(serviceProvider)
            ?? ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType);

    private static object CreateKeyed(IServiceProvider serviceProvider, ServiceDescriptor descriptor, object key)
        => descriptor.KeyedImplementationInstance
            ?? descriptor.KeyedImplementationFactory?.Invoke(serviceProvider, key)
            ?? ActivatorUtilities.CreateInstance(serviceProvider, descriptor.KeyedImplementationType);
}
