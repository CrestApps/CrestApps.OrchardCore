using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Composes the rule condition and operator schemas from the registered
/// <see cref="IRuleConditionSchemaDefinition"/> and <see cref="IRuleConditionOperatorSchemaDefinition"/>
/// contributions into the schema used by the <c>Layers</c> recipe step.
/// </summary>
public sealed class RuleSchemaService : IRuleSchemaService
{
    /// <summary>
    /// The maximum nesting depth for which condition groups are described with the full, per-type schema.
    /// Groups nested deeper remain valid but are validated with a permissive condition schema.
    /// </summary>
    private const int _maxNestingDepth = 2;

    private readonly IEnumerable<IRuleConditionSchemaDefinition> _conditionDefinitions;
    private readonly IEnumerable<IRuleConditionOperatorSchemaDefinition> _operatorDefinitions;
    private readonly IRecipeSchemaExampleService _exampleService;

    private IReadOnlyList<RuleConditionDescriptor> _cachedConditionDescriptors;
    private IReadOnlyList<RuleConditionOperatorDescriptor> _cachedOperatorDescriptors;
    private JsonSchemaBuilder _cachedConditionSchema;
    private JsonSchemaBuilder _cachedLayerRuleSchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuleSchemaService"/> class.
    /// </summary>
    /// <param name="conditionDefinitions">The registered rule condition schema definitions.</param>
    /// <param name="operatorDefinitions">The registered rule condition operator schema definitions.</param>
    /// <param name="exampleService">The service that supplies live tenant example values.</param>
    public RuleSchemaService(
        IEnumerable<IRuleConditionSchemaDefinition> conditionDefinitions,
        IEnumerable<IRuleConditionOperatorSchemaDefinition> operatorDefinitions,
        IRecipeSchemaExampleService exampleService)
    {
        _conditionDefinitions = conditionDefinitions;
        _operatorDefinitions = operatorDefinitions;
        _exampleService = exampleService;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<RuleConditionOperatorDescriptor>> GetOperatorDescriptorsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedOperatorDescriptors is not null)
        {
            return _cachedOperatorDescriptors;
        }

        var definitions = _operatorDefinitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Name))
            .GroupBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var descriptors = new List<RuleConditionOperatorDescriptor>();

        foreach (var definition in definitions.Values.OrderBy(definition => definition.Name, StringComparer.Ordinal))
        {
            var context = new RuleConditionOperatorSchemaContext
            {
                OperatorName = definition.Name,
            };

            var schema = await definition.GetOperatorSchemaAsync(context, cancellationToken);

            descriptors.Add(new RuleConditionOperatorDescriptor
            {
                Name = definition.Name,
                TypeDiscriminator = definition.TypeDiscriminator,
                DisplayText = schema?.DisplayText,
                Description = schema?.Description,
                Properties = schema?.Properties ?? [],
            });
        }

        _cachedOperatorDescriptors = descriptors;

        return _cachedOperatorDescriptors;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<RuleConditionDescriptor>> GetConditionDescriptorsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedConditionDescriptors is not null)
        {
            return _cachedConditionDescriptors;
        }

        var definitions = _conditionDefinitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Name))
            .GroupBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var context = new RuleConditionSchemaContext
        {
            OperatorSchema = await GetOperatorSchemaAsync(cancellationToken),
            Examples = await _exampleService.GetExamplesAsync(cancellationToken),
        };

        var descriptors = new List<RuleConditionDescriptor>();

        foreach (var definition in definitions.Values.OrderBy(definition => definition.Name, StringComparer.Ordinal))
        {
            var schema = await definition.GetConditionSchemaAsync(context, cancellationToken);

            descriptors.Add(new RuleConditionDescriptor
            {
                Name = definition.Name,
                TypeDiscriminator = definition.TypeDiscriminator,
                DisplayText = schema?.DisplayText,
                Description = schema?.Description,
                IsGroup = schema?.IsGroup ?? false,
                Properties = schema?.Properties ?? [],
                RequiredProperties = schema?.RequiredProperties ?? [],
            });
        }

        _cachedConditionDescriptors = descriptors;

        return _cachedConditionDescriptors;
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchemaBuilder> GetOperatorSchemaAsync(CancellationToken cancellationToken = default)
    {
        var descriptors = await GetOperatorDescriptorsAsync(cancellationToken);

        return BuildOperatorSchema(descriptors);
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchemaBuilder> GetConditionSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedConditionSchema is not null)
        {
            return _cachedConditionSchema;
        }

        var descriptors = await GetConditionDescriptorsAsync(cancellationToken);

        _cachedConditionSchema = BuildConditionSchema(descriptors, _maxNestingDepth);

        return _cachedConditionSchema;
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchemaBuilder> GetLayerRuleSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedLayerRuleSchema is not null)
        {
            return _cachedLayerRuleSchema;
        }

        var conditionSchema = await GetConditionSchemaAsync(cancellationToken);

        _cachedLayerRuleSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("An optional name for the rule shown in the layer editor.")),
                ("ConditionId", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("A stable unique identifier for the rule. When omitted, one is generated when the recipe runs.")),
                ("Conditions", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(conditionSchema)
                    .Description("The conditions that make up the rule. Every top-level condition must evaluate to true for the layer to be active.")))
            .AdditionalProperties(true)
            .Description("The structured rule evaluated to decide whether the layer is active for the current request.");

        return _cachedLayerRuleSchema;
    }

    private static JsonSchemaBuilder BuildOperatorSchema(IReadOnlyList<RuleConditionOperatorDescriptor> descriptors)
    {
        var discriminators = descriptors
            .Select(descriptor => descriptor.TypeDiscriminator)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        var conditionals = new List<JsonSchemaBuilder>();

        foreach (var descriptor in descriptors)
        {
            var thenProperties = new List<(string Name, JsonSchemaBuilder Schema)>
            {
                ("$type", new JsonSchemaBuilder()
                    .Const(descriptor.TypeDiscriminator)
                    .Description(BuildOperatorDescription(descriptor))),
            };

            thenProperties.AddRange(descriptor.Properties);

            conditionals.Add(new JsonSchemaBuilder()
                .If(new JsonSchemaBuilder()
                    .Properties(("$type", new JsonSchemaBuilder().Const(descriptor.TypeDiscriminator)))
                    .Required("$type"))
                .Then(new JsonSchemaBuilder().Properties(thenProperties.ToArray())));
        }

        var builder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("$type", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .WithSuggestions(discriminators)
                    .Description("The polymorphic operator type discriminator, for example OrchardCore.Rules.Models.StringStartsWithOperator, OrchardCore.Rules.")))
            .Required("$type")
            .AdditionalProperties(true)
            .Description("The operator that determines how the condition value is compared against the current request.");

        if (conditionals.Count > 0)
        {
            builder = builder.AllOf(conditionals.ToArray());
        }

        return builder;
    }

    private static JsonSchemaBuilder BuildConditionSchema(IReadOnlyList<RuleConditionDescriptor> descriptors, int depth)
    {
        var names = descriptors
            .Select(descriptor => descriptor.Name)
            .ToArray();
        var discriminators = descriptors
            .Select(descriptor => descriptor.TypeDiscriminator)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        var conditionals = new List<JsonSchemaBuilder>();

        JsonSchemaBuilder nestedConditions = null;
        if (descriptors.Any(descriptor => descriptor.IsGroup))
        {
            nestedConditions = BuildNestedConditionsSchema(descriptors, depth);
        }

        foreach (var descriptor in descriptors)
        {
            var thenProperties = new List<(string Name, JsonSchemaBuilder Schema)>
            {
                ("$type", new JsonSchemaBuilder()
                    .Const(descriptor.TypeDiscriminator)
                    .Description(BuildConditionDescription(descriptor))),
            };

            thenProperties.AddRange(descriptor.Properties);

            if (descriptor.IsGroup)
            {
                thenProperties.Add(("Conditions", nestedConditions));
            }

            var thenBuilder = new JsonSchemaBuilder()
                .Properties(thenProperties.ToArray());

            if (descriptor.RequiredProperties.Count > 0)
            {
                thenBuilder = thenBuilder.Required(descriptor.RequiredProperties.ToArray());
            }

            conditionals.Add(new JsonSchemaBuilder()
                .If(new JsonSchemaBuilder()
                    .Properties(("Name", new JsonSchemaBuilder().Const(descriptor.Name)))
                    .Required("Name"))
                .Then(thenBuilder));
        }

        var builder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("$type", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .WithSuggestions(discriminators)
                    .Description("The polymorphic condition type discriminator, for example OrchardCore.Rules.Models.UrlCondition, OrchardCore.Rules. Required so nested conditions deserialize correctly.")),
                ("Name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .WithSuggestions(names)
                    .Description("The condition type name, for example UrlCondition. Used to resolve the condition factory when the recipe runs.")),
                ("ConditionId", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("A stable unique identifier for the condition. When omitted, one is generated when the recipe runs.")))
            .Required("$type", "Name")
            .AdditionalProperties(true);

        if (conditionals.Count > 0)
        {
            builder = builder.AllOf(conditionals.ToArray());
        }

        return builder;
    }

    private static JsonSchemaBuilder BuildNestedConditionsSchema(IReadOnlyList<RuleConditionDescriptor> descriptors, int depth)
    {
        var items = depth > 1
            ? BuildConditionSchema(descriptors, depth - 1)
            : BuildPermissiveConditionSchema(descriptors);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(items)
            .Description("The conditions nested inside this condition group.");
    }

    private static JsonSchemaBuilder BuildPermissiveConditionSchema(IReadOnlyList<RuleConditionDescriptor> descriptors)
    {
        var names = descriptors
            .Select(descriptor => descriptor.Name)
            .ToArray();
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
                    .Description("The polymorphic condition type discriminator.")),
                ("Name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .WithSuggestions(names)
                    .Description("The condition type name.")))
            .Required("$type", "Name")
            .AdditionalProperties(true)
            .Description("A nested condition. The maximum described nesting depth has been reached, so members are validated permissively.");
    }

    private static string BuildConditionDescription(RuleConditionDescriptor descriptor)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(descriptor.DisplayText))
        {
            parts.Add($"{descriptor.DisplayText} condition.");
        }

        if (!string.IsNullOrWhiteSpace(descriptor.Description))
        {
            parts.Add(descriptor.Description.EndsWith('.') ? descriptor.Description : $"{descriptor.Description}.");
        }

        parts.Add($"Fixed '$type' for the '{descriptor.Name}' condition.");

        return string.Join(" ", parts);
    }

    private static string BuildOperatorDescription(RuleConditionOperatorDescriptor descriptor)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(descriptor.DisplayText))
        {
            parts.Add($"{descriptor.DisplayText}.");
        }

        if (!string.IsNullOrWhiteSpace(descriptor.Description))
        {
            parts.Add(descriptor.Description.EndsWith('.') ? descriptor.Description : $"{descriptor.Description}.");
        }

        parts.Add($"Fixed '$type' for the '{descriptor.Name}' operator.");

        return string.Join(" ", parts);
    }
}
