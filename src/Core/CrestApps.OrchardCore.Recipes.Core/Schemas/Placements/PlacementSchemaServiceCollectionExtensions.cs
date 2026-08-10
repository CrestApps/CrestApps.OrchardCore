using CrestApps.OrchardCore.Recipes.Core;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering placement node filter recipe schema definitions.
/// </summary>
public static class PlacementSchemaServiceCollectionExtensions
{
    /// <summary>
    /// Registers a placement node filter schema definition so the <c>Placements</c> recipe step can describe
    /// the filter's value.
    /// </summary>
    /// <typeparam name="TDefinition">The schema definition type.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddPlacementNodeFilterSchema<TDefinition>(this IServiceCollection services)
        where TDefinition : class, IPlacementNodeFilterSchemaDefinition
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddScoped<IPlacementNodeFilterSchemaDefinition, TDefinition>();
    }
}
