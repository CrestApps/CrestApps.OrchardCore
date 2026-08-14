using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Represents the publish later part schema.
/// </summary>
public sealed class PublishLaterPartSchema : PartSchemaDefinitionBase
{
    public override string Name { get; } = "PublishLaterPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ScheduledPublishUtc", new JsonSchemaBuilder().AnyOf(
                    new JsonSchemaBuilder()
                        .Type(SchemaValueType.String)
                        .Description("An ISO-8601 UTC date/time value."),
                    new JsonSchemaBuilder().Type(SchemaValueType.Null))))
            .AdditionalProperties(true);
}
