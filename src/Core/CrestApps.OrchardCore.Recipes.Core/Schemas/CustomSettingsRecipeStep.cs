using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "custom-settings" recipe step — updates custom settings content items stored in site settings.
/// </summary>
public sealed class CustomSettingsRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "custom-settings";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("custom-settings")))
            .Required("name")
            .MinProperties(2)
            .AdditionalProperties(true)
            .Description("Each additional property is a custom settings content type name with its content item data.")
            .Build();
}
