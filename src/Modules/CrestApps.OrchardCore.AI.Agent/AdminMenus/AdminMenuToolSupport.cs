using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.AdminMenus;

/// <summary>
/// Reads the custom admin menus through the admin menu deployment step, which exports every menu with the service
/// the admin pages use, in the same JSON the <c>AdminMenu</c> recipe step saves.
/// </summary>
internal static class AdminMenuToolSupport
{
    /// <summary>
    /// The name of the deployment step type that exports every admin menu.
    /// </summary>
    public const string DeploymentStepType = "AdminMenuDeploymentStep";

    /// <summary>
    /// The name of the recipe step that creates or replaces admin menus.
    /// </summary>
    public const string RecipeStepName = "AdminMenu";

    /// <summary>
    /// Reads every custom admin menu.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The menus, or <see langword="null"/> when the Admin Menu feature does not provide its export step.</returns>
    public static async Task<List<JsonObject>> ReadMenusAsync(IServiceProvider services)
    {
        var steps = await DeploymentPlanRunner.RunStepAsync(services, DeploymentStepType);

        if (steps is null)
        {
            return null;
        }

        return steps
            .Where(step => string.Equals(step["name"]?.GetValue<string>(), RecipeStepName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(step => step["data"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .ToList();
    }

    /// <summary>
    /// Finds the value of a member of a menu or node, whatever the case of its name.
    /// </summary>
    /// <param name="node">The menu or node.</param>
    /// <param name="name">The member name.</param>
    /// <returns>The value, or <see langword="null"/> when the member is missing.</returns>
    public static JsonNode Get(JsonObject node, string name)
        => node.FirstOrDefault(member => string.Equals(member.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>
    /// Counts the nodes of a node tree.
    /// </summary>
    /// <param name="nodes">The nodes.</param>
    /// <returns>The number of nodes, nested ones included.</returns>
    public static int CountNodes(JsonArray nodes)
    {
        if (nodes is null)
        {
            return 0;
        }

        var count = 0;

        foreach (var node in nodes.OfType<JsonObject>())
        {
            count += 1 + CountNodes(Get(node, "Items") as JsonArray);
        }

        return count;
    }

    /// <summary>
    /// Describes the node types that can be added to an admin menu on this site, from the admin menu schemas.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The node types, or <see langword="null"/> when no schema is available.</returns>
    public static async Task<List<AdminNodeType>> GetNodeTypesAsync(IServiceProvider services)
    {
        var schemaService = services.GetService<IAdminMenuSchemaService>();

        if (schemaService is null)
        {
            return null;
        }

        var descriptors = await schemaService.GetNodeDescriptorsAsync();

        return descriptors
            .Select(descriptor => new AdminNodeType(
                descriptor.Name,
                descriptor.TypeDiscriminator,
                descriptor.DisplayText,
                descriptor.Description,
                descriptor.Properties.ToDictionary(
                    property => property.Name,
                    property => JsonSerializer.SerializeToNode(property.Schema.Build())),
                [.. descriptor.RequiredProperties]))
            .ToList();
    }
}

/// <summary>
/// A node type that can be added to an admin menu.
/// </summary>
/// <param name="Name">The node type name, such as <c>LinkAdminNode</c>.</param>
/// <param name="Type">The value of the <c>$type</c> member of a node of this type.</param>
/// <param name="DisplayText">The title of the node type in the menu editor.</param>
/// <param name="Description">What the node adds to the menu.</param>
/// <param name="Properties">The schema of each field of the node type.</param>
/// <param name="Required">The fields a node of this type must have.</param>
internal sealed record AdminNodeType(string Name, string Type, string DisplayText, string Description, Dictionary<string, JsonNode> Properties, List<string> Required);
