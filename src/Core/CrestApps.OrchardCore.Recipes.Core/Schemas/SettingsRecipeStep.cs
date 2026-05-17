using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the <c>Settings</c> recipe step.
/// </summary>
public sealed class SettingsRecipeStep(IEnumerable<ISiteSettingsSchemaDefinition> schemaDefinitions) : IRecipeStep
{
    private readonly IEnumerable<ISiteSettingsSchemaDefinition> _schemaDefinitions = schemaDefinitions;

    private JsonSchema _cached;

    public string Name => "Settings";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public async ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var properties = CreateBaseProperties();

        foreach (var schemaDefinition in _schemaDefinitions.OrderBy(definition => definition.Name, StringComparer.Ordinal))
        {
            properties[schemaDefinition.Name] = await schemaDefinition.GetSchemaAsync(cancellationToken);
        }

        _cached = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(properties)
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return _cached;
    }

    private static Dictionary<string, JsonSchemaBuilder> CreateBaseProperties()
    {
        return new Dictionary<string, JsonSchemaBuilder>(StringComparer.Ordinal)
        {
            ["name"] = RecipeStepSchemaBuilders.String().Const("Settings"),
            ["BaseUrl"] = RecipeStepSchemaBuilders.Nullable(RecipeStepSchemaBuilders.String()),
            ["Calendar"] = RecipeStepSchemaBuilders.Nullable(RecipeStepSchemaBuilders.String()),
            ["MaxPagedCount"] = RecipeStepSchemaBuilders.Integer(),
            ["MaxPageSize"] = RecipeStepSchemaBuilders.Integer(),
            ["PageSize"] = RecipeStepSchemaBuilders.Integer(),
            ["ResourceDebugMode"] = RecipeStepSchemaBuilders.String().Enum(["FromConfiguration", "Enabled", "Disabled"]),
            ["SiteName"] = RecipeStepSchemaBuilders.String(),
            ["PageTitleFormat"] = RecipeStepSchemaBuilders.String(),
            ["SiteSalt"] = RecipeStepSchemaBuilders.String(),
            ["SuperUser"] = RecipeStepSchemaBuilders.String(),
            ["TimeZoneId"] = RecipeStepSchemaBuilders.String(),
            ["UseCdn"] = RecipeStepSchemaBuilders.Boolean(),
            ["CdnBaseUrl"] = RecipeStepSchemaBuilders.Nullable(RecipeStepSchemaBuilders.String()),
            ["AppendVersion"] = RecipeStepSchemaBuilders.Boolean(),
            ["HomeRoute"] = RecipeStepSchemaBuilders.Nullable(BuildHomeRoute()),
            ["CacheMode"] = RecipeStepSchemaBuilders.String().Enum(["FromConfiguration", "Enabled", "DebugEnabled", "Disabled"]),
        };
    }

    private static JsonSchemaBuilder BuildHomeRoute()
        => RecipeStepSchemaBuilders.Object(
        [
            ("Area", RecipeStepSchemaBuilders.String()),
            ("Controller", RecipeStepSchemaBuilders.String()),
            ("Action", RecipeStepSchemaBuilders.String()),
        ],
        requiredProperties: ["Area", "Controller", "Action"],
        additionalProperties: true);
}
