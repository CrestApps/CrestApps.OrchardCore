using System.Reflection;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using Json.Schema;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

public sealed class RecipeSchemaService
{
    private readonly IEnumerable<IRecipeStepHandler> _handlers;
    private readonly IEnumerable<IRecipeStep> _recipeSteps;
    private readonly IMemoryCache _memoryCache;

    private string[] _names = null;

    public RecipeSchemaService(
        IEnumerable<IRecipeStepHandler> handlers,
        IEnumerable<IRecipeStep> recipeSteps,
        IMemoryCache memoryCache)
    {
        _handlers = handlers;
        _recipeSteps = recipeSteps;
        _memoryCache = memoryCache;
        _names = _memoryCache.TryGetValue("RecipeStepNames", out string[] cachedNames)
            ? cachedNames
            : null;
    }

    public ValueTask<JsonSchema> GetStepSchemaAsync(string stepName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

        var step = _recipeSteps.FirstOrDefault(s => string.Equals(s.Name, stepName, StringComparison.OrdinalIgnoreCase));

        if (step is not null)
        {
            return step.GetSchemaAsync();
        }

        return ValueTask.FromResult<JsonSchema>(null);
    }

    public async ValueTask<JsonSchema> GetRecipeSchemaAsync()
    {
        var stepSchemas = new Dictionary<string, JsonSchema>(StringComparer.OrdinalIgnoreCase);

        foreach (var stepName in GetStepNames())
        {
            if (stepSchemas.ContainsKey(stepName))
            {
                continue;
            }

            JsonSchema stepSchema = null;

            foreach (var recipeStep in _recipeSteps)
            {
                if (!string.Equals(recipeStep.Name, stepName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                stepSchema = await recipeStep.GetSchemaAsync();
                break;
            }

            stepSchema ??= new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("name", new JsonSchemaBuilder()
                        .Type(SchemaValueType.String)
                        .Enum(stepName)))
                .Required("name")
                .Build();

            stepSchemas[stepName] = stepSchema;
        }

        var stepsBuilder = new JsonSchemaBuilder().OneOf(stepSchemas.Values);

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
                    .Items(stepsBuilder)
                    .MinItems(1)
                    .Description("The collection of recipe steps.")))
            .Required("steps")
            .Build();
    }

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
             .Distinct()
             .ToArray() ?? [];

        _memoryCache.Set(_names, _names, TimeSpan.FromHours(1));

        return _names;
    }
}
