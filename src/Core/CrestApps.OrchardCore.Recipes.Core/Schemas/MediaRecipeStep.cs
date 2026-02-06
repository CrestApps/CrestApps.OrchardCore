namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Schema for the "media" recipe step — imports media files.
/// </summary>
public sealed class MediaRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "media";

    public ValueTask<JsonSchema> GetSchemaAsync()
    {
        _cached ??= CreateSchema();
        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("media")),
                ("Files", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("TargetPath", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Path", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Description("Alias for TargetPath. Path where the content will be written.")),
                            ("SourcePath", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Description("Relative path from the recipe file to the source media.")),
                            ("SourceUrl", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Description("Absolute URL to download the media from.")),
                            ("Base64", new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Description("Base64-encoded content of the file.")))
                        .Required("TargetPath")
                        .AdditionalProperties(true))))
            .Required("name", "Files")
            .AdditionalProperties(true)
            .Build();
}
