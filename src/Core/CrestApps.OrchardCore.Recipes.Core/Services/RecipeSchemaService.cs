using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Provides JSON schema generation and step name discovery for Orchard Core recipes.
/// </summary>
public sealed class RecipeSchemaService
{
    private readonly IEnumerable<IRecipeStep> _recipeSteps;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecipeSchemaService"/> class.
    /// </summary>
    /// <param name="recipeSteps">The recipe steps registered by the tenant's enabled features.</param>
    public RecipeSchemaService(IEnumerable<IRecipeStep> recipeSteps)
    {
        _recipeSteps = recipeSteps;
    }

    /// <summary>
    /// Gets the JSON schema for a specific recipe step by name.
    /// </summary>
    /// <param name="stepName">The name of the recipe step.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public ValueTask<JsonSchema> GetStepSchemaAsync(string stepName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

        var step = _recipeSteps.FirstOrDefault(s => string.Equals(s.Name, stepName, StringComparison.OrdinalIgnoreCase));

        if (step is not null)
        {
            return step.GetSchemaAsync(cancellationToken);
        }

        return ValueTask.FromResult<JsonSchema>(null);
    }

    /// <summary>
    /// Gets the full JSON schema for an Orchard Core recipe, including metadata and step definitions.
    /// </summary>
    public ValueTask<JsonSchema> GetRecipeSchemaAsync(CancellationToken cancellationToken = default)
        => GetRecipeSchemaAsync(null, cancellationToken);

    /// <summary>
    /// Gets the full JSON schema for an Orchard Core recipe, optionally narrowing the composed step definitions to a single step.
    /// </summary>
    /// <param name="requestedStep">The optional recipe step name whose definition should be included.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async ValueTask<JsonSchema> GetRecipeSchemaAsync(string requestedStep, CancellationToken cancellationToken = default)
    {
        var stepNames = GetStepNames()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(requestedStep) &&
            !stepNames.Contains(requestedStep, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var recipeStepsByName = _recipeSteps
            .GroupBy(step => step.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var selectedStepNames = string.IsNullOrWhiteSpace(requestedStep)
            ? stepNames
            : stepNames
                .Where(stepName => string.Equals(stepName, requestedStep, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        var stepSchemas = new List<JsonSchema>();

        foreach (var stepName in selectedStepNames)
        {
            JsonSchema schema = null;

            if (recipeStepsByName.TryGetValue(stepName, out var recipeStep))
            {
                schema = await recipeStep.GetSchemaAsync(cancellationToken);
            }

            if (schema is not null)
            {
                stepSchemas.Add(schema);
                continue;
            }

            stepSchemas.Add(BuildFallbackStepSchema(stepName));
        }

        JsonSchemaBuilder stepsItemBuilder;

        if (stepSchemas.Count > 0)
        {
            // Build if/then pairs for discriminated union.
            // This pattern allows Monaco to suggest name values from the enum
            // AND per-step properties once the name is typed.
            var ifThenNodes = new JsonArray();

            foreach (var stepSchema in stepSchemas)
            {
                var stepNode = JsonSerializer.SerializeToNode(stepSchema, JOptions.Default);

                if (stepNode is not JsonObject stepObj ||
                    !stepObj.TryGetPropertyValue("properties", out var propsNode) ||
                    propsNode is not JsonObject propsObj ||
                    !propsObj.TryGetPropertyValue("name", out var nameNode) ||
                    nameNode is not JsonObject nameObj ||
                    !nameObj.TryGetPropertyValue("const", out var constNode))
                {
                    continue;
                }

                var constValue = constNode?.GetValue<string>();

                if (string.IsNullOrEmpty(constValue))
                {
                    continue;
                }

                var ifThenEntry = new JsonObject
                {
                    ["if"] = new JsonObject
                    {
                        ["properties"] = new JsonObject
                        {
                            ["name"] = new JsonObject
                            {
                                ["const"] = constValue,
                            },
                        },
                        ["required"] = new JsonArray("name"),
                    },
                    ["then"] = stepNode,
                };

                ifThenNodes.Add(ifThenEntry);
            }

            stepsItemBuilder = new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("name", new JsonSchemaBuilder()
                        .Type(SchemaValueType.String)
                        .WithSuggestions(stepNames)))
                .Required("name")
                .AdditionalProperties(true);

            stepsItemBuilder.Add("allOf", ifThenNodes);
        }
        else
        {
            stepsItemBuilder = new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("name", new JsonSchemaBuilder()
                        .Type(SchemaValueType.String)
                        .WithSuggestions(GetStepNames())))
                .Required("name")
                .AdditionalProperties(true);
        }

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A unique name for the recipe.")),
                ("displayName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A display name for the recipe.")),
                ("description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A description for the recipe.")),
                ("author", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The author of the recipe.")),
                ("website", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The website URL associated with the recipe.")),
                ("version", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The version of the recipe.")),
                ("isSetupRecipe", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether this recipe is a setup recipe.")),
                ("exportUtc", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The UTC date/time when the recipe was exported.")),
                ("categories", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("Categories for the recipe.")),
                ("tags", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("Tags for the recipe.")),
                ("requireNewScope", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Default(true)
                    .Description("Whether the recipe requires a new scope.")),
                ("steps", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(stepsItemBuilder)
                    .MinItems(1)
                    .Description("The collection of recipe steps.")))
            .Required("steps")
            .Build();
    }

    private static JsonSchema BuildFallbackStepSchema(string stepName)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Const(stepName)))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

    /// <summary>
    /// Gets the names of the recipe steps registered by the tenant's enabled features.
    /// </summary>
    /// <remarks>
    /// A step is listed once a feature registers an <see cref="IRecipeStep"/> for it. Orchard Core does not
    /// expose the name a recipe step handler answers to, so a handler with no registered step is not listed.
    /// </remarks>
    public IEnumerable<string> GetStepNames()
        => _recipeSteps
            .Select(step => step.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct()
            .Order()
            .ToArray();
}
