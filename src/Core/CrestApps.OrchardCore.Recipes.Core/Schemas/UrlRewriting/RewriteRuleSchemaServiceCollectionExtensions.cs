using CrestApps.OrchardCore.Recipes.Core;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering rewrite rule source recipe schema definitions.
/// </summary>
public static class RewriteRuleSchemaServiceCollectionExtensions
{
    /// <summary>
    /// Registers a rewrite rule source schema definition so the <c>UrlRewriting</c> recipe step can describe
    /// the source's members.
    /// </summary>
    /// <typeparam name="TDefinition">The schema definition type.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddRewriteRuleSourceSchema<TDefinition>(this IServiceCollection services)
        where TDefinition : class, IRewriteRuleSourceSchemaDefinition
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddScoped<IRewriteRuleSourceSchemaDefinition, TDefinition>();
    }
}
