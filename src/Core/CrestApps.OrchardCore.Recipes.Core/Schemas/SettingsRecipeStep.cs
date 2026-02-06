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
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("settings")),
                ("BaseUrl", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("Calendar", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("MaxPagedCount", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                ("MaxPageSize", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                ("PageSize", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                ("ResourceDebugMode", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("SiteName", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("PageTitleFormat", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("SiteSalt", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("SuperUser", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("TimeZoneId", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("UseCdn", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                ("CdnBaseUrl", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("AppendVersion", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                ("HomeRoute", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)),
                ("CacheMode", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .Required("name")
            .AdditionalProperties(true)
            .Build();
}
