using CrestApps.OrchardCore.Recipes.Core;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering admin menu node recipe schema definitions.
/// </summary>
public static class AdminMenuSchemaServiceCollectionExtensions
{
    /// <summary>
    /// Registers an admin menu node schema definition so the <c>AdminMenu</c> recipe step can describe the
    /// node's members.
    /// </summary>
    /// <typeparam name="TDefinition">The schema definition type.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddAdminNodeSchema<TDefinition>(this IServiceCollection services)
        where TDefinition : class, IAdminNodeSchemaDefinition
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddScoped<IAdminNodeSchemaDefinition, TDefinition>();
    }
}
