using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "MediaProfiles" recipe step — creates or updates media processing profiles.
/// </summary>
public sealed class MediaProfilesRecipeStep : IRecipeStep
{
    private JsonSchema _cached;
    public string Name => "MediaProfiles";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        var profileItemSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Short note shown to editors explaining when to use the media profile.")),
                ("Width", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Target width in pixels.")),
                ("Height", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Target height in pixels.")),
                ("Mode", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum(MediaProfileEnums.ResizeModes)
                    .Description("The image resize mode.")),
                ("Format", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum(MediaProfileEnums.OutputFormats)
                    .Description("The output image format.")),
                ("Quality", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("Encoder quality percentage used by formats that support lossy compression.")),
                ("BackgroundColor", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Background color used by pad-style resize modes.")))
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("MediaProfiles").Description("Recipe step discriminator. Must be 'MediaProfiles'.")),
                ("MediaProfiles", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(profileItemSchema)
                    .Description("A dictionary keyed by profile name. Each value is a media profile object.")))
            .Required("name", "MediaProfiles")
            .AdditionalProperties(true)
            .Build();
    }

    /// <summary>
    /// Common enum values used in media profile schemas.
    /// </summary>
    private static class MediaProfileEnums
    {
        /// <summary>The resize mode values for media profiles.</summary>
        public static readonly string[] ResizeModes = ["Undefined", "Max", "Crop", "Pad", "BoxPad", "Min", "Stretch"];

        /// <summary>The output format values for media profiles.</summary>
        public static readonly string[] OutputFormats = ["Undefined", "Bmp", "Gif", "Jpg", "Png", "Tga", "WebP"];
    }
}
