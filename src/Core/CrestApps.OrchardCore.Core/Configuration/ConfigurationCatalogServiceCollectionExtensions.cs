using CrestApps.Core.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Provides registration helpers for configuration catalogs.
/// </summary>
public static class ConfigurationCatalogServiceCollectionExtensions
{
    /// <summary>
    /// Registers a configuration catalog backed by the given manager, making the entries scriptable through recipes
    /// and exportable through the group's deployment step.
    /// </summary>
    /// <typeparam name="T">The catalog entry type.</typeparam>
    /// <typeparam name="TManager">The manager that owns the entries.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="group">The identifier of the group the catalog belongs to.</param>
    /// <param name="stepName">The recipe step name that carries the entries.</param>
    /// <param name="collectionName">The name of the property inside the step that holds the array of entries.</param>
    /// <param name="order">The relative import order of the catalog, lowest first.</param>
    /// <param name="identityProperties">
    /// The members that identify an entry when the destination does not know its identifier. Leave this empty for a
    /// catalog whose entries carry a name or a display text; supply it for a catalog whose entries carry neither, or
    /// every replay will create a second copy of configuration the destination already had.
    /// </param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddConfigurationCatalog<T, TManager>(
        this IServiceCollection services,
        string group,
        string stepName,
        string collectionName,
        int order,
        string[] identityProperties = null)
        where T : CatalogItem
        where TManager : ICatalogManager<T>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(group);
        ArgumentException.ThrowIfNullOrEmpty(stepName);
        ArgumentException.ThrowIfNullOrEmpty(collectionName);

        var descriptor = new ConfigurationCatalogDescriptor
        {
            Group = group,
            StepName = stepName,
            CollectionName = collectionName,
            Order = order,
            IdentityProperties = identityProperties,
        };

        services.AddConfigurationImportIdentityStore();

        return services.AddScoped<IConfigurationCatalog>(serviceProvider =>
            new ConfigurationCatalog<T>(
                new CatalogManagerConfigurationCatalogWriter<T>(serviceProvider.GetRequiredService<TManager>()),
                descriptor,
                serviceProvider.GetRequiredService<ConfigurationImportIdentityStore>()));
    }

    /// <summary>
    /// Registers a configuration catalog whose entries are owned by a source-backed manager, making them scriptable
    /// through recipes and exportable through the group's deployment step.
    /// </summary>
    /// <remarks>
    /// A source-backed manager describes the same operations as a plain one through a separate interface, so it needs
    /// its own registration rather than a cast. Everything else about the catalog is identical.
    /// </remarks>
    /// <typeparam name="T">The catalog entry type.</typeparam>
    /// <typeparam name="TManager">The source-backed manager that owns the entries.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="group">The identifier of the group the catalog belongs to.</param>
    /// <param name="stepName">The recipe step name that carries the entries.</param>
    /// <param name="collectionName">The name of the property inside the step that holds the array of entries.</param>
    /// <param name="order">The relative import order of the catalog, lowest first.</param>
    /// <param name="identityProperties">
    /// The members that identify an entry when the destination does not know its identifier. Leave this empty for a
    /// catalog whose entries carry a name or a display text; supply it for a catalog whose entries carry neither, or
    /// every replay will create a second copy of configuration the destination already had.
    /// </param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSourceConfigurationCatalog<T, TManager>(
        this IServiceCollection services,
        string group,
        string stepName,
        string collectionName,
        int order,
        string[] identityProperties = null)
        where T : SourceCatalogEntry
        where TManager : ISourceCatalogManager<T>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(group);
        ArgumentException.ThrowIfNullOrEmpty(stepName);
        ArgumentException.ThrowIfNullOrEmpty(collectionName);

        var descriptor = new ConfigurationCatalogDescriptor
        {
            Group = group,
            StepName = stepName,
            CollectionName = collectionName,
            Order = order,
            IdentityProperties = identityProperties,
        };

        services.AddConfigurationImportIdentityStore();

        return services.AddScoped<IConfigurationCatalog>(serviceProvider =>
            new ConfigurationCatalog<T>(
                new SourceCatalogManagerConfigurationCatalogWriter<T>(serviceProvider.GetRequiredService<TManager>()),
                descriptor,
                serviceProvider.GetRequiredService<ConfigurationImportIdentityStore>()));
    }

    /// <summary>
    /// Registers the recipe step handler that imports every registered configuration catalog.
    /// </summary>
    /// <remarks>
    /// A single handler serves every catalog in the tenant, and more than one module may need it, so the registration
    /// is deduplicated; registering it twice would run each imported step twice.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddConfigurationCatalogRecipeStep(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecipeStepHandler, ConfigurationCatalogRecipeStep>());

        return services;
    }

    private static void AddConfigurationImportIdentityStore(this IServiceCollection services)
    {
        services.TryAddSingleton<ConfigurationImportIdentityStore>();
    }
}
