using CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Composes the admin menu node schemas from the registered <see cref="IAdminNodeSchemaDefinition"/>
/// contributions into the schema used by the <c>AdminMenu</c> recipe step.
/// </summary>
public sealed class AdminMenuSchemaService : IAdminMenuSchemaService
{
    /// <summary>
    /// The maximum nesting depth for which the child <c>Items</c> array is described with the full, per-type
    /// schema. Nodes nested deeper remain valid but are validated with a permissive node schema.
    /// </summary>
    private const int _maxNestingDepth = 3;

    private readonly IEnumerable<IAdminNodeSchemaDefinition> _nodeDefinitions;

    private IReadOnlyList<AdminNodeDescriptor> _cachedNodeDescriptors;
    private JsonSchemaBuilder _cachedNodeSchema;
    private JsonSchemaBuilder _cachedAdminMenuSchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminMenuSchemaService"/> class.
    /// </summary>
    /// <param name="nodeDefinitions">The registered admin menu node schema definitions.</param>
    public AdminMenuSchemaService(IEnumerable<IAdminNodeSchemaDefinition> nodeDefinitions)
    {
        _nodeDefinitions = nodeDefinitions;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AdminNodeDescriptor>> GetNodeDescriptorsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedNodeDescriptors is not null)
        {
            return _cachedNodeDescriptors;
        }

        var definitions = _nodeDefinitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Name))
            .GroupBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var descriptors = new List<AdminNodeDescriptor>();

        foreach (var definition in definitions.Values.OrderBy(definition => definition.Name, StringComparer.Ordinal))
        {
            var context = new AdminNodeSchemaContext
            {
                NodeName = definition.Name,
            };

            var schema = await definition.GetNodeSchemaAsync(context, cancellationToken);

            descriptors.Add(new AdminNodeDescriptor
            {
                Name = definition.Name,
                TypeDiscriminator = definition.TypeDiscriminator,
                DisplayText = schema?.DisplayText,
                Description = schema?.Description,
                Properties = schema?.Properties ?? [],
                RequiredProperties = schema?.RequiredProperties ?? [],
            });
        }

        _cachedNodeDescriptors = descriptors;

        return _cachedNodeDescriptors;
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchemaBuilder> GetNodeSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedNodeSchema is not null)
        {
            return _cachedNodeSchema;
        }

        var descriptors = await GetNodeDescriptorsAsync(cancellationToken);

        _cachedNodeSchema = BuildNodeSchema(descriptors, _maxNestingDepth);

        return _cachedNodeSchema;
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchemaBuilder> GetAdminMenuSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedAdminMenuSchema is not null)
        {
            return _cachedAdminMenuSchema;
        }

        var nodeSchema = await GetNodeSchemaAsync(cancellationToken);

        _cachedAdminMenuSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Id", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("A stable unique identifier for the admin menu. When omitted, one is generated when the recipe runs.")),
                ("Name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The admin menu name shown in the admin menus list.")),
                ("Enabled", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether the admin menu is active. Defaults to true.")),
                ("MenuItems", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(nodeSchema)
                    .Description("The top-level nodes that make up the admin menu.")))
            .Required("Name")
            .AdditionalProperties(true)
            .Description("An admin menu to create or update.");

        return _cachedAdminMenuSchema;
    }

    private static JsonSchemaBuilder BuildNodeSchema(IReadOnlyList<AdminNodeDescriptor> descriptors, int depth)
    {
        var discriminators = descriptors
            .Select(descriptor => descriptor.TypeDiscriminator)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        var childItems = BuildChildItemsSchema(descriptors, depth);

        var conditionals = new List<JsonSchemaBuilder>();

        foreach (var descriptor in descriptors)
        {
            var thenProperties = new List<(string Name, JsonSchemaBuilder Schema)>
            {
                ("$type", new JsonSchemaBuilder()
                    .Const(descriptor.TypeDiscriminator)
                    .Description(BuildNodeDescription(descriptor))),
            };

            thenProperties.AddRange(descriptor.Properties);
            thenProperties.Add(("Items", childItems));

            var thenBuilder = new JsonSchemaBuilder()
                .Properties(thenProperties.ToArray());

            if (descriptor.RequiredProperties.Count > 0)
            {
                thenBuilder = thenBuilder.Required(descriptor.RequiredProperties.ToArray());
            }

            conditionals.Add(new JsonSchemaBuilder()
                .If(new JsonSchemaBuilder()
                    .Properties(("$type", new JsonSchemaBuilder().Const(descriptor.TypeDiscriminator)))
                    .Required("$type"))
                .Then(thenBuilder));
        }

        var builder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(GetSharedNodeProperties(discriminators, childItems))
            .Required("$type")
            .AdditionalProperties(true)
            .Description("An admin menu node. The '$type' discriminator selects the node kind and the members it accepts.");

        if (conditionals.Count > 0)
        {
            builder = builder.AllOf(conditionals.ToArray());
        }

        return builder;
    }

    private static JsonSchemaBuilder BuildChildItemsSchema(IReadOnlyList<AdminNodeDescriptor> descriptors, int depth)
    {
        var items = depth > 1
            ? BuildNodeSchema(descriptors, depth - 1)
            : BuildPermissiveNodeSchema(descriptors);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(items)
            .Description("The child nodes nested under this node.");
    }

    private static JsonSchemaBuilder BuildPermissiveNodeSchema(IReadOnlyList<AdminNodeDescriptor> descriptors)
    {
        var discriminators = descriptors
            .Select(descriptor => descriptor.TypeDiscriminator)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("$type", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .WithSuggestions(discriminators)
                    .Description("The polymorphic admin menu node type discriminator.")))
            .Required("$type")
            .AdditionalProperties(true)
            .Description("A nested admin menu node. The maximum described nesting depth has been reached, so members are validated permissively.");
    }

    private static (string Name, JsonSchemaBuilder Schema)[] GetSharedNodeProperties(string[] discriminators, JsonSchemaBuilder childItems)
        =>
        [
            ("$type", new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .WithSuggestions(discriminators)
                .Description("The polymorphic admin menu node type discriminator, for example OrchardCore.AdminMenu.AdminNodes.LinkAdminNode, OrchardCore.AdminMenu. Required so the node deserializes correctly.")),
            ("UniqueId", new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Description("A stable unique identifier for the node. When omitted, one is generated when the recipe runs.")),
            ("Enabled", new JsonSchemaBuilder()
                .Type(SchemaValueType.Boolean)
                .Description("Whether the node is active. Defaults to true.")),
            ("MenuName", new JsonSchemaBuilder()
                .Type(SchemaValueType.String | SchemaValueType.Null)
                .Description("The name of the admin menu the node belongs to. Populated automatically when the recipe runs.")),
            ("Position", new JsonSchemaBuilder()
                .Type(SchemaValueType.String | SchemaValueType.Null)
                .Description("The relative position of the node among its siblings, for example '10' or 'after'.")),
            ("Priority", new JsonSchemaBuilder()
                .Type(SchemaValueType.Integer)
                .Description("The priority used to resolve which node is marked as selected when several match the request.")),
            ("LinkToFirstChild", new JsonSchemaBuilder()
                .Type(SchemaValueType.Boolean)
                .Description("Whether the node links to the same URL as its first child.")),
            ("LocalNav", new JsonSchemaBuilder()
                .Type(SchemaValueType.Boolean)
                .Description("Whether the node is local to the page, such as a tab.")),
            ("Culture", new JsonSchemaBuilder()
                .Type(SchemaValueType.String | SchemaValueType.Null)
                .Description("The culture for which the node is shown. Leave null to show it for every culture.")),
            ("Classes", new JsonSchemaBuilder()
                .Type(SchemaValueType.Array)
                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                .Description("The CSS classes rendered with the node.")),
            ("Items", childItems),
        ];

    private static string BuildNodeDescription(AdminNodeDescriptor descriptor)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(descriptor.DisplayText))
        {
            parts.Add($"{descriptor.DisplayText} node.");
        }

        if (!string.IsNullOrWhiteSpace(descriptor.Description))
        {
            parts.Add(descriptor.Description.EndsWith('.') ? descriptor.Description : $"{descriptor.Description}.");
        }

        parts.Add($"Fixed '$type' for the '{descriptor.Name}' node.");

        return string.Join(" ", parts);
    }
}
