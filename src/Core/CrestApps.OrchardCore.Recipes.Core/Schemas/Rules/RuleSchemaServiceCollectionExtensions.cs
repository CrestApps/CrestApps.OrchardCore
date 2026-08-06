using CrestApps.OrchardCore.Recipes.Core;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering rule condition and operator recipe schema definitions.
/// </summary>
public static class RuleSchemaServiceCollectionExtensions
{
    /// <summary>
    /// Registers a rule condition schema definition so the <c>Layers</c> recipe step can describe the
    /// condition's members.
    /// </summary>
    /// <typeparam name="TDefinition">The schema definition type.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddRuleConditionSchema<TDefinition>(this IServiceCollection services)
        where TDefinition : class, IRuleConditionSchemaDefinition
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddScoped<IRuleConditionSchemaDefinition, TDefinition>();
    }

    /// <summary>
    /// Registers a rule condition operator schema definition so the <c>Layers</c> recipe step can describe the
    /// operator's members.
    /// </summary>
    /// <typeparam name="TDefinition">The schema definition type.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddRuleConditionOperatorSchema<TDefinition>(this IServiceCollection services)
        where TDefinition : class, IRuleConditionOperatorSchemaDefinition
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddScoped<IRuleConditionOperatorSchemaDefinition, TDefinition>();
    }
}
