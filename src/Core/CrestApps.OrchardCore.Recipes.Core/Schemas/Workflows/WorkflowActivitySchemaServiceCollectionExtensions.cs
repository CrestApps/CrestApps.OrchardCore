using CrestApps.OrchardCore.Recipes.Core;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering workflow activity recipe schema definitions.
/// </summary>
public static class WorkflowActivitySchemaServiceCollectionExtensions
{
    /// <summary>
    /// Registers a workflow activity schema definition so the <c>WorkflowType</c> recipe step can describe
    /// the activity's properties, category and outcomes.
    /// </summary>
    /// <typeparam name="TDefinition">The schema definition type.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddWorkflowActivitySchema<TDefinition>(this IServiceCollection services)
        where TDefinition : class, IWorkflowActivitySchemaDefinition
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddScoped<IWorkflowActivitySchemaDefinition, TDefinition>();
    }
}
