using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Provides JSON schema generation and step name discovery for Orchard Core recipes.
/// </summary>
public sealed class RecipeSchemaService
{
    private readonly IEnumerable<IRecipeStepHandler> _handlers;
    private readonly IEnumerable<IRecipeStep> _recipeSteps;
    private readonly IMemoryCache _memoryCache;

    private const string _stepNamesCacheKey = "RecipeStepNames";

    private string[] _names = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecipeSchemaService"/> class.
    /// </summary>
    /// <param name="handlers">The registered recipe step handlers.</param>
    /// <param name="recipeSteps">The registered recipe step schema providers.</param>
    /// <param name="memoryCache">The memory cache used to store discovered step names.</param>
    public RecipeSchemaService(
        IEnumerable<IRecipeStepHandler> handlers,
        IEnumerable<IRecipeStep> recipeSteps,
        IMemoryCache memoryCache)
    {
        _handlers = handlers;
        _recipeSteps = recipeSteps;
        _memoryCache = memoryCache;
        _names = _memoryCache.TryGetValue(_stepNamesCacheKey, out string[] cachedNames)
        ? cachedNames
        : null;
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
    public async ValueTask<JsonSchema> GetRecipeSchemaAsync()
    {
        var stepSchemas = new List<JsonSchema>();

        foreach (var step in _recipeSteps)
        {
            var schema = await step.GetSchemaAsync();

            if (schema is not null)
            {
                stepSchemas.Add(schema);
            }
        }

        JsonSchemaBuilder stepsItemBuilder;

        if (stepSchemas.Count > 0)
        {
            var stepNames = _recipeSteps
                .Select(s => s.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

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
                        .Enum(stepNames)))
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
                        .Enum(GetStepNames())))
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

    /// <summary>
    /// Gets the names of all known recipe steps by inspecting registered handlers and step providers.
    /// </summary>
    public IEnumerable<string> GetStepNames()
    {
        _names ??= _handlers
            .Where(h =>
        h.GetType() == typeof(NamedRecipeStepHandler) ||
            h.GetType().IsSubclassOf(typeof(NamedRecipeStepHandler)))
            .Select(h =>
        (string)h.GetType()
            .GetField("StepName", BindingFlags.Instance | BindingFlags.NonPublic)
        ?.GetValue(h))
            .Where(name => name != null)
            .Union(_recipeSteps.Select(s => s.Name).Where(name => !string.IsNullOrEmpty(name)))
            .Distinct()
            .Order()
            .ToArray() ?? [];

        _memoryCache.Set(_stepNamesCacheKey, _names, TimeSpan.FromHours(1));

        return _names;
    }
}
