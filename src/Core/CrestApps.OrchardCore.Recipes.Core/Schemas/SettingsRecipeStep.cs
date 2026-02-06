namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "Settings" recipe step — the simplest step that
/// pushes arbitrary key-value site settings.
/// </summary>
public sealed class SettingsRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "Settings";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("settings")))
            .Required("name")
            .MinProperties(2)
            .AdditionalProperties(true)
            .Build();
}
