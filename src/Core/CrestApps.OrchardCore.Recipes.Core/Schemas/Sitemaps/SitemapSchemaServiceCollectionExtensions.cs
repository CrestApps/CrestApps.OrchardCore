using CrestApps.OrchardCore.Recipes.Core;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering sitemap source recipe schema definitions.
/// </summary>
public static class SitemapSchemaServiceCollectionExtensions
{
    /// <summary>
    /// Registers a sitemap source schema definition so the <c>Sitemaps</c> recipe step can describe the
    /// source's members.
    /// </summary>
    /// <typeparam name="TDefinition">The schema definition type.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddSitemapSourceSchema<TDefinition>(this IServiceCollection services)
        where TDefinition : class, ISitemapSourceSchemaDefinition
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddScoped<ISitemapSourceSchemaDefinition, TDefinition>();
    }
}
