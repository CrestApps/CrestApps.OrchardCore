using CrestApps.OrchardCore.Recipes.Core;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering deployment step recipe schema definitions.
/// </summary>
public static class DeploymentSchemaServiceCollectionExtensions
{
    /// <summary>
    /// Registers a deployment step schema definition so the <c>deployment</c> recipe step can describe the
    /// step's <c>Step</c> payload.
    /// </summary>
    /// <typeparam name="TDefinition">The schema definition type.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddDeploymentStepSchema<TDefinition>(this IServiceCollection services)
        where TDefinition : class, IDeploymentStepSchemaDefinition
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddScoped<IDeploymentStepSchemaDefinition, TDefinition>();
    }
}
