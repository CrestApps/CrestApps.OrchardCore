using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;

namespace CrestApps.OrchardCore.ServiceLevels.Recipes;

/// <summary>
/// Imports the service level policies a recipe carries, creating the ones that don't exist and updating the others. A
/// policy is matched by its id, then by its scope and name, so running a recipe twice never duplicates one.
/// </summary>
internal sealed class ServiceLevelPoliciesRecipeStep : CatalogRecipeStep<ServiceLevelPolicy>
{
    /// <summary>
    /// The name of the step member that holds the policies.
    /// </summary>
    public const string EntriesName = "Policies";

    private readonly IServiceLevelPolicyManager _manager;

    public ServiceLevelPoliciesRecipeStep(IServiceLevelPolicyManager manager)
        : base(ServiceLevelsConstants.RecipeStepName, EntriesName, manager)
    {
        _manager = manager;
    }

    protected override async ValueTask<ServiceLevelPolicy> FindExistingAsync(string id, JsonObject data)
    {
        var policy = await base.FindExistingAsync(id, data);

        if (policy is not null)
        {
            return policy;
        }

        var scope = Text(data, nameof(ServiceLevelPolicy.Scope));
        var name = Text(data, nameof(ServiceLevelPolicy.Name));

        return string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(name)
            ? null
            : await _manager.FindByNameAsync(scope, name);
    }

    private static string Text(JsonObject data, string member)
        => data[member] is JsonValue value && value.TryGetValue<string>(out var text) ? text?.Trim() : null;
}
