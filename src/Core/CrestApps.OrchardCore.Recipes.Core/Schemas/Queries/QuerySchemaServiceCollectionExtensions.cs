using CrestApps.OrchardCore.Recipes.Core;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering query source recipe schema definitions.
/// </summary>
public static class QuerySchemaServiceCollectionExtensions
{
    /// <summary>
    /// Registers a query source schema definition so the <c>Queries</c> recipe step can describe the
    /// source's members.
    /// </summary>
    /// <typeparam name="TDefinition">The schema definition type.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddQuerySourceSchema<TDefinition>(this IServiceCollection services)
        where TDefinition : class, IQuerySourceSchemaDefinition
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddScoped<IQuerySourceSchemaDefinition, TDefinition>();
    }
}
